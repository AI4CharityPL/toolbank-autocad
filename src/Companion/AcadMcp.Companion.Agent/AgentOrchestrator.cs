using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Companion.Agent.Providers;
using AcadMcp.Companion.Agent.Settings;
using AcadMcp.Companion.Mcp;

namespace AcadMcp.Companion.Agent;

/// <summary>
/// Owns the model-facing conversation and runs the tool-calling loop. Supports:
/// • auto-continuation when the token limit truncates an answer ("nawet przy limicie znaków"),
/// • an optional planning pass (planner -> sequential executor steps, like Cursor's plan/agent),
/// • a client-side visualization tool that renders a room image via the provider's image model.
/// </summary>
public sealed class AgentOrchestrator
{
    /// <summary>Name of the local (non-MCP) tool that generates a room visualization image.</summary>
    public const string VisualizationTool = "render_visualization";

    private const int MaxContinuations = 6;

    private readonly IChatProvider _provider;
    private readonly McpStdioClient _mcp;
    private readonly CompanionSettings _settings;
    private readonly string _model;
    private readonly Action<string>? _log;
    private readonly List<ChatMessage> _history = new();

    public AgentOrchestrator(IChatProvider provider, McpStdioClient mcp, CompanionSettings settings, Action<string>? log = null)
    {
        _provider = provider;
        _mcp = mcp;
        _settings = settings;
        _model = settings.ModelFor(provider.Kind);
        _log = log;
    }

    private void Log(string msg) { try { _log?.Invoke("[agent] " + msg); } catch { } }

    public IReadOnlyList<ChatMessage> History => _history;

    public void Reset() => _history.Clear();

    public async Task<string> SendAsync(IReadOnlyList<ContentPart> userParts, IAgentObserver observer, CancellationToken ct)
    {
        _history.Add(new ChatMessage { Role = ChatRole.User, Content = new List<ContentPart>(userParts) });
        Log($"SendAsync model={_model} provider={_provider.Kind} planMode={_settings.PlanMode} imageCapable={(_provider is IImageGenerator g && g.CanGenerateImages)}");

        return _settings.PlanMode
            ? await RunWithPlanningAsync(observer, ct).ConfigureAwait(false)
            : await RunAgentLoopAsync(observer, ct).ConfigureAwait(false);
    }

    // ─────────── planning pipeline ───────────

    private async Task<string> RunWithPlanningAsync(IAgentObserver observer, CancellationToken ct)
    {
        observer.OnStatus("Tworzę plan...");
        var tools = BuildToolDefinitions();

        // Planner turn: read-only reasoning that yields a numbered plan. Tools are available so
        // the planner can inspect the drawing first (acad_status etc.), but it must end with a plan.
        var planText = await RunAgentLoopAsync(observer, ct, systemOverride: PlannerSystemPrompt, tools: tools).ConfigureAwait(false);
        observer.OnPlanUpdate(planText);

        var steps = ParsePlanSteps(planText);
        if (steps.Count == 0)
        {
            Log("planner produced no parseable steps; returning plan text");
            return planText;
        }
        Log($"plan has {steps.Count} step(s)");

        for (int i = 0; i < steps.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            observer.OnStatus($"Wykonuję krok {i + 1}/{steps.Count}...");
            observer.OnPlanUpdate($"▶ Krok {i + 1}/{steps.Count}: {steps[i]}");

            _history.Add(ChatMessage.UserText(
                $"Wykonaj teraz krok {i + 1} z {steps.Count} planu: \"{steps[i]}\". " +
                "Użyj narzędzi AutoCAD, aby zrealizować ten krok w rysunku. " +
                "Gdy skończysz, napisz jednozdaniowe podsumowanie tego kroku."));

            await RunAgentLoopAsync(observer, ct, tools: tools).ConfigureAwait(false);
        }

        // Final synthesis: the step loops stream into their own bubbles, but users expect ONE
        // closing answer. Run a text-only pass (no tools) in a fresh UI section.
        observer.OnPlanUpdate("✓ Wszystkie kroki wykonane — przygotowuję podsumowanie końcowe...");
        observer.OnSectionBreak();
        observer.OnStatus("Tworzę podsumowanie końcowe...");
        _history.Add(ChatMessage.UserText(
            "Wszystkie kroki planu zostały wykonane. Napisz końcowe podsumowanie dla użytkownika: " +
            "co dokładnie zostało zrobione, jakie są wyniki (liczby, nazwy, pliki), czy coś wymaga uwagi " +
            "i jakie są sensowne następne kroki. NIE wywołuj już narzędzi — podsumuj wyłącznie na podstawie " +
            "historii rozmowy i wyników narzędzi z poprzednich kroków."));

        var finalResult = await RunAgentLoopAsync(observer, ct, tools: Array.Empty<ToolDefinition>()).ConfigureAwait(false);
        observer.OnStatus("Plan wykonany.");
        return string.IsNullOrWhiteSpace(finalResult)
            ? "Plan wykonany, ale model nie zwrócił podsumowania końcowego — sprawdź wyniki kroków powyżej."
            : finalResult;
    }

    // ─────────── core agent loop (tools + continuation) ───────────

    private async Task<string> RunAgentLoopAsync(
        IAgentObserver observer,
        CancellationToken ct,
        string? systemOverride = null,
        IReadOnlyList<ToolDefinition>? tools = null)
    {
        tools ??= BuildToolDefinitions();
        var system = systemOverride ?? SystemPrompt;
        var fullText = new StringBuilder();
        int toolIterations = 0;
        int continuations = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var request = new ChatRequest
            {
                Model = _model,
                SystemPrompt = system,
                Messages = _history,
                Tools = tools,
                MaxTokens = _settings.MaxTokens,
                Temperature = _settings.Temperature,
                StreamIdleTimeout = TimeSpan.FromSeconds(Math.Max(5, _settings.StreamIdleTimeoutSeconds)),
            };

            observer.OnStatus(toolIterations == 0 && continuations == 0 ? "Analizuję..." : "Kontynuuję...");
            AssistantTurn turn = await SendTurnWithTimeoutAsync(request, observer, ct).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(turn.Text)) fullText.Append(turn.Text);
            Log($"turn: textLen={turn.Text.Length} stop={turn.StopReason} toolCalls={turn.ToolCalls.Count}"
                + (turn.HasToolCalls ? " [" + string.Join(", ", turn.ToolCalls.Select(c => c.Name)) + "]" : ""));

            var assistantMsg = new ChatMessage { Role = ChatRole.Assistant };
            if (!string.IsNullOrEmpty(turn.Text)) assistantMsg.Content.Add(ContentPart.FromText(turn.Text));
            assistantMsg.ToolCalls.AddRange(turn.ToolCalls);
            _history.Add(assistantMsg);

            if (turn.HasToolCalls)
            {
                await ExecuteToolCallsAsync(turn.ToolCalls, observer, ct).ConfigureAwait(false);
                if (++toolIterations >= _settings.MaxToolIterations)
                {
                    observer.OnStatus("Osiągnięto limit kroków narzędzi.");
                    Log("hit MaxToolIterations");
                    break;
                }
                continue;
            }

            // No tool calls. If the model was cut off by the token limit, auto-continue so a long
            // build/answer is finished even past the per-response character limit.
            if (turn.StopReason == StopReason.Length && continuations < MaxContinuations)
            {
                continuations++;
                Log($"auto-continue {continuations}/{MaxContinuations} (token limit)");
                observer.OnTextDelta("\n");
                _history.Add(ChatMessage.UserText(
                    "Twoja poprzednia odpowiedź została ucięta przez limit długości. " +
                    "Kontynuuj DOKŁADNIE od miejsca, w którym przerwałeś — nie powtarzaj już napisanego tekstu " +
                    "i nie zaczynaj od nowa. Jeśli to było zadanie budowania w rysunku, dokończ pozostałe operacje."));
                continue;
            }

            break;
        }

        return fullText.ToString();
    }

    /// <summary>
    /// Runs one model turn under a hard wall-clock timeout. If the turn stalls (silent stream,
    /// connect/TLS hang) it is aborted and retried ONCE; a second stall surfaces a clean error
    /// instead of freezing the chat. User-initiated cancellation is never swallowed.
    /// </summary>
    private async Task<AssistantTurn> SendTurnWithTimeoutAsync(ChatRequest request, IAgentObserver observer, CancellationToken ct)
    {
        var turnTimeout = TimeSpan.FromSeconds(Math.Max(15, _settings.TurnTimeoutSeconds));
        for (int attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            using var turnCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            turnCts.CancelAfter(turnTimeout);
            try
            {
                return await _provider.SendAsync(request, observer.OnTextDelta, turnCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // user pressed Cancel — propagate
            }
            catch (Exception ex) when (ex is TimeoutException
                                       || (ex is OperationCanceledException && turnCts.IsCancellationRequested))
            {
                Log($"turn stalled (attempt {attempt}) after {turnTimeout.TotalSeconds:N0}s: {ex.Message}");
                if (attempt >= 2)
                    throw new ProviderException(
                        $"Model nie odpowiedział w wyznaczonym czasie ({turnTimeout.TotalSeconds:N0} s) mimo ponowienia. " +
                        "Sprawdź połączenie/klucz API albo spróbuj ponownie — rozmowa nie została utracona.");
                observer.OnStatus("Model zamilkł — ponawiam...");
                Log("retrying turn once");
            }
            catch (Exception ex)
            {
                Log($"provider THREW {ex.GetType().Name}: {ex.Message}");
                throw;
            }
        }
    }

    private async Task ExecuteToolCallsAsync(IReadOnlyList<ToolCall> calls, IAgentObserver observer, CancellationToken ct)
    {
        var toolMessage = new ChatMessage { Role = ChatRole.Tool };
        foreach (var call in calls)
        {
            ct.ThrowIfCancellationRequested();
            observer.OnToolStarted(call.Name, Summarize(call));

            string content;
            bool isError;
            if (string.Equals(call.Name, VisualizationTool, StringComparison.OrdinalIgnoreCase))
            {
                (content, isError) = await RenderVisualizationAsync(call, observer, ct).ConfigureAwait(false);
            }
            else
            {
                try
                {
                    var result = await _mcp.CallToolAsync(call.Name, call.Arguments, ct).ConfigureAwait(false);
                    content = string.IsNullOrEmpty(result.Text) ? "(brak wyniku)" : result.Text;
                    isError = result.IsError;
                }
                catch (Exception ex)
                {
                    content = $"Błąd wykonania narzędzia: {ex.Message}";
                    isError = true;
                }
            }

            observer.OnToolCompleted(call.Name, isError);
            toolMessage.ToolResults.Add(new ToolResult
            {
                ToolCallId = call.Id,
                Name = call.Name,
                Content = content,
                IsError = isError,
            });
        }
        _history.Add(toolMessage);
    }

    // ─────────── visualization client tool ───────────

    private async Task<(string Content, bool IsError)> RenderVisualizationAsync(ToolCall call, IAgentObserver observer, CancellationToken ct)
    {
        var title = Field(call, "title");
        var prompt = ComposeVisualizationPrompt(call, out var missing);
        if (missing.Count > 0)
            return ("Brak wymaganych danych do dokładnej wizualizacji: " + string.Join(", ", missing) +
                    ". Najpierw pobierz z rysunku: wymiary sali, okna (na której ścianie, rozmiar), drzwi " +
                    "(ściana, szerokość, kierunek otwierania) oraz umeblowanie wewnątrz sali, a potem wywołaj " +
                    "render_visualization wypełniając WSZYSTKIE pola.", true);

        if (_provider is not IImageGenerator gen || !gen.CanGenerateImages)
            return ($"Wybrany dostawca ({_provider.Kind}) nie generuje obrazów. " +
                    "Przełącz na OpenAI lub Google Gemini w Ustawieniach, aby tworzyć wizualizacje.", true);

        try
        {
            Log($"render_visualization composed prompt ({prompt.Length} chars): {Trim(prompt, 240)}");
            var (bytes, media) = await gen.GenerateImageAsync(prompt, ct).ConfigureAwait(false);
            observer.OnImage(bytes, media, string.IsNullOrWhiteSpace(title) ? "Wizualizacja" : title!);
            return ("Wizualizacja została wygenerowana z dokładnego opisu (wymiary, okna, drzwi, światło, " +
                    "umeblowanie) i wyświetlona użytkownikowi w czacie.", false);
        }
        catch (Exception ex)
        {
            Log($"render_visualization FAILED: {ex.Message}");
            return ($"Nie udało się wygenerować wizualizacji: {ex.Message}", true);
        }
    }

    /// <summary>
    /// Builds one rich, photoreal image prompt from the structured room fields the model supplied,
    /// guaranteeing that dimensions, windows + daylight, doors and furniture are all represented.
    /// </summary>
    private static string ComposeVisualizationPrompt(ToolCall call, out List<string> missing)
    {
        var missingList = new List<string>();
        missing = missingList;
        string Need(string key, string label)
        {
            var v = Field(call, key);
            if (string.IsNullOrWhiteSpace(v)) missingList.Add(label);
            return v ?? string.Empty;
        }

        var roomLabel = Need("room_label", "nazwa/numer sali");
        var dimensions = Need("dimensions", "wymiary");
        var windows = Need("windows", "okna");
        var doors = Need("doors", "drzwi");
        var furniture = Need("furniture", "umeblowanie");
        var roomType = Field(call, "room_type");
        var context = Field(call, "building_context");
        var finishes = Field(call, "finishes");
        var viewpoint = Field(call, "viewpoint");
        var spaceKind = Field(call, "space_kind"); // "interior" (default) | "exterior" | "garden"/"yard" etc.
        var extra = Field(call, "prompt") ?? Field(call, "extra");

        if (missing.Count > 0) return string.Empty;

        bool exterior = spaceKind is not null &&
            (spaceKind.IndexOf("exter", StringComparison.OrdinalIgnoreCase) >= 0
             || spaceKind.IndexOf("garden", StringComparison.OrdinalIgnoreCase) >= 0
             || spaceKind.IndexOf("yard", StringComparison.OrdinalIgnoreCase) >= 0
             || spaceKind.IndexOf("ogrod", StringComparison.OrdinalIgnoreCase) >= 0
             || spaceKind.IndexOf("ogr\u00f3d", StringComparison.OrdinalIgnoreCase) >= 0
             || spaceKind.IndexOf("podw", StringComparison.OrdinalIgnoreCase) >= 0);

        var sb = new StringBuilder();
        sb.Append("Photorealistic, architecturally accurate ")
          .Append(exterior ? "exterior visualization, wide-angle view, of " : "interior visualization, eye-level wide-angle view, of ")
          .Append(roomLabel);
        if (!string.IsNullOrWhiteSpace(context)) sb.Append(" (").Append(context).Append(')');
        sb.Append('.');
        if (!string.IsNullOrWhiteSpace(roomType)) sb.Append(" Type: ").Append(roomType).Append('.');
        sb.Append(" Exact dimensions (must match the plan proportions): ").Append(dimensions).Append('.');
        sb.Append(" Windows / openings: ").Append(windows)
          .Append(exterior
              ? " Show window openings on the facade in their correct positions."
              : " Render realistic natural daylight entering through these windows from the correct direction, with soft shadows and accurate light falloff across the space.");
        sb.Append(" Doors: ").Append(doors).Append(" Place doors exactly on the indicated walls/sides.");
        sb.Append(exterior
            ? " Outdoor elements / objects (match the plan — correct items, count, size and placement): "
            : " Furniture and equipment must match the plan (correct items, count, size and placement): ").Append(furniture).Append('.');
        if (!string.IsNullOrWhiteSpace(finishes)) sb.Append(" Finishes and lighting: ").Append(finishes).Append('.');
        if (!string.IsNullOrWhiteSpace(viewpoint)) sb.Append(" Camera / viewpoint: ").Append(viewpoint).Append('.');
        if (!string.IsNullOrWhiteSpace(extra)) sb.Append(' ').Append(extra);
        sb.Append(" Professional architectural rendering, physically accurate scale and proportions, realistic materials, ")
          .Append("global illumination, high detail, no text, no labels, no watermarks, no people unless explicitly stated. ")
          .Append("Do NOT add medical/hospital elements unless the type explicitly says so.");
        return sb.ToString();
    }

    private static string? Field(ToolCall call, string key)
        => call.Arguments[key] is JsonValue v ? v.ToString() : null;

    // ─────────── tool catalog ───────────

    private IReadOnlyList<ToolDefinition> BuildToolDefinitions()
    {
        var defs = new List<ToolDefinition>(AcadToolCatalog.ToDefinitions(_mcp.Tools));
        if (_provider is IImageGenerator g && g.CanGenerateImages)
            defs.Add(VisualizationToolDefinition());
        return defs;
    }

    private static ToolDefinition VisualizationToolDefinition() => new(
        VisualizationTool,
        "Generuje realistyczną wizualizację (render) WSKAZANEJ przestrzeni (pokój, biuro, mieszkanie, sala, ogród, " +
        "podwórze itp.) i pokazuje ją w czacie. NIE zgaduj i NIE zakładaj, że to szpital — typ przestrzeni wywnioskuj " +
        "z jej nazwy i rozmowy. Wypełnij pola DOKŁADNYMI danymi z rysunku: rzeczywiste wymiary i powierzchnię, każde " +
        "okno (ściana, rozmiar, kierunek światła), każde drzwi (ściana, szerokość, kierunek), wszystkie obiekty/meble " +
        "wewnątrz z rozmiarami i rozmieszczeniem. Opisy pól pisz po angielsku (lepsze rendery). Panel złoży z tych pól " +
        "pełny, fotorealistyczny prompt.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["title"] = Str("Krótki podpis wizualizacji w czacie (po polsku), np. „Sala konferencyjna A-304”, „Pokój 12”, „Ogród”."),
                ["room_label"] = Str("Numer i nazwa pomieszczenia/przestrzeni, np. 'conference room A-304', 'living room', 'garden'."),
                ["room_type"] = Str("Typ/przeznaczenie WYWNIOSKOWANY z nazwy i rozmowy, np. 'office', 'apartment living room', 'classroom', 'garden'. NIE zakładaj szpitala."),
                ["space_kind"] = Str("'interior' (domyślnie) dla wnętrz, lub 'exterior'/'garden'/'yard'/'ogród'/'podwórze' dla przestrzeni zewnętrznych."),
                ["building_context"] = Str("Kontekst budynku TYLKO jeśli wynika z danych/rozmowy, np. 'office building', 'private apartment'. Pomiń, jeśli nieznany."),
                ["dimensions"] = Str("RZECZYWISTE wymiary z rysunku: długość x szerokość, powierzchnia, wysokość, np. '20.0 m x 10.0 m, 200 m², ceiling 3.2 m'."),
                ["windows"] = Str("Każde okno: na której ścianie (N/S/E/W), liczba, rozmiar, parapet — i wynikający kierunek światła dziennego."),
                ["doors"] = Str("Każde drzwi: ściana, szerokość, jedno/dwuskrzydłowe, kierunek otwierania."),
                ["furniture"] = Str("Wszystkie meble/sprzęt/obiekty w przestrzeni wg rysunku: rodzaj, rozmiar, liczba, rozmieszczenie (np. stół 4.8x1.2 m na środku, 12 krzeseł, ekran na ścianie N)."),
                ["finishes"] = Str("Wykończenia i oświetlenie: podłoga/nawierzchnia, ściany, sufit, oprawy."),
                ["viewpoint"] = Str("Perspektywa kamery, np. 'from the door corner looking toward the window wall'."),
                ["prompt"] = Str("Opcjonalne dodatkowe wskazówki stylistyczne (po angielsku)."),
            },
            ["required"] = new JsonArray { "room_label", "dimensions", "windows", "doors", "furniture" },
        });

    private static JsonObject Str(string description) => new() { ["type"] = "string", ["description"] = description };

    // ─────────── helpers ───────────

    private static string Trim(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }

    private static List<string> ParsePlanSteps(string planText)
    {
        var steps = new List<string>();
        if (string.IsNullOrWhiteSpace(planText)) return steps;
        foreach (var raw in planText.Split('\n'))
        {
            var line = raw.Trim();
            // Match "1. ...", "1) ...", "- 1. ...", "Krok 1: ..." etc.
            var m = Regex.Match(line, @"^(?:krok\s*)?[-*]?\s*(\d{1,2})[\.\):]\s+(.+)$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var text = m.Groups[2].Value.Trim();
                if (text.Length > 0) steps.Add(text);
            }
        }
        return steps;
    }

    private static string Summarize(ToolCall call)
    {
        if (string.Equals(call.Name, VisualizationTool, StringComparison.OrdinalIgnoreCase))
            return call.Arguments["title"]?.GetValue<string>() ?? "wizualizacja";
        if (string.Equals(call.Name, "acad_call", StringComparison.OrdinalIgnoreCase))
        {
            var cat = call.Arguments["category"]?.GetValue<string>();
            var tool = call.Arguments["tool"]?.GetValue<string>();
            return string.IsNullOrEmpty(cat) ? tool ?? "" : $"{cat}/{tool}";
        }
        var firstArg = string.Empty;
        foreach (var kv in call.Arguments)
        {
            firstArg = kv.Value is JsonValue v ? v.ToString() : "";
            break;
        }
        return firstArg;
    }

    private const string SystemPrompt =
        "Jesteś asystentem AI wbudowanym bezpośrednio w AutoCAD. Pomagasz użytkownikowi rysować, " +
        "modyfikować, inspekcjonować i analizować bieżący rysunek oraz tworzyć zestawienia, raporty i wizualizacje.\n\n" +
        "MASZ REALNY DOSTĘP do bieżącego rysunku przez wbudowane narzędzia — NIGDY nie zgaduj i nie mów, że " +
        "nie widzisz rysunku. Zamiast tego WYWOŁAJ narzędzie. Każde pytanie o zawartość rysunku zaczynaj od " +
        "wywołania narzędzia.\n\n" +
        "OBOWIĄZKOWY pierwszy krok dla pytań o rysunek: wywołaj acad_status (zwraca aktywny dokument, warstwę, " +
        "liczbę encji). Następnie, zależnie od pytania, wołaj narzędzia przez acad_call { tool, args }:\n" +
        "• Podsumowanie rysunku: acad_call { tool: \"acad.validators.doc_summary\" }\n" +
        "• Liczba encji / zliczanie: acad_call { tool: \"acad.selection.count_entities\", args: { ... } }\n" +
        "• Lista warstw: acad_call { tool: \"acad.layers.list_layers\" }\n" +
        "• Lista bloków (okna/drzwi/meble jako bloki): acad_call { tool: \"acad.blocks.list_blocks\" }\n" +
        "• Wstawione bloki w modelu: acad_call { tool: \"acad.blocks.extract_block_references\" }\n" +
        "• Meble w modelu: acad_call { tool: \"acad.furniture.list_furniture_in_model\" }\n" +
        "• Filtrowanie/typy encji: acad_call { tool: \"acad.selection.filter_entities\", args: { ... } }\n" +
        "• Layouty: acad_call { tool: \"acad.layouts.list_layouts\" }\n\n" +
        "Gdy nie znasz właściwego narzędzia: acad_find_tools { query }, a katalog kategorii: acad_load_category { category }. " +
        "Prymitywy mają nazwy z kropkami (np. acad.annotations.add_table) i wołasz je przez acad_call { tool, args } bez pola category.\n\n" +
        "UNIWERSALNOŚĆ — DZIAŁAJ NA WSZYSTKICH WARSTWACH:\n" +
        "Ten system NIE jest tylko dla szpitali. Przestrzeń to równie dobrze pokój, biuro, mieszkanie, klasa, hala, " +
        "ogród czy podwórze. Szukanie pomieszczeń, zliczanie obiektów i analiza projektu MUSZĄ obejmować WSZYSTKIE " +
        "warstwy — nigdy nie zakładaj konkretnych nazw warstw (A-ROOM-*, FURN-* itp.). Jeśli filtrujesz po warstwie i " +
        "nic nie znajdziesz, ponów BEZ filtra warstwy. Typ przestrzeni wnioskuj z jej nazwy i rozmowy.\n\n" +
        "DANE POMIESZCZENIA / SZUKANIE PO NUMERZE LUB NAZWIE:\n" +
        "Aby znaleźć przestrzeń po numerze lub nazwie (np. 'A-304', 'sala konferencyjna', 'pokój 12', 'ogród') i pobrać " +
        "jej dane, użyj JEDNEGO wywołania: acad_call { category: \"schedules\", tool: \"get_room_data\", args: { query: \"A-304\" } }. " +
        "To narzędzie domyślnie skanuje etykiety na WSZYSTKICH warstwach i samo wyznacza granicę pomieszczenia " +
        "uniwersalnym algorytmem (rastrowy flood-fill po ścianach, z uszczelnieniem otworów; fallback: promienie do " +
        "ścian / najmniejszy zamknięty obrys). Zwraca numer, nazwę, ZMIERZONĄ powierzchnię (areaM2), powierzchnię z " +
        "etykiety (labelAreaM2), sposób wyznaczenia granicy (method), wymiary (szer×głęb mm) oraz listy drzwi, okien " +
        "i obiektów w obrysie (z rozmiarami, ścianą N/S/E/W i pozycją). READ-ONLY. " +
        "NIE używaj filter_entities z 'textContains' (nie istnieje) ani generate_room_schedule do CZYTANIA danych.\n" +
        "RAPORTUJ użytkownikowi ZMIERZONĄ powierzchnię, a gdy różni się od etykietowej — podaj obie i zaznacz różnicę " +
        "(nie zakładaj z góry wartości z etykiety). Zawsze wypisz znalezione okna, drzwi i obiekty. Jeśli któraś lista " +
        "jest pusta lub pole 'note' mówi, że obszar jest otwarty/nieszczelny — napisz to wprost i zaproponuj korektę, " +
        "NIE zgaduj zawartości.\n" +
        "Gdy get_room_data nie znajdzie przestrzeni, możesz też zlokalizować ją wizualnie: zrób zrzut/zbliżenie " +
        "(kategoria 'view'/'vision') wokół etykiety, by potwierdzić położenie, a następnie ponów odczyt danych.\n\n" +
        "WIZUALIZACJE (KLUCZOWE — render MUSI odpowiadać rysunkowi, nie zgaduj):\n" +
        "Gdy użytkownik prosi o wizualizację / render konkretnej przestrzeni:\n" +
        "  1) Wywołaj get_room_data z numerem/nazwą, żeby dostać dokładne wymiary, okna, drzwi i obiekty.\n" +
        "  2) Wywołaj render_visualization, mapując te dane na pola: room_type/space_kind = wywnioskowany typ " +
        "(np. office, apartment living room, classroom, garden — NIE szpital, chyba że to wynika z danych); " +
        "dimensions = '{width} m x {depth} m, {area} m²'; windows = dla każdego okna ściana (N/S/E/W → kierunek " +
        "światła), szerokość; doors = ściana + szerokość; furniture = każdy obiekt z rozmiarem (z nazwy bloku, " +
        "np. FURN-...-2400-800 = 2400×800 mm) i rozmieszczeniem.\n" +
        "Wypełniaj pola DOKŁADNYMI wartościami z get_room_data, nie przybliżeniami. Jeśli get_room_data nie znalazło " +
        "przestrzeni lub brak obrysu — dopytaj użytkownika, nie zgaduj. Obraz pojawi się w czacie.\n\n" +
        "Przy operacjach zmieniających rysunek rozważ acad_undo_checkpoint, aby umożliwić cofnięcie. " +
        "Zliczanie/raporty prezentuj w tabeli Markdown. Gdy użytkownik dołączy pliki (obrazy/PDF), analizuj je razem z poleceniem.\n\n" +
        "PRACUJ PRZYROSTOWO — NIE WSZYSTKO NA RAZ:\n" +
        "Nie próbuj zmieścić ogromnego zadania w jednej odpowiedzi ani wywołać dziesiątek narzędzi naraz. " +
        "Rozbij pracę na małe porcje: wykonaj jeden logiczny krok (np. jedno odczytanie/jedną grupę zmian), " +
        "krótko podsumuj wynik, a potem przejdź dalej. Jeśli odpowiedź lub zestawienie jest długie, podaj kolejną " +
        "porcję w następnej turze zamiast generować wszystko jednym ciągiem. Przy wielu pomieszczeniach/elementach " +
        "przetwarzaj je partiami (np. po kilka), nie wszystkie w jednym wywołaniu. To utrzymuje responsywność czatu " +
        "i zapobiega przekroczeniu limitów.\n\n" +
        "Zasady: odpowiadaj w języku użytkownika (domyślnie polski), zwięźle i konkretnie, na podstawie WYNIKÓW narzędzi. " +
        "Nie ujawniaj wewnętrznej architektury, nazw bibliotek ani protokołów komunikacji — mów wyłącznie o " +
        "\"wbudowanych narzędziach AutoCAD\". Jeśli acad_status pokaże brak dokumentu, poproś o otwarcie rysunku.";

    private const string PlannerSystemPrompt =
        "Jesteś PLANISTĄ wbudowanym w AutoCAD. Twoim jedynym zadaniem jest stworzyć krótki, wykonalny PLAN działania " +
        "dla polecenia użytkownika — NIE wykonuj jeszcze zmian w rysunku. Możesz najpierw wywołać narzędzia tylko do " +
        "ODCZYTU (np. acad_status, acad.validators.doc_summary, acad.layers.list_layers), aby poznać stan rysunku.\n\n" +
        "Następnie zwróć plan jako ponumerowaną listę 3–8 konkretnych kroków, każdy w jednej linii w formacie:\n" +
        "1. <krok>\n2. <krok>\n... Każdy krok ma być pojedynczą, samodzielną operacją możliwą do wykonania narzędziami " +
        "AutoCAD. Nie dodawaj długich opisów — tylko zwięzłe kroki. Po liście nie pisz nic więcej.";
}
