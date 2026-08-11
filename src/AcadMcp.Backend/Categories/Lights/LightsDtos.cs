// Typed DTOs for the acad-lights category. Mirrors plugin-side wire shape.
// See rule 19-tool-implementation-pattern.md.

using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Lights;

public sealed record PointLightArgs(
    [property: JsonPropertyName("name")]      string Name,
    [property: JsonPropertyName("position")]  Point3dDto Position,
    [property: JsonPropertyName("intensity")] double? Intensity = null,
    [property: JsonPropertyName("color")]     ColorDto? Color = null,
    [property: JsonPropertyName("on")]        bool? On = null,
    [property: JsonPropertyName("layer")]     string? Layer = null);

public sealed record SpotLightArgs(
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("position")]     Point3dDto Position,
    [property: JsonPropertyName("target")]       Point3dDto Target,
    [property: JsonPropertyName("hotspotAngle")] double? HotspotAngle = null,
    [property: JsonPropertyName("falloffAngle")] double? FalloffAngle = null,
    [property: JsonPropertyName("intensity")]    double? Intensity = null,
    [property: JsonPropertyName("color")]        ColorDto? Color = null,
    [property: JsonPropertyName("on")]           bool? On = null,
    [property: JsonPropertyName("layer")]        string? Layer = null);

public sealed record DistantLightArgs(
    [property: JsonPropertyName("name")]      string Name,
    [property: JsonPropertyName("direction")] Point3dDto? Direction = null,
    [property: JsonPropertyName("position")]  Point3dDto? Position = null,
    [property: JsonPropertyName("target")]    Point3dDto? Target = null,
    [property: JsonPropertyName("intensity")] double? Intensity = null,
    [property: JsonPropertyName("color")]     ColorDto? Color = null,
    [property: JsonPropertyName("on")]        bool? On = null,
    [property: JsonPropertyName("layer")]     string? Layer = null);

public sealed record LightsNoArgs();

public sealed record LightNameArgs(
    [property: JsonPropertyName("name")] string Name);

public sealed record LightModifyArgs(
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("on")]           bool? On = null,
    [property: JsonPropertyName("intensity")]    double? Intensity = null,
    [property: JsonPropertyName("color")]        ColorDto? Color = null,
    [property: JsonPropertyName("position")]     Point3dDto? Position = null,
    [property: JsonPropertyName("target")]       Point3dDto? Target = null,
    [property: JsonPropertyName("hotspotAngle")] double? HotspotAngle = null,
    [property: JsonPropertyName("falloffAngle")] double? FalloffAngle = null);

public sealed record LightRgb(
    [property: JsonPropertyName("r")] int R,
    [property: JsonPropertyName("g")] int G,
    [property: JsonPropertyName("b")] int B);

public sealed record LightInfo(
    [property: JsonPropertyName("name")]         string Name,
    [property: JsonPropertyName("type")]         string Type,
    [property: JsonPropertyName("on")]           bool On,
    [property: JsonPropertyName("position")]     Point3dDto Position,
    [property: JsonPropertyName("target")]       Point3dDto? Target,
    [property: JsonPropertyName("hasTarget")]    bool HasTarget,
    [property: JsonPropertyName("intensity")]    double Intensity,
    [property: JsonPropertyName("hotspotAngle")] double HotspotAngle,
    [property: JsonPropertyName("falloffAngle")] double FalloffAngle,
    [property: JsonPropertyName("color")]        LightRgb Color,
    [property: JsonPropertyName("handle")]       string Handle,
    [property: JsonPropertyName("layer")]        string? Layer);

public sealed record LightCreateResult(
    [property: JsonPropertyName("entity")]    EntityHandle Entity,
    [property: JsonPropertyName("light")]     LightInfo Light,
    [property: JsonPropertyName("direction")] Point3dDto? Direction,
    [property: JsonPropertyName("note")]      string Note);

public sealed record LightListResult(
    [property: JsonPropertyName("count")]   int Count,
    [property: JsonPropertyName("onCount")] int OnCount,
    [property: JsonPropertyName("lights")]  IReadOnlyList<LightInfo> Lights,
    [property: JsonPropertyName("note")]    string Note);

public sealed record LightModifyResult(
    [property: JsonPropertyName("changed")] IReadOnlyList<string> Changed,
    [property: JsonPropertyName("before")]  LightInfo Before,
    [property: JsonPropertyName("light")]   LightInfo Light,
    [property: JsonPropertyName("note")]    string Note);

public sealed record LightDeleteResult(
    [property: JsonPropertyName("name")]     string Name,
    [property: JsonPropertyName("deleted")]  bool Deleted,
    [property: JsonPropertyName("previous")] LightInfo Previous,
    [property: JsonPropertyName("note")]     string Note);

// ── the sun (roadmap 6.1, third tranche) ──
//
// MEASURED: a sun belongs to a VIEWPORT, not to the drawing - Database.SunId does not exist and
// ViewportTableRecord.SunId does. SkyParameters.Illumination is a BOOL, not a level.

public sealed record SunSetArgs(
    [property: JsonPropertyName("on")]              bool? On = null,
    [property: JsonPropertyName("intensity")]       double? Intensity = null,
    [property: JsonPropertyName("dateTime")]        string? DateTime = null,
    [property: JsonPropertyName("skyIllumination")] bool? SkyIllumination = null,
    [property: JsonPropertyName("haze")]            double? Haze = null);

public sealed record SunInfo(
    [property: JsonPropertyName("on")]              bool On,
    [property: JsonPropertyName("intensity")]       double Intensity,
    [property: JsonPropertyName("dateTime")]        string DateTime,
    [property: JsonPropertyName("skyIllumination")] bool SkyIllumination,
    [property: JsonPropertyName("haze")]            double Haze,
    [property: JsonPropertyName("shadowType")]      string? ShadowType,
    [property: JsonPropertyName("shadowMapSize")]   int ShadowMapSize,
    [property: JsonPropertyName("handle")]          string Handle);

public sealed record SunGetResult(
    [property: JsonPropertyName("hasSun")] bool HasSun,
    [property: JsonPropertyName("sun")]    SunInfo? Sun,
    [property: JsonPropertyName("note")]   string Note);

public sealed record SunSetResult(
    [property: JsonPropertyName("created")] bool Created,
    [property: JsonPropertyName("before")]  SunInfo? Before,
    [property: JsonPropertyName("sun")]     SunInfo Sun,
    [property: JsonPropertyName("note")]    string Note);
