using System;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AcadMcp.Companion.Agent;

namespace AcadMcp.Companion.Host.ViewModels;

/// <summary>
/// Bridges <see cref="IAgentObserver"/> callbacks (raised on background threads) onto the
/// WPF dispatcher, streaming text into the active assistant bubble and surfacing tool activity.
/// </summary>
public sealed class DispatcherObserver : IAgentObserver
{
    private readonly Dispatcher _dispatcher;
    private readonly Action<MessageBubble> _addBubble;
    private readonly Action<MessageBubble> _moveToEnd;
    private readonly Action<string> _setStatus;

    private MessageBubble? _assistant;
    private MessageBubble? _toolGroup;
    private ToolEntry? _currentEntry;

    public DispatcherObserver(
        Dispatcher dispatcher,
        Action<MessageBubble> addBubble,
        Action<MessageBubble> moveToEnd,
        Action<string> setStatus)
    {
        _dispatcher = dispatcher;
        _addBubble = addBubble;
        _moveToEnd = moveToEnd;
        _setStatus = setStatus;
    }

    public void OnTextDelta(string delta)
        => _dispatcher.Invoke(() => EnsureAssistant().Append(delta));

    public void OnToolStarted(string toolName, string summary)
        => _dispatcher.Invoke(() =>
        {
            // Tools accumulate in a single collapsible group; the answer stays below them.
            if (_toolGroup is null)
            {
                _toolGroup = new MessageBubble(BubbleRole.ToolGroup);
                _addBubble(_toolGroup);
            }
            _currentEntry = _toolGroup.AddTool(Friendly(toolName, summary));
            // Keep any already-started answer bubble visually below the (growing) tool group.
            if (_assistant is not null) _moveToEnd(_assistant);
        });

    public void OnToolCompleted(string toolName, bool isError)
        => _dispatcher.Invoke(() =>
        {
            if (_currentEntry is not null) _currentEntry.Status = isError ? "✗" : "✓";
            _currentEntry = null;
        });

    public void OnStatus(string status) => _dispatcher.Invoke(() => _setStatus(status));

    public void OnImage(byte[] bytes, string mediaType, string caption)
        => _dispatcher.Invoke(() =>
        {
            var bubble = new MessageBubble(BubbleRole.Assistant, caption) { Image = DecodeImage(bytes) };
            _addBubble(bubble);
        });

    public void OnPlanUpdate(string text)
        => _dispatcher.Invoke(() => _addBubble(new MessageBubble(BubbleRole.Plan, text)));

    public void OnSectionBreak()
        => _dispatcher.Invoke(() =>
        {
            _assistant = null;
            _toolGroup = null;
            _currentEntry = null;
        });

    /// <summary>Final answer text (used by the caller if the model never streamed any deltas).</summary>
    public void SetFinalText(string finalText)
        => _dispatcher.Invoke(() =>
        {
            if (string.IsNullOrEmpty(finalText)) return;
            var bubble = EnsureAssistant();
            // Always surface the orchestrator's final string — in plan mode the step loops may have
            // already streamed partial text into an earlier bubble, but the synthesis pass targets
            // a fresh section started via OnSectionBreak().
            if (string.IsNullOrEmpty(bubble.Text))
                bubble.Text = finalText;
            else if (!bubble.Text.Contains(finalText, StringComparison.Ordinal))
                bubble.Append("\n\n" + finalText);
        });

    public void AppendToAnswer(string text)
        => _dispatcher.Invoke(() => EnsureAssistant().Append(text));

    private MessageBubble EnsureAssistant()
    {
        if (_assistant is null)
        {
            _assistant = new MessageBubble(BubbleRole.Assistant);
            _addBubble(_assistant); // added after any tool group -> renders below it
        }
        return _assistant;
    }

    private static BitmapImage DecodeImage(byte[] bytes)
    {
        var img = new BitmapImage();
        using var ms = new MemoryStream(bytes);
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = ms;
        img.EndInit();
        img.Freeze();
        return img;
    }

    private static string Friendly(string toolName, string summary) => toolName switch
    {
        "acad_status" => "Sprawdzam stan AutoCAD",
        "acad_recommend_categories" => "Dobieram narzędzia",
        "acad_find_tools" => "Szukam narzędzi",
        "acad_load_category" => "Ładuję katalog narzędzi",
        "acad_explain_capabilities" => "Sprawdzam możliwości",
        "acad_call" => string.IsNullOrEmpty(summary) ? "Wykonuję operację" : $"Wykonuję: {summary}",
        "acad_undo_checkpoint" => "Tworzę punkt cofania",
        "acad_restore_checkpoint" => "Przywracam punkt cofania",
        "acad_design_iterate" => "Iteracja projektowa",
        _ => toolName,
    };
}
