// Typed DTOs for the acad-views category. Mirrors plugin-side wire shape.
// See rule 19-tool-implementation-pattern.md.

using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Views;

public sealed record ViewCreateArgs(
    [property: JsonPropertyName("name")]          string Name,
    [property: JsonPropertyName("center")]        Point3dDto Center,
    [property: JsonPropertyName("width")]         double Width,
    [property: JsonPropertyName("height")]        double Height,
    [property: JsonPropertyName("target")]        Point3dDto? Target = null,
    [property: JsonPropertyName("viewDirection")] Point3dDto? ViewDirection = null,
    [property: JsonPropertyName("lensLength")]    double? LensLength = null,
    [property: JsonPropertyName("twist")]         double? Twist = null);

public sealed record ViewWindowArgs(
    [property: JsonPropertyName("name")]          string Name,
    [property: JsonPropertyName("corner1")]       Point3dDto Corner1,
    [property: JsonPropertyName("corner2")]       Point3dDto Corner2,
    [property: JsonPropertyName("target")]        Point3dDto? Target = null,
    [property: JsonPropertyName("viewDirection")] Point3dDto? ViewDirection = null,
    [property: JsonPropertyName("lensLength")]    double? LensLength = null,
    [property: JsonPropertyName("twist")]         double? Twist = null);

public sealed record ViewsNoArgs();

public sealed record ViewNameArgs(
    [property: JsonPropertyName("name")] string Name);

public sealed record ViewRestoreArgs(
    [property: JsonPropertyName("name")]           string Name,
    [property: JsonPropertyName("viewportHandle")] string ViewportHandle);

public sealed record ViewTargetArgs(
    [property: JsonPropertyName("name")]   string Name,
    [property: JsonPropertyName("target")] Point3dDto Target);

public sealed record ViewLensArgs(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("lensLength")] double LensLength);

public sealed record ViewPerspectiveArgs(
    [property: JsonPropertyName("viewportHandle")] string ViewportHandle,
    [property: JsonPropertyName("enabled")]        bool Enabled);

public sealed record ViewUcsArgs(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("ucsName")] string? UcsName = null);

public sealed record Point2dOut(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y);

public sealed record ViewInfo(
    [property: JsonPropertyName("name")]          string Name,
    [property: JsonPropertyName("center")]        Point2dOut Center,
    [property: JsonPropertyName("width")]         double Width,
    [property: JsonPropertyName("height")]        double Height,
    [property: JsonPropertyName("target")]        Point3dDto Target,
    [property: JsonPropertyName("viewDirection")] Point3dDto ViewDirection,
    [property: JsonPropertyName("lensLength")]    double LensLength,
    [property: JsonPropertyName("twist")]         double Twist,
    [property: JsonPropertyName("elevation")]     double Elevation,
    [property: JsonPropertyName("frontClip")]     double? FrontClip,
    [property: JsonPropertyName("backClip")]      double? BackClip,
    [property: JsonPropertyName("ucsAssociated")] bool UcsAssociated,
    [property: JsonPropertyName("handle")]        string Handle);

public sealed record ViewCreateResult(
    [property: JsonPropertyName("view")]        ViewInfo View,
    [property: JsonPropertyName("createdFrom")] string CreatedFrom,
    [property: JsonPropertyName("note")]        string Note);

public sealed record ViewListResult(
    [property: JsonPropertyName("count")] int Count,
    [property: JsonPropertyName("views")] IReadOnlyList<ViewInfo> Views,
    [property: JsonPropertyName("note")]  string Note);

public sealed record ViewDeleteResult(
    [property: JsonPropertyName("name")]    string Name,
    [property: JsonPropertyName("deleted")] bool Deleted,
    [property: JsonPropertyName("note")]    string Note);

public sealed record ViewRestoreResult(
    [property: JsonPropertyName("name")]             string Name,
    [property: JsonPropertyName("viewportHandle")]   string ViewportHandle,
    [property: JsonPropertyName("viewHeightBefore")] double ViewHeightBefore,
    [property: JsonPropertyName("viewHeight")]       double ViewHeight,
    [property: JsonPropertyName("target")]           Point3dDto Target,
    [property: JsonPropertyName("twist")]            double Twist,
    [property: JsonPropertyName("note")]             string Note);

public sealed record ViewTargetResult(
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("targetBefore")] Point3dDto TargetBefore,
    [property: JsonPropertyName("target")]       Point3dDto Target,
    [property: JsonPropertyName("note")]         string Note);

public sealed record ViewLensResult(
    [property: JsonPropertyName("name")]             string Name,
    [property: JsonPropertyName("lensLengthBefore")] double LensLengthBefore,
    [property: JsonPropertyName("lensLength")]       double LensLength,
    [property: JsonPropertyName("note")]             string Note);

public sealed record ViewPerspectiveResult(
    [property: JsonPropertyName("viewportHandle")]    string ViewportHandle,
    [property: JsonPropertyName("perspectiveBefore")] bool PerspectiveBefore,
    [property: JsonPropertyName("perspective")]       bool Perspective,
    [property: JsonPropertyName("lensLength")]        double LensLength,
    [property: JsonPropertyName("note")]              string Note);

public sealed record ViewUcsResult(
    [property: JsonPropertyName("name")]                 string Name,
    [property: JsonPropertyName("ucsAssociatedBefore")]  bool UcsAssociatedBefore,
    [property: JsonPropertyName("ucsAssociated")]        bool UcsAssociated,
    [property: JsonPropertyName("ucs")]                  string Ucs,
    [property: JsonPropertyName("note")]                 string Note);
