// Plugin-side DTOs for acad-openings. Must stay binary-compatible with the
// backend DTOs in Categories/Openings/OpeningsDtos.cs (JsonPropertyName).

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record OpeningsListCatalogArgsDto(
    [property: JsonPropertyName("kind")] string? Kind);

internal sealed record OpeningsInsertDoorDto(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("type")]       string Type,
    [property: JsonPropertyName("widthMm")]    double WidthMm,
    [property: JsonPropertyName("heightMm")]   double HeightMm,
    [property: JsonPropertyName("leafDirection")] string LeafDirection,
    [property: JsonPropertyName("swingDirection")] string SwingDirection,
    [property: JsonPropertyName("rei")]        int Rei,
    [property: JsonPropertyName("acousticDb")] int AcousticDb,
    [property: JsonPropertyName("leadShielded")] bool LeadShielded,
    [property: JsonPropertyName("roomFrom")]   string? RoomFrom,
    [property: JsonPropertyName("roomTo")]     string? RoomTo,
    [property: JsonPropertyName("number")]     string? Number,
    [property: JsonPropertyName("autoNumber")] bool AutoNumber,
    [property: JsonPropertyName("layer")]      string? Layer);

internal sealed record OpeningsInsertWindowDto(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("type")]       string Type,
    [property: JsonPropertyName("widthMm")]    double WidthMm,
    [property: JsonPropertyName("heightMm")]   double HeightMm,
    [property: JsonPropertyName("sillHeightMm")] double SillHeightMm,
    [property: JsonPropertyName("rc")]         int Rc,
    [property: JsonPropertyName("fireClass")]  string FireClass,
    [property: JsonPropertyName("room")]       string? Room,
    [property: JsonPropertyName("number")]     string? Number,
    [property: JsonPropertyName("autoNumber")] bool AutoNumber,
    [property: JsonPropertyName("layer")]      string? Layer);

internal sealed record OpeningsInsertGenericDto(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("layer")]      string? Layer,
    [property: JsonPropertyName("attributes")] Dictionary<string, string>? Attributes);

internal sealed record OpeningsDrawDoorDto(
    [property: JsonPropertyName("hingePoint")] Point2dDto HingePoint,
    [property: JsonPropertyName("leafEnd")]    Point2dDto LeafEnd,
    [property: JsonPropertyName("swingDirection")] string SwingDirection,
    [property: JsonPropertyName("layer")]      string? Layer);

internal sealed record OpeningsDrawWindowDto(
    [property: JsonPropertyName("jamb1")]      Point2dDto Jamb1,
    [property: JsonPropertyName("jamb2")]      Point2dDto Jamb2,
    [property: JsonPropertyName("wallThickness")] double WallThickness,
    [property: JsonPropertyName("layer")]      string? Layer);

internal sealed record OpeningsCutWallDto(
    [property: JsonPropertyName("wallHandle")] string WallHandle,
    [property: JsonPropertyName("jamb1")]      Point2dDto Jamb1,
    [property: JsonPropertyName("jamb2")]      Point2dDto Jamb2);

internal sealed record OpeningsRenumberDto(
    [property: JsonPropertyName("kind")]       string Kind,
    [property: JsonPropertyName("order")]      string Order,
    [property: JsonPropertyName("startAt")]    int StartAt,
    [property: JsonPropertyName("prefixDoor")] string PrefixDoor,
    [property: JsonPropertyName("prefixWindow")] string PrefixWindow,
    [property: JsonPropertyName("padDigits")]  int PadDigits);

internal sealed record OpeningsListInModelDto(
    [property: JsonPropertyName("kind")]        string Kind,
    [property: JsonPropertyName("layerFilter")] string? LayerFilter);

internal sealed record OpeningsExportScheduleDto(
    [property: JsonPropertyName("kind")]        string Kind,
    [property: JsonPropertyName("format")]      string Format,
    [property: JsonPropertyName("outputPath")]  string? OutputPath);
