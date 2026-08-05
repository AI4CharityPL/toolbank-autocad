// acad-router: the only AutoCAD-MCP server permanently registered in user's mcp.json.
// Exposes a small set of meta-tools that orchestrate discovery of, and high-level
// actions across, the rest of the categories. Uses in-process ToolRegistry so
// the router can directly dispatch backend composite tools (no subprocess spawn,
// no external MCPBank discovery needed).
//
// See section 1a of the plan and rule 00-architecture-invariants.md Invariant #6.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Backend.Sidecar;
using AcadMcp.Shared.Mcp;
using Microsoft.Extensions.Logging;

namespace AcadMcp.Backend.Mcp;

public sealed class RouterServer : ICategoryServer, IJsonRpcDispatcher
{
    private const int DefaultToolTimeoutMs = 30_000;
    private readonly ILogger<RouterServer> _logger;
    private readonly StartupOptions _options;
    private readonly IPluginGateway? _plugin;
    private readonly ToolRegistry _registry;
    private readonly IVisionSidecarClient? _vision;

    public RouterServer(
        ILogger<RouterServer> logger,
        StartupOptions options,
        ToolRegistry registry,
        IPluginGateway? plugin = null,
        IVisionSidecarClient? vision = null)
    {
        _logger = logger;
        _options = options;
        _plugin = plugin;
        _registry = registry;
        _vision = vision;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var host = new StdioJsonRpcHost(_logger, this);
        await host.RunAsync(ct).ConfigureAwait(false);
    }

    public Task<JsonObject?> DispatchAsync(JsonObject? request, CancellationToken ct)
    {
        if (request is null) return Task.FromResult<JsonObject?>(null);
        var id = request["id"];
        var method = request["method"]?.GetValue<string>();
        var prms = request["params"] as JsonObject;

        try
        {
            return method switch
            {
                McpMethods.Initialize => Task.FromResult<JsonObject?>(HandleInitialize(id)),
                McpMethods.Initialized => Task.FromResult<JsonObject?>(null),
                McpMethods.ToolsList => Task.FromResult<JsonObject?>(HandleToolsList(id)),
                McpMethods.ToolsCall => HandleToolsCallAsync(id, prms, ct),
                McpMethods.Ping => Task.FromResult<JsonObject?>(StdioJsonRpcHost.BuildResult(id, new JsonObject())),
                McpMethods.Shutdown => Task.FromResult<JsonObject?>(StdioJsonRpcHost.BuildResult(id, new JsonObject())),
                McpMethods.Exit => Task.FromResult<JsonObject?>(null),
                _ => Task.FromResult<JsonObject?>(StdioJsonRpcHost.BuildError(id, -32601, $"Method not found: {method}")),
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Router dispatch failed for {Method}", method);
            return Task.FromResult<JsonObject?>(StdioJsonRpcHost.BuildError(id, -32603, "Internal error: " + ex.Message));
        }
    }

    private JsonObject HandleInitialize(JsonNode? id)
        => StdioJsonRpcHost.BuildResult(id, new JsonObject
        {
            ["protocolVersion"] = McpProtocolVersion.Current,
            ["capabilities"] = new JsonObject { ["tools"] = new JsonObject { ["listChanged"] = false } },
            ["serverInfo"] = new JsonObject { ["name"] = "acad-router", ["version"] = "0.2.0" },
            ["instructions"] =
                "AutoCAD MCP router. Single entry point for ~30 specialist categories " +
                "(geometry, annotations, blocks, architecture, mechanical, civil, electrical, " +
                "vision, validators, schedules, openings, hatches, furniture, plumbing, grids, " +
                "verticals, sections, plotstyles, callouts, ...). Discovery: acad_recommend_categories -> " +
                "acad_find_tools -> acad_load_category. Invocation: acad_call { category, tool, args } " +
                "proxies ANY backend composite or plugin primitive in-process (no subprocess spawn). " +
                "acad_design_iterate closes the loop (checkpoint -> plan -> validate -> fix/rollback).",
        });

    private JsonObject HandleToolsList(JsonNode? id)
    {
        var tools = new JsonArray
        {
            BuildToolStub("acad_status",
                "Lightweight health-check: AutoCAD alive?, version, vertical, active document, layer, entity count, mode banner.",
                new JsonObject { ["type"] = "object", ["properties"] = new JsonObject(), ["additionalProperties"] = false }),
            BuildToolStub("acad_find_tools",
                "Semantic/keyword search across all in-process acad-* categories (backend composites + plugin primitives).",
                new JsonObject {
                    ["type"] = "object",
                    ["properties"] = new JsonObject {
                        ["query"] = new JsonObject { ["type"] = "string", ["description"] = "Natural language query, PL or EN" },
                        ["maxResults"] = new JsonObject { ["type"] = "integer", ["default"] = 10 } },
                    ["required"] = new JsonArray { "query" } }),
            BuildToolStub("acad_load_category",
                "Returns the full tool catalog for a single acad-<name> category: name, description, intent examples, input schema.",
                new JsonObject {
                    ["type"] = "object",
                    ["properties"] = new JsonObject {
                        ["category"] = new JsonObject { ["type"] = "string", ["description"] = "Category id without 'acad-' prefix, e.g. 'schedules' or 'geometry-2d'" },
                        ["includeSchema"] = new JsonObject { ["type"] = "boolean", ["default"] = true, ["description"] = "Include full input schema for each tool (set false for a compact listing)." } },
                    ["required"] = new JsonArray { "category" } }),
            BuildToolStub("acad_recommend_categories",
                "Suggest the 1-3 most relevant categories for a task (avoids loading all 30). Saves tokens.",
                new JsonObject {
                    ["type"] = "object",
                    ["properties"] = new JsonObject {
                        ["task"] = new JsonObject { ["type"] = "string", ["description"] = "Plain-language task description" } },
                    ["required"] = new JsonArray { "task" } }),
            BuildToolStub("acad_explain_capabilities",
                "Returns a compact catalog of all known categories with tool counts and one-line summaries.",
                new JsonObject { ["type"] = "object", ["properties"] = new JsonObject(), ["additionalProperties"] = false }),
            BuildToolStub("acad_call",
                "UNIVERSAL dispatch: invoke any backend composite (e.g. 'schedules/generate_door_schedule') OR any plugin primitive (e.g. tool='acad.annotations.add_table', category left empty). Routes in-process, no subprocess spawn.",
                new JsonObject {
                    ["type"] = "object",
                    ["properties"] = new JsonObject {
                        ["category"] = new JsonObject { ["type"] = "string", ["description"] = "Category id without 'acad-' prefix (e.g. 'schedules', 'openings'). Leave empty when invoking a plugin primitive via its dotted name." },
                        ["tool"] = new JsonObject { ["type"] = "string", ["description"] = "Tool name. Either a backend composite (e.g. 'generate_door_schedule') OR a plugin primitive dotted name (e.g. 'acad.annotations.add_table')." },
                        ["args"] = new JsonObject { ["type"] = "object", ["description"] = "Arguments for the tool (object)." } },
                    ["required"] = new JsonArray { "tool" } }),
            BuildToolStub("acad_describe_drawing",
                "Vision shortcut: screenshot active viewport + OCR + LLM-describe in one call. Phase 4 implementation.",
                new JsonObject { ["type"] = "object", ["properties"] = new JsonObject(), ["additionalProperties"] = false }),
            BuildToolStub("acad_undo_checkpoint",
                "Create a named undo checkpoint so subsequent operations can be rolled back.",
                new JsonObject {
                    ["type"] = "object",
                    ["properties"] = new JsonObject {
                        ["label"] = new JsonObject { ["type"] = "string" } },
                    ["required"] = new JsonArray { "label" } }),
            BuildToolStub("acad_restore_checkpoint",
                "Roll back to a previously created checkpoint.",
                new JsonObject {
                    ["type"] = "object",
                    ["properties"] = new JsonObject {
                        ["label"] = new JsonObject { ["type"] = "string" },
                        ["id"] = new JsonObject { ["type"] = "string" } } }),
            BuildToolStub("acad_design_iterate",
                "Auto-design loop: create a checkpoint, execute a planned sequence of tool calls, validate against a standard, auto-fix if fixable, otherwise roll back and return the report.",
                new JsonObject {
                    ["type"] = "object",
                    ["properties"] = new JsonObject {
                        ["task"] = new JsonObject { ["type"] = "string", ["description"] = "Human description of the design goal (used for logging only)." },
                        ["plan"] = new JsonObject {
                            ["type"] = "array",
                            ["description"] = "Ordered list of tool calls: [{category, tool, args}].",
                            ["items"] = new JsonObject {
                                ["type"] = "object",
                                ["properties"] = new JsonObject {
                                    ["category"] = new JsonObject { ["type"] = "string" },
                                    ["tool"] = new JsonObject { ["type"] = "string" },
                                    ["args"] = new JsonObject { ["type"] = "object" }
                                },
                                ["required"] = new JsonArray { "category", "tool" }
                            }
                        },
                        ["standardId"] = new JsonObject { ["type"] = "string", ["description"] = "Validator standard id, e.g. 'polish-arch-baseline'." },
                        ["maxIterations"] = new JsonObject { ["type"] = "integer", ["default"] = 3 },
                        ["checkpointLabel"] = new JsonObject { ["type"] = "string", ["description"] = "Optional explicit checkpoint label; default 'iter_<timestamp>'." }
                    },
                    ["required"] = new JsonArray { "task", "plan" } }),
        };
        return StdioJsonRpcHost.BuildResult(id, new JsonObject { ["tools"] = tools });
    }

    /// <summary>
    /// Prefix every router refusal carries. Load-bearing: the dispatcher maps it to MCP's
    /// isError, so a message that starts with this is an error by construction. See KNOWN-GAPS A6.
    /// </summary>
    internal const string RouterErrorMarker = "[router-error]";

    private async Task<JsonObject?> HandleToolsCallAsync(JsonNode? id, JsonObject? prms, CancellationToken ct)
    {
        var name = prms?["name"]?.GetValue<string>();
        var args = prms?["arguments"] as JsonObject ?? new JsonObject();

        string text;
        bool isError = false;
        try
        {
            switch (name)
            {
                case "acad_status":
                    text = await PluginStatusAsync(ct).ConfigureAwait(false);
                    break;
                case "acad_find_tools":
                    text = FindTools(args);
                    break;
                case "acad_load_category":
                    text = LoadCategory(args);
                    break;
                case "acad_recommend_categories":
                    text = RecommendCategories(args["task"]?.GetValue<string>() ?? "");
                    break;
                case "acad_explain_capabilities":
                    text = ExplainCapabilities();
                    break;
                case "acad_call":
                    {
                        var r = await AcadCallAsync(args, ct).ConfigureAwait(false);
                        text = r.Text;
                        isError = r.IsError;
                        break;
                    }
                case "acad_describe_drawing":
                    text = Stub("Vision pipeline not yet implemented (Phase 4).");
                    break;
                case "acad_undo_checkpoint":
                    text = await CheckpointCreateAsync(args, ct).ConfigureAwait(false);
                    break;
                case "acad_restore_checkpoint":
                    text = await CheckpointRestoreAsync(args, ct).ConfigureAwait(false);
                    break;
                case "acad_design_iterate":
                    text = await DesignIterateAsync(args, ct).ConfigureAwait(false);
                    break;
                default:
                    text = Stub($"Unknown router tool: {name}");
                    isError = true;
                    break;
            }
        }
        catch (PluginUnavailableException ex)
        {
            text = $"[router-error] AutoCAD plugin unreachable on pipe '{ex.PipeName}': {ex.Message}. " +
                   "Is AutoCAD running and has NETLOAD'd AcadMcp.Plugin.dll?";
            isError = true;
        }
        catch (PluginToolException ex)
        {
            text = $"[router-error] {ex.Message}";
            isError = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "router tool '{Name}' failed", name);
            text = $"[router-error] {name}: {ex.Message}";
            isError = true;
        }

        // KNOWN-GAPS A6. Every handler in the switch above except acad_call returns a plain
        // string, and several of them return a "[router-error] ..." message for a refusal. Those
        // arrived here with isError still false, so a client saw a SUCCESSFUL tool call whose
        // content happened to begin with a marker it would have to string-match to notice. That
        // is the exact failure shape this whole sweep has been removing, sitting in the one
        // category every agent talks to first.
        //
        // The marker is already the convention at a dozen sites. Making it load-bearing in
        // exactly ONE place means the text and the flag cannot disagree again, whatever a future
        // handler does: a refusal that says [router-error] is an error, by construction.
        if (!isError && text.StartsWith(RouterErrorMarker, StringComparison.Ordinal))
            isError = true;

        var contentArr = new JsonArray { new JsonObject { ["type"] = "text", ["text"] = text } };
        return StdioJsonRpcHost.BuildResult(id, new JsonObject
        {
            ["content"] = contentArr,
            ["isError"] = isError,
        });
    }

    // ─────────── acad_call — universal dispatch ───────────

    private async Task<ToolInvoker.InvokeResult> AcadCallAsync(JsonObject args, CancellationToken ct)
    {
        var tool = args["tool"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(tool))
            return new ToolInvoker.InvokeResult("[router-error] acad_call requires 'tool'.", IsError: true);

        var toolArgs = args["args"] as JsonObject ?? new JsonObject();

        // Plugin primitive? (dotted names like 'acad.annotations.add_table')
        if (tool.StartsWith("acad.", StringComparison.OrdinalIgnoreCase))
        {
            if (_plugin is null)
                return new ToolInvoker.InvokeResult("[router-error] plugin gateway not wired.", IsError: true);
            try
            {
                var resp = await _plugin.InvokeAsync(tool, toolArgs, DefaultToolTimeoutMs, ct).ConfigureAwait(false);
                var text = resp?.ToJsonString(JsonOpts.Pretty) ?? "(no result)";
                return new ToolInvoker.InvokeResult(text, IsError: false);
            }
            catch (PluginToolException ex)
            {
                return new ToolInvoker.InvokeResult($"[router-error] plugin '{tool}' -> {ex.Code}: {ex.Message}", IsError: true);
            }
        }

        // Backend composite. Category must be supplied.
        var category = args["category"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(category))
            return new ToolInvoker.InvokeResult(
                $"[router-error] acad_call: tool '{tool}' looks like a backend composite - 'category' is required.",
                IsError: true);

        if (!_registry.TryGetTool(category!, tool!, out var meta) || meta is null)
        {
            var known = string.Join(", ", _registry.Categories.OrderBy(x => x));
            return new ToolInvoker.InvokeResult(
                $"[router-error] tool '{tool}' not found in category '{category}'. Known categories: {known}.",
                IsError: true);
        }

        var method = _registry.ResolveMethod(meta);
        if (method is null)
            return new ToolInvoker.InvokeResult(
                $"[router-error] tool '{tool}' has no resolvable method.",
                IsError: true);

        return await ToolInvoker.InvokeAsync(_logger, meta, method, toolArgs, _plugin, _vision, ct).ConfigureAwait(false);
    }

    // ─────────── acad_find_tools — search over ToolRegistry ───────────

    private string FindTools(JsonObject args)
    {
        var query = (args["query"]?.GetValue<string>() ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(query))
            return "[router] acad_find_tools: 'query' is required.";

        int maxResults = 10;
        if (args["maxResults"]?.AsValue() is JsonValue mv && mv.TryGetValue<int>(out var mr) && mr > 0) maxResults = mr;

        var terms = query.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hits = new List<(int score, string cat, McpToolMetadata meta)>();
        foreach (var cat in _registry.Categories)
        {
            foreach (var t in _registry.ToolsFor(cat))
            {
                int score = 0;
                var hay = string.Join(' ', new[] {
                    t.Name, t.Description ?? "",
                    t.Intent.Count > 0 ? string.Join(' ', t.Intent) : "",
                }).ToLowerInvariant();

                foreach (var term in terms)
                {
                    if (hay.Contains(term)) score++;
                    if (t.Name.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 2;
                }
                if (score > 0) hits.Add((score, cat, t));
            }
        }

        if (hits.Count == 0)
            return $"[router] acad_find_tools: no matches for '{query}'. Try acad_explain_capabilities.";

        var top = hits.OrderByDescending(x => x.score).Take(maxResults)
            .Select(x => new JsonObject
            {
                ["category"] = x.cat,
                ["tool"] = x.meta.Name,
                ["description"] = x.meta.Description ?? "",
                ["readOnly"] = x.meta.ReadOnly,
                ["requiresPlugin"] = x.meta.RequiresPlugin,
                ["score"] = x.score,
            });

        var arr = new JsonArray();
        foreach (var n in top) arr.Add(n);
        return new JsonObject { ["query"] = query, ["hits"] = arr }.ToJsonString(JsonOpts.Pretty);
    }

    // ─────────── acad_load_category — full tool catalog ───────────

    private string LoadCategory(JsonObject args)
    {
        var cat = args["category"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(cat))
            return "[router] acad_load_category: 'category' is required.";

        bool includeSchema = true;
        if (args["includeSchema"]?.AsValue() is JsonValue sv && sv.TryGetValue<bool>(out var s)) includeSchema = s;

        var tools = _registry.ToolsFor(cat!);
        if (tools.Count == 0)
        {
            var known = string.Join(", ", _registry.Categories.OrderBy(x => x));
            return $"[router] acad_load_category: no tools for '{cat}'. Known categories: {known}.";
        }

        var arr = new JsonArray();
        foreach (var t in tools)
        {
            var node = new JsonObject
            {
                ["name"] = t.Name,
                ["description"] = t.Description ?? "",
                ["readOnly"] = t.ReadOnly,
                ["requiresPlugin"] = t.RequiresPlugin,
            };
            if (t.Intent.Count > 0)
            {
                var iArr = new JsonArray();
                foreach (var i in t.Intent) iArr.Add((JsonNode?)JsonValue.Create(i));
                node["intent"] = iArr;
            }
            if (includeSchema)
            {
                // Same builder as CategoryServer's tools/list. These two used to construct
                // the schema independently, which is how they could have drifted apart.
                node["inputSchema"] = JsonSchemaBuilder.BuildToolSchema(t);
            }
            arr.Add(node);
        }

        return new JsonObject
        {
            ["category"] = cat,
            ["toolCount"] = tools.Count,
            ["tools"] = arr,
            ["invocation"] = $"Call via acad_call {{ category: '{cat}', tool: '<name>', args: {{ ... }} }}",
        }.ToJsonString(JsonOpts.Pretty);
    }

    private static string ClrToJsonType(Type t) => t switch
    {
        _ when t == typeof(string) => "string",
        _ when t == typeof(int) || t == typeof(long) => "integer",
        _ when t == typeof(double) || t == typeof(float) || t == typeof(decimal) => "number",
        _ when t == typeof(bool) => "boolean",
        _ when t.IsArray => "array",
        _ => "object",
    };

    // ─────────── plugin-backed meta tool implementations (Phase 7.0) ───────────

    private async Task<string> PluginStatusAsync(CancellationToken ct)
    {
        if (_plugin is null) return Stub("acad_status: plugin gateway not wired (router DI incomplete).");
        var result = await _plugin.InvokeAsync("acad_status", new JsonObject(), DefaultToolTimeoutMs, ct).ConfigureAwait(false);
        return result?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "{}";
    }

    private async Task<string> CheckpointCreateAsync(JsonObject args, CancellationToken ct)
    {
        if (_plugin is null) return Stub("acad_undo_checkpoint: plugin gateway not wired.");

        // KNOWN-GAPS A7. The schema above declares `label` REQUIRED, and this used to accept its
        // absence, creating a checkpoint labelled "(none)". A caller who mistyped the argument -
        // `name` instead of `label`, say - got a checkpoint back and then could not find it by
        // label, because the label they thought they set was never read. Advertised as required
        // and treated as optional is the same catalogue-versus-consumer disagreement the property
        // catalogues exist to prevent, only here between a schema and its own handler.
        var label = args["label"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(label))
        {
            var passed = args.Select(kv => kv.Key).Where(k => k != "fileSnapshot").ToList();
            var sawSomethingElse = passed.Count > 0
                ? " Received instead: " + string.Join(", ", passed) + "."
                : "";
            return RouterErrorMarker +
                   " acad_undo_checkpoint requires 'label' - it is declared required in this " +
                   "tool's schema, and a checkpoint you cannot name is one you cannot restore by " +
                   "name later." + sawSomethingElse;
        }

        var req = new JsonObject { ["label"] = label };
        if (args["fileSnapshot"] is JsonValue jv && jv.TryGetValue<bool>(out var snap)) req["fileSnapshot"] = snap;

        var result = await _plugin.InvokeAsync("acad.checkpoint.create", req, DefaultToolTimeoutMs, ct).ConfigureAwait(false);
        if (result is JsonObject obj)
        {
            var cid = obj["id"]?.GetValue<string>() ?? "<unknown>";
            var depth = obj["stackDepth"]?.GetValue<int>() ?? -1;
            return $"[router] checkpoint created id='{cid}' label='{label}' stack_depth={depth}";
        }
        return "[router] checkpoint created (plugin returned no body).";
    }

    private async Task<string> DesignIterateAsync(JsonObject args, CancellationToken ct)
    {
        if (_plugin is null) return Stub("acad_design_iterate: plugin gateway not wired.");
        var req = JsonSerializer.Deserialize<DesignIterateRequest>(args, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });
        if (req is null) return "[router-error] acad_design_iterate: cannot deserialize request.";
        if (string.IsNullOrWhiteSpace(req.Task) || req.Plan is null || req.Plan.Count == 0)
            return "[router-error] acad_design_iterate: 'task' and non-empty 'plan' are required.";

        var iterator = new DesignIterator(_logger, _plugin);
        var outcome = await iterator.RunAsync(req, ct).ConfigureAwait(false);
        return JsonSerializer.Serialize(outcome, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });
    }

    private async Task<string> CheckpointRestoreAsync(JsonObject args, CancellationToken ct)
    {
        if (_plugin is null) return Stub("acad_restore_checkpoint: plugin gateway not wired.");
        var label = args["label"]?.GetValue<string>();
        var idArg = args["id"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(idArg))
            return "[router-error] acad_restore_checkpoint requires 'id' or 'label'.";

        var req = new JsonObject();
        if (!string.IsNullOrWhiteSpace(idArg)) req["id"] = idArg;
        if (!string.IsNullOrWhiteSpace(label)) req["label"] = label;

        var result = await _plugin.InvokeAsync("acad.checkpoint.restore", req, DefaultToolTimeoutMs, ct).ConfigureAwait(false);
        if (result is JsonObject obj)
        {
            var strategy = obj["strategy"]?.GetValue<string>() ?? "?";
            var steps = obj["undoStepsIssued"]?.GetValue<int>() ?? 0;
            var msg = obj["message"]?.GetValue<string>() ?? "";
            return $"[router] restore strategy={strategy} undo_steps={steps}. {msg}";
        }
        return "[router] restore completed (plugin returned no body).";
    }

    private static string Stub(string msg) => "[router-stub] " + msg;

    private string RecommendCategories(string task)
    {
        var t = task.ToLowerInvariant();
        var picks = new List<string>();

        void AddIf(string cat, params string[] keywords)
        {
            foreach (var k in keywords)
                if (t.Contains(k)) { picks.Add(cat); return; }
        }

        AddIf("geometry-2d", "line", "polyline", "circle", "arc", "ellipse", "linia", "okrag", "luk", "polilinia", "rysuj");
        AddIf("geometry-3d", "box", "sphere", "cylinder", "extrude", "solid", "bryla", "wycia", "wytloczenie", "3d");
        AddIf("modify", "move", "rotate", "scale", "mirror", "offset", "trim", "extend", "przesun", "obroc", "skaluj", "lustro");
        AddIf("layers", "layer", "warstwa", "warstw");
        AddIf("blocks", "block", "xref", "blok", "odnos");
        AddIf("annotations", "text", "mtext", "leader", "tekst", "opis", "wyno", "table", "tabel", "tabela");
        AddIf("dimensions", "dimension", "wymiar");
        AddIf("hatches", "hatch", "kreskowanie");
        AddIf("architecture", "wall", "floor plan", "rzut", "scian", "pietro");
        AddIf("openings", "door", "window", "drzwi", "okno", "otwor");
        AddIf("schedules", "schedule", "zestawienie", "stolark", "legenda", "wykaz");
        AddIf("furniture", "furniture", "bed", "table", "meble", "lozko", "stol");
        AddIf("plumbing", "toilet", "sink", "bathroom", "lazienka", "umywalka", "wc", "sanitar");
        AddIf("grids", "grid", "axis", "os konstrukcyjna", "siatka");
        AddIf("verticals", "stair", "lift", "elevator", "klatka", "schody", "winda");
        AddIf("mechanical", "bolt", "bearing", "gear", "weld", "tolerance", "sruba", "lozysko", "kolo zebate", "spaw", "tolerancja");
        AddIf("civil", "alignment", "profile", "corridor", "surface", "drogi", "teren");
        AddIf("electrical", "wire", "cable", "panel", "schemat", "obwod", "instalacja elektryczna");
        AddIf("vision", "screenshot", "ocr", "describe", "zdjecie", "rozpoznaj", "opisz");
        AddIf("validators", "validate", "check", "norm", "compliance", "waliduj", "spraw", "zgodno");
        AddIf("files", "save", "open", "export", "import", "pdf", "dxf", "zapisz", "otworz", "eksport");

        if (picks.Count == 0)
        {
            return "[router] No specific category match. Try acad_explain_capabilities and pick manually, " +
                   "or call acad_find_tools with the task as query.";
        }
        return "[router] Recommended categories: " + string.Join(", ", picks.Distinct())
               + ". Load with acad_load_category, invoke with acad_call { category, tool, args }.";
    }

    private string ExplainCapabilities()
    {
        // Build a catalog from actual loaded categories so drift between text and
        // real registry is impossible.
        var cats = _registry.Categories.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        var lines = new List<string> { $"AutoCAD MCP categories ({cats.Count} loaded in-process):" };
        foreach (var c in cats)
        {
            var count = _registry.ToolsFor(c).Count;
            lines.Add($"  acad-{c,-14} ({count} tools)");
        }
        lines.Add("");
        lines.Add("Discovery: acad_recommend_categories -> acad_find_tools -> acad_load_category.");
        lines.Add("Invocation: acad_call { category, tool, args }. Plugin primitives: acad_call { tool: 'acad.<cat>.<name>', args }.");
        return string.Join('\n', lines);
    }

    private static JsonObject BuildToolStub(string name, string description, JsonObject inputSchema)
        => new()
        {
            ["name"] = name,
            ["description"] = description,
            ["inputSchema"] = inputSchema,
        };
}
