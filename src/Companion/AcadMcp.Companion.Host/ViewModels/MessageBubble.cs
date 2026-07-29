using System.Collections.ObjectModel;
using System.Windows.Media;
using AcadMcp.Companion.Host.Mvvm;

namespace AcadMcp.Companion.Host.ViewModels;

/// <summary>Visual role of a chat bubble (drives styling/alignment in the view).</summary>
public enum BubbleRole
{
    User,
    Assistant,
    Tool,
    Error,
    Plan,
    ToolGroup,
}

/// <summary>One tool invocation inside a collapsible <see cref="BubbleRole.ToolGroup"/> bubble.</summary>
public sealed class ToolEntry : ViewModelBase
{
    private string _status = "…";
    public ToolEntry(string title) => Title = title;
    public string Title { get; }
    public string Status { get => _status; set => Set(ref _status, value); }
}

/// <summary>A single rendered chat entry (text and/or an inline image visualization).</summary>
public sealed class MessageBubble : ViewModelBase
{
    private string _text = string.Empty;
    private ImageSource? _image;

    public MessageBubble(BubbleRole role, string text = "")
    {
        Role = role;
        _text = text;
    }

    public BubbleRole Role { get; }

    public string Text
    {
        get => _text;
        set => Set(ref _text, value);
    }

    /// <summary>Optional inline image (AI-generated room visualization).</summary>
    public ImageSource? Image
    {
        get => _image;
        set { if (Set(ref _image, value)) Raise(nameof(HasImage)); }
    }

    public bool HasImage => _image is not null;

    /// <summary>Tool invocations collected under a single collapsible group (ToolGroup role).</summary>
    public ObservableCollection<ToolEntry> Tools { get; } = new();

    private bool _isExpanded;
    public bool IsExpanded { get => _isExpanded; set => Set(ref _isExpanded, value); }

    /// <summary>Header shown on the collapsed tool group, e.g. "Użyto 3 narzędzi".</summary>
    public string GroupHeader => Tools.Count == 1
        ? "Użyto 1 narzędzia"
        : $"Użyto {Tools.Count} narzędzi";

    /// <summary>Adds a tool entry to this group and refreshes the header.</summary>
    public ToolEntry AddTool(string title)
    {
        var entry = new ToolEntry(title);
        Tools.Add(entry);
        Raise(nameof(GroupHeader));
        return entry;
    }

    public string Header => Role switch
    {
        BubbleRole.User => "Ty",
        BubbleRole.Assistant => "Asystent",
        BubbleRole.Tool => "Narzędzie AutoCAD",
        BubbleRole.Error => "Błąd",
        BubbleRole.Plan => "Plan",
        _ => "",
    };

    public bool IsUser => Role == BubbleRole.User;
    public bool IsAssistant => Role == BubbleRole.Assistant;
    public bool IsTool => Role == BubbleRole.Tool;
    public bool IsError => Role == BubbleRole.Error;
    public bool IsPlan => Role == BubbleRole.Plan;
    public bool IsToolGroup => Role == BubbleRole.ToolGroup;

    public void Append(string delta) => Text += delta;
}
