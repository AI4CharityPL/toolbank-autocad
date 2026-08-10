// Typed DTOs for the acad-sections-3d category. Mirrors plugin-side wire shape.
// See rule 19-tool-implementation-pattern.md.

using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Sections3d;

public sealed record SectionCreateArgs(
    // No `normal`: Section.Normal is READ-ONLY and the cut plane is the one containing the
    // section line and verticalDirection, so the normal is a result, never an input.
    [property: JsonPropertyName("vertices")]          IReadOnlyList<Point3dDto> Vertices,
    [property: JsonPropertyName("verticalDirection")] Point3dDto? VerticalDirection = null,
    [property: JsonPropertyName("state")]             string? State = null,
    [property: JsonPropertyName("elevation")]         double? Elevation = null,
    [property: JsonPropertyName("height")]            double? Height = null,
    [property: JsonPropertyName("depth")]             double? Depth = null,
    [property: JsonPropertyName("liveSection")]       bool? LiveSection = null,
    [property: JsonPropertyName("layer")]             string? Layer = null);

public sealed record SectionStateArgs(
    [property: JsonPropertyName("handle")] string Handle,
    [property: JsonPropertyName("state")]  string? State = null);

public sealed record SectionLiveArgs(
    [property: JsonPropertyName("handle")]  string Handle,
    [property: JsonPropertyName("enabled")] bool? Enabled = null);

public sealed record SectionHeightArgs(
    [property: JsonPropertyName("handle")]    string Handle,
    [property: JsonPropertyName("above")]     double? Above = null,
    [property: JsonPropertyName("below")]     double? Below = null,
    [property: JsonPropertyName("elevation")] double? Elevation = null);

public sealed record SectionGenerateArgs(
    [property: JsonPropertyName("handle")]            string Handle,
    [property: JsonPropertyName("sourceHandles")]     IReadOnlyList<string> SourceHandles,
    [property: JsonPropertyName("kind")]              string? Kind = null,
    [property: JsonPropertyName("includeBackground")] bool? IncludeBackground = null,
    [property: JsonPropertyName("includeForeground")] bool? IncludeForeground = null,
    [property: JsonPropertyName("includeTangency")]   bool? IncludeTangency = null,
    [property: JsonPropertyName("layer")]             string? Layer = null);

public sealed record SectionListArgs();

public sealed record SectionCreateResult(
    [property: JsonPropertyName("entity")]      EntityHandle Entity,
    [property: JsonPropertyName("vertices")]    int Vertices,
    [property: JsonPropertyName("state")]       string State,
    [property: JsonPropertyName("liveSection")] bool LiveSection,
    // Reported, not accepted - see the note on SectionCreateArgs.
    [property: JsonPropertyName("normal")]             Point3dDto Normal,
    [property: JsonPropertyName("verticalDirection")]  Point3dDto VerticalDirection,
    [property: JsonPropertyName("note")]        string Note);

public sealed record SectionInfo(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("state")]       string State,
    [property: JsonPropertyName("liveSection")] bool LiveSection,
    [property: JsonPropertyName("vertices")]    int Vertices,
    [property: JsonPropertyName("elevation")]   double Elevation,
    [property: JsonPropertyName("normal")]      Point3dDto Normal,
    [property: JsonPropertyName("layer")]       string Layer);

public sealed record SectionListResult(
    [property: JsonPropertyName("count")]    int Count,
    [property: JsonPropertyName("sections")] IReadOnlyList<SectionInfo> Sections,
    [property: JsonPropertyName("note")]     string Note);

public sealed record SectionStateResult(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("stateBefore")] string StateBefore,
    [property: JsonPropertyName("state")]       string State,
    [property: JsonPropertyName("note")]        string Note);

public sealed record SectionLiveResult(
    [property: JsonPropertyName("handle")]            string Handle,
    [property: JsonPropertyName("liveSectionBefore")] bool LiveSectionBefore,
    [property: JsonPropertyName("liveSection")]       bool LiveSection,
    [property: JsonPropertyName("note")]              string Note);

public sealed record SectionHeightResult(
    [property: JsonPropertyName("handle")]          string Handle,
    [property: JsonPropertyName("aboveBefore")]     double AboveBefore,
    [property: JsonPropertyName("above")]           double Above,
    [property: JsonPropertyName("belowBefore")]     double BelowBefore,
    [property: JsonPropertyName("below")]           double Below,
    [property: JsonPropertyName("elevationBefore")] double ElevationBefore,
    [property: JsonPropertyName("elevation")]       double Elevation,
    [property: JsonPropertyName("note")]            string Note);

public sealed record SectionGenerateResult(
    [property: JsonPropertyName("entities")]         IReadOnlyList<EntityHandle> Entities,
    [property: JsonPropertyName("count")]            int Count,
    [property: JsonPropertyName("kind")]             string Kind,
    [property: JsonPropertyName("cutCurves")]        int CutCurves,
    [property: JsonPropertyName("backgroundCurves")] int BackgroundCurves,
    [property: JsonPropertyName("foregroundCurves")] int ForegroundCurves,
    [property: JsonPropertyName("tangencyCurves")]   int TangencyCurves,
    [property: JsonPropertyName("totalCurveLength")] double TotalCurveLength,
    [property: JsonPropertyName("note")]             string Note);
