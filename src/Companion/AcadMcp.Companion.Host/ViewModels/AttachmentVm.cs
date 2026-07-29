using AcadMcp.Companion.Agent;

namespace AcadMcp.Companion.Host.ViewModels;

/// <summary>A staged file attachment shown as a chip before the message is sent.</summary>
public sealed class AttachmentVm
{
    public AttachmentVm(string fileName, ContentPart part)
    {
        FileName = fileName;
        Part = part;
    }

    public string FileName { get; }
    public ContentPart Part { get; }
}
