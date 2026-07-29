using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using AcadMcp.Companion.Agent;
using AcadMcp.Companion.Agent.Reports;
using AcadMcp.Companion.Agent.Settings;
using AcadMcp.Companion.Host.Mvvm;
using AcadMcp.Companion.Mcp;
using Microsoft.Win32;

namespace AcadMcp.Companion.Host.ViewModels;

/// <summary>Drives the chat palette: conversation, attachments, settings and BYOK key entry.</summary>
public sealed class ChatViewModel : ViewModelBase
{
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    private readonly Dispatcher _dispatcher;
    private readonly CompanionSettings _settings;
    private readonly List<AttachmentVm> _pending = new();

    private McpStdioClient? _mcp;
    private AgentOrchestrator? _agent;
    private ProviderKind _agentProvider;
    private CancellationTokenSource? _cts;

    private string _inputText = string.Empty;
    private string _statusText = "Gotowe. Otwórz rysunek w AutoCAD i zadaj pytanie.";
    private bool _isBusy;
    private int _selectedProviderIndex;
    private string _modelText = string.Empty;
    private string _apiKeyStatus = string.Empty;

    public ChatViewModel(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _settings = CompanionSettings.Load();
        _selectedProviderIndex = (int)_settings.Provider;
        _agentProvider = _settings.Provider;
        _modelText = _settings.ModelFor(_settings.Provider);

        Messages = new ObservableCollection<MessageBubble>();
        Attachments = new ObservableCollection<AttachmentVm>();
        ProviderOptions = new ObservableCollection<string>
        {
            ProviderFactory.DisplayName(ProviderKind.OpenAI),
            ProviderFactory.DisplayName(ProviderKind.Anthropic),
            ProviderFactory.DisplayName(ProviderKind.Gemini),
        };
        AvailableModels = new ObservableCollection<string>(ModelCatalog.Fallback(_settings.Provider));
        Reports = new ObservableCollection<ReportTemplate>(ReportTemplates.All);

        SendCommand = new RelayCommand(_ => _ = SendAsync(InputText), _ => !IsBusy);
        CancelCommand = new RelayCommand(_ => _cts?.Cancel(), _ => IsBusy);
        AttachCommand = new RelayCommand(_ => AttachFiles(), _ => !IsBusy);
        ResetCommand = new RelayCommand(_ => ResetConversation(), _ => !IsBusy);
        SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
        RunReportCommand = new RelayCommand(p => { if (p is ReportTemplate t) _ = SendAsync(t.Prompt); }, _ => !IsBusy);
        ExportCsvCommand = new RelayCommand(p => { if (p is MessageBubble b) ExportCsv(b); });

        UpdateApiKeyStatus();
        AddIntro();
        _ = RefreshModelsAsync();
    }

    // ─────────── bound collections ───────────

    public ObservableCollection<MessageBubble> Messages { get; }
    public ObservableCollection<AttachmentVm> Attachments { get; }
    public ObservableCollection<string> ProviderOptions { get; }
    public ObservableCollection<string> AvailableModels { get; }
    public ObservableCollection<ReportTemplate> Reports { get; }

    // ─────────── bound scalars ───────────

    public string InputText { get => _inputText; set => Set(ref _inputText, value); }
    public string StatusText { get => _statusText; set => Set(ref _statusText, value); }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (Set(ref _isBusy, value))
            {
                Raise(nameof(IsNotBusy));
                (SendCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CancelCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AttachCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ResetCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RunReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !_isBusy;

    public int SelectedProviderIndex
    {
        get => _selectedProviderIndex;
        set
        {
            if (Set(ref _selectedProviderIndex, value))
            {
                ModelText = _settings.ModelFor(SelectedProvider);
                UpdateApiKeyStatus();
                _ = RefreshModelsAsync();
            }
        }
    }

    public ProviderKind SelectedProvider => (ProviderKind)_selectedProviderIndex;

    public string ModelText { get => _modelText; set => Set(ref _modelText, value); }
    public string ApiKeyStatus { get => _apiKeyStatus; set => Set(ref _apiKeyStatus, value); }

    public int MaxTokens
    {
        get => _settings.MaxTokens;
        set { _settings.MaxTokens = value; Raise(); }
    }

    public double Temperature
    {
        get => _settings.Temperature;
        set { _settings.Temperature = value; Raise(); }
    }

    public int MaxToolIterations
    {
        get => _settings.MaxToolIterations;
        set { _settings.MaxToolIterations = value; Raise(); }
    }

    /// <summary>When on, the agent plans first then executes the plan step-by-step (planner/executor style).</summary>
    public bool PlanMode
    {
        get => _settings.PlanMode;
        set
        {
            if (_settings.PlanMode != value)
            {
                _settings.PlanMode = value;
                _settings.Save();
                Raise();
                StatusText = value
                    ? "Tryb planowania: agent najpierw zaplanuje, potem wykona krok po kroku."
                    : "Tryb pojedynczego agenta.";
            }
        }
    }

    public string PipeName
    {
        get => _settings.PipeName;
        set { _settings.PipeName = value; Raise(); }
    }

    // ─────────── commands ───────────

    public ICommand SendCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand RunReportCommand { get; }
    public ICommand ExportCsvCommand { get; }

    /// <summary>Persists the API key for the currently selected provider (called from code-behind).</summary>
    public void SaveApiKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) { ApiKeyStatus = "⚠ Wpisz klucz API."; return; }
        SecureKeyStore.SaveKey(SelectedProvider, key.Trim());
        // Force the agent to rebuild with the new key on next send.
        _agent = null;
        ApiKeyStatus = $"✓ Klucz {ProviderFactory.DisplayName(SelectedProvider)} zapisany (zaszyfrowany lokalnie).";
        StatusText = "Klucz API zapisany. Pobieram listę modeli...";
        _ = RefreshModelsAsync();
    }

    /// <summary>True while the model list is being fetched from the provider.</summary>
    private bool _isLoadingModels;
    public bool IsLoadingModels { get => _isLoadingModels; private set => Set(ref _isLoadingModels, value); }

    /// <summary>
    /// Refreshes <see cref="AvailableModels"/> from the selected provider using the saved key.
    /// Always ends with a usable list (curated fallback if the API is unreachable).
    /// </summary>
    public async Task RefreshModelsAsync()
    {
        var provider = SelectedProvider;
        var key = SecureKeyStore.LoadKey(provider);
        IsLoadingModels = true;
        // The shared HttpClient has an infinite timeout (needed for long streamed chat turns), so
        // bound this startup/provider-switch call explicitly — otherwise a network stall freezes the panel.
        using var modelCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        try
        {
            var models = await ModelCatalog.ListAsync(provider, key, Http, modelCts.Token).ConfigureAwait(true);
            if (provider != SelectedProvider) return; // user switched while we were loading
            AvailableModels.Clear();
            foreach (var m in models) AvailableModels.Add(m);

            // Keep current selection if still valid; else pick the saved/default model.
            var current = _settings.ModelFor(provider);
            if (!AvailableModels.Contains(current) && AvailableModels.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(ModelText) || !AvailableModels.Contains(ModelText))
                    ModelText = AvailableModels[0];
            }
            else if (string.IsNullOrWhiteSpace(ModelText))
            {
                ModelText = current;
            }

            StatusText = string.IsNullOrWhiteSpace(key)
                ? $"Modele: lista domyślna ({AvailableModels.Count}). Wpisz klucz, aby pobrać aktualną z konta."
                : $"Załadowano {AvailableModels.Count} modeli {ProviderFactory.DisplayName(provider)}.";
        }
        catch (Exception ex)
        {
            CompanionLog.Error("RefreshModelsAsync failed", ex);
        }
        finally
        {
            IsLoadingModels = false;
        }
    }

    public void ClearApiKey()
    {
        SecureKeyStore.DeleteKey(SelectedProvider);
        _agent = null;
        UpdateApiKeyStatus();
    }

    private void SaveSettings()
    {
        _settings.Provider = SelectedProvider;
        _settings.Models[SelectedProvider] = string.IsNullOrWhiteSpace(ModelText)
            ? CompanionSettings.DefaultModel(SelectedProvider)
            : ModelText.Trim();
        _settings.Save();
        // Provider/model may have changed -> drop cached agent and connection.
        _agent = null;
        StatusText = "Ustawienia zapisane.";
    }

    // ─────────── core send loop ───────────

    private async Task SendAsync(string text)
    {
        if (IsBusy) return;
        text = (text ?? string.Empty).Trim();
        if (text.Length == 0 && _pending.Count == 0) return;

        var userParts = new List<ContentPart>();
        var bubbleText = text;
        foreach (var a in _pending)
        {
            userParts.Add(a.Part);
            bubbleText += (bubbleText.Length > 0 ? "\n" : "") + $"[plik: {a.FileName}]";
        }
        if (text.Length > 0) userParts.Insert(0, ContentPart.FromText(text));

        Messages.Add(new MessageBubble(BubbleRole.User, bubbleText));
        InputText = string.Empty;
        Attachments.Clear();
        _pending.Clear();

        // The assistant answer bubble is created lazily by the observer so it always renders
        // BELOW the (collapsible) tool-call group rather than above it.
        DispatcherObserver? observer = null;

        IsBusy = true;
        _cts = new CancellationTokenSource();
        try
        {
            await EnsureReadyAsync(_cts.Token).ConfigureAwait(true);
            observer = new DispatcherObserver(_dispatcher, b => Messages.Add(b), MoveToEnd, s => StatusText = s);
            var final = await _agent!.SendAsync(userParts, observer, _cts.Token).ConfigureAwait(true);
            observer.SetFinalText(final);
            StatusText = "Gotowe.";
        }
        catch (OperationCanceledException)
        {
            observer?.AppendToAnswer("\n\n(przerwano)");
            StatusText = "Przerwano.";
        }
        catch (Exception ex)
        {
            Messages.Add(new MessageBubble(BubbleRole.Error, ex.Message));
            StatusText = "Wystąpił błąd.";
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            IsBusy = false;
        }
    }

    /// <summary>Moves an existing bubble to the bottom of the list (keeps the answer below tools).</summary>
    private void MoveToEnd(MessageBubble bubble)
    {
        int idx = Messages.IndexOf(bubble);
        if (idx >= 0 && idx != Messages.Count - 1) Messages.Move(idx, Messages.Count - 1);
    }

    private async Task EnsureReadyAsync(CancellationToken ct)
    {
        if (_mcp is null || !_mcp.IsConnected)
        {
            StatusText = "Łączę z narzędziami AutoCAD...";
            if (_mcp is not null) await _mcp.DisposeAsync().ConfigureAwait(true);
            _mcp = new McpStdioClient(new McpClientOptions
            {
                PipeName = _settings.PipeName,
                Log = CompanionLog.Info,
            });
            await _mcp.ConnectAsync(ct).ConfigureAwait(true);
            CompanionLog.Info($"MCP connected: {_mcp.Tools.Count} tools available");
        }

        var key = SecureKeyStore.LoadKey(SelectedProvider);
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                $"Brak klucza API dla {ProviderFactory.DisplayName(SelectedProvider)}. Wpisz klucz w zakładce Ustawienia.");

        if (_agent is null || _agentProvider != SelectedProvider)
        {
            _settings.Models[SelectedProvider] = string.IsNullOrWhiteSpace(ModelText)
                ? CompanionSettings.DefaultModel(SelectedProvider)
                : ModelText.Trim();
            var provider = ProviderFactory.Create(SelectedProvider, key!, Http);
            _agent = new AgentOrchestrator(provider, _mcp, _settings, CompanionLog.Info);
            _agentProvider = SelectedProvider;
        }
    }

    // ─────────── attachments ───────────

    private void AttachFiles()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Dołącz pliki do czatu",
            Multiselect = true,
            Filter = "Obsługiwane pliki|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.pdf;*.txt;*.md;*.csv|Wszystkie pliki|*.*",
        };
        if (dlg.ShowDialog() != true) return;

        foreach (var path in dlg.FileNames)
        {
            try
            {
                var part = BuildPart(path);
                if (part is null) continue;
                var vm = new AttachmentVm(Path.GetFileName(path), part);
                _pending.Add(vm);
                Attachments.Add(vm);
            }
            catch (Exception ex)
            {
                Messages.Add(new MessageBubble(BubbleRole.Error, $"Nie udało się dołączyć {Path.GetFileName(path)}: {ex.Message}"));
            }
        }
    }

    private static ContentPart? BuildPart(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        switch (ext)
        {
            case ".png": return ContentPart.FromImage(File.ReadAllBytes(path), "image/png", Path.GetFileName(path));
            case ".jpg":
            case ".jpeg": return ContentPart.FromImage(File.ReadAllBytes(path), "image/jpeg", Path.GetFileName(path));
            case ".gif": return ContentPart.FromImage(File.ReadAllBytes(path), "image/gif", Path.GetFileName(path));
            case ".webp": return ContentPart.FromImage(File.ReadAllBytes(path), "image/webp", Path.GetFileName(path));
            case ".pdf": return ContentPart.FromDocument(File.ReadAllBytes(path), "application/pdf", Path.GetFileName(path));
            case ".txt":
            case ".md":
            case ".csv":
                var content = File.ReadAllText(path);
                return ContentPart.FromText($"Zawartość pliku {Path.GetFileName(path)}:\n\n{content}");
            default:
                return null;
        }
    }

    // ─────────── reports / export ───────────

    private void ExportCsv(MessageBubble bubble)
    {
        var csv = MarkdownTable.ToCsv(bubble.Text);
        if (csv is null) { StatusText = "Brak tabeli do eksportu w tej wiadomości."; return; }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Zapisz raport jako CSV",
            Filter = "Plik CSV|*.csv",
            FileName = "raport.csv",
        };
        if (dlg.ShowDialog() != true) return;
        File.WriteAllText(dlg.FileName, csv, new System.Text.UTF8Encoding(true));
        StatusText = $"Zapisano: {dlg.FileName}";
    }

    // ─────────── helpers ───────────

    private void ResetConversation()
    {
        _agent?.Reset();
        Messages.Clear();
        Attachments.Clear();
        _pending.Clear();
        AddIntro();
        StatusText = "Rozpoczęto nową rozmowę.";
    }

    private void UpdateApiKeyStatus()
        => ApiKeyStatus = SecureKeyStore.HasKey(SelectedProvider)
            ? "Klucz API zapisany (zaszyfrowany lokalnie)."
            : "Brak zapisanego klucza API.";

    private void AddIntro()
        => Messages.Add(new MessageBubble(BubbleRole.Assistant,
            "Cześć! Jestem asystentem AI wbudowanym w AutoCAD. Mogę rysować, modyfikować, " +
            "zliczać elementy i tworzyć zestawienia w bieżącym rysunku. Możesz też dołączyć obraz lub PDF. " +
            "Aby zacząć, wybierz dostawcę i wpisz swój klucz API w zakładce Ustawienia."));

    public async Task ShutdownAsync()
    {
        _cts?.Cancel();
        if (_mcp is not null) await _mcp.DisposeAsync().ConfigureAwait(false);
    }
}
