// Typed DTOs for acad-openings.
// See rules 19 (tool impl pattern), 22 (args/results) and 65-door-window-schedule.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Openings;

#region catalog

public sealed record ListOpeningCatalogArgs(
    [property: JsonPropertyName("kind")] string? Kind = null);

public sealed record OpeningCatalogEntry(
    [property: JsonPropertyName("family")]      string Family,
    [property: JsonPropertyName("kind")]        string Kind,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("defaultWidthMm")]  double DefaultWidthMm,
    [property: JsonPropertyName("defaultHeightMm")] double DefaultHeightMm,
    [property: JsonPropertyName("supportsFire")]    bool SupportsFire,
    [property: JsonPropertyName("supportsBurglary")] bool SupportsBurglary,
    [property: JsonPropertyName("supportsLeadShield")] bool SupportsLeadShield);

public sealed record ListOpeningCatalogResult(
    [property: JsonPropertyName("entries")] IReadOnlyList<OpeningCatalogEntry> Entries,
    [property: JsonPropertyName("count")]   int Count);

#endregion

#region doors

public sealed record InsertDoorArgs(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("type")]       string Type = "single",
    [property: JsonPropertyName("widthMm")]    double WidthMm = 900.0,
    [property: JsonPropertyName("heightMm")]   double HeightMm = 2100.0,
    [property: JsonPropertyName("leafDirection")] string LeafDirection = "R",
    [property: JsonPropertyName("swingDirection")] string SwingDirection = "IN",
    [property: JsonPropertyName("rei")]        int Rei = 0,
    [property: JsonPropertyName("acousticDb")] int AcousticDb = 0,
    [property: JsonPropertyName("leadShielded")] bool LeadShielded = false,
    [property: JsonPropertyName("roomFrom")]   string? RoomFrom = null,
    [property: JsonPropertyName("roomTo")]     string? RoomTo = null,
    [property: JsonPropertyName("number")]     string? Number = null,
    [property: JsonPropertyName("autoNumber")] bool AutoNumber = true,
    [property: JsonPropertyName("layer")]      string? Layer = null,
    // Optional: handle of the wall (Line or 2-vertex Polyline) this door sits in. When supplied,
    // the wall is cut at the door's own axis span (position ± widthMm/2 along rotationDeg) BEFORE
    // the block is placed - 2026-08-12, closing the gap that made architecture.insert_door the
    // only way to get BOTH a cut wall and a placed opening in one call.
    [property: JsonPropertyName("wallHandle")] string? WallHandle = null);

public sealed record InsertWindowArgs(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("type")]       string Type = "casement",
    [property: JsonPropertyName("widthMm")]    double WidthMm = 1200.0,
    [property: JsonPropertyName("heightMm")]   double HeightMm = 1500.0,
    [property: JsonPropertyName("sillHeightMm")] double SillHeightMm = 900.0,
    [property: JsonPropertyName("rc")]         int Rc = 0,
    [property: JsonPropertyName("fireClass")]  string FireClass = "",
    [property: JsonPropertyName("room")]       string? Room = null,
    [property: JsonPropertyName("number")]     string? Number = null,
    [property: JsonPropertyName("autoNumber")] bool AutoNumber = true,
    [property: JsonPropertyName("layer")]      string? Layer = null,
    [property: JsonPropertyName("wallHandle")] string? WallHandle = null);

public sealed record InsertOpeningGenericArgs(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("layer")]      string? Layer = null,
    [property: JsonPropertyName("attributes")] Dictionary<string, string>? Attributes = null);

public sealed record DrawDoorByPointsArgs(
    [property: JsonPropertyName("hingePoint")] Point2dDto HingePoint,
    [property: JsonPropertyName("leafEnd")]    Point2dDto LeafEnd,
    [property: JsonPropertyName("swingDirection")] string SwingDirection = "IN",
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record DrawWindowByPointsArgs(
    [property: JsonPropertyName("jamb1")]      Point2dDto Jamb1,
    [property: JsonPropertyName("jamb2")]      Point2dDto Jamb2,
    [property: JsonPropertyName("wallThickness")] double WallThickness = 250.0,
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record CutWallForOpeningArgs(
    [property: JsonPropertyName("wallHandle")] string WallHandle,
    [property: JsonPropertyName("jamb1")]      Point2dDto Jamb1,
    [property: JsonPropertyName("jamb2")]      Point2dDto Jamb2);

public sealed record CutWallForOpeningResult(
    [property: JsonPropertyName("originalHandle")] string OriginalHandle,
    [property: JsonPropertyName("leftHandle")]  string? LeftHandle,
    [property: JsonPropertyName("rightHandle")] string? RightHandle,
    [property: JsonPropertyName("gapLengthMm")] double GapLengthMm);

public sealed record RenumberOpeningsArgs(
    [property: JsonPropertyName("kind")]       string Kind = "all",
    [property: JsonPropertyName("order")]      string Order = "insertion",
    [property: JsonPropertyName("startAt")]    int StartAt = 1,
    [property: JsonPropertyName("prefixDoor")] string PrefixDoor = "D",
    [property: JsonPropertyName("prefixWindow")] string PrefixWindow = "W",
    [property: JsonPropertyName("padDigits")]  int PadDigits = 3);

public sealed record RenumberOpeningsResult(
    [property: JsonPropertyName("doorsRenumbered")]  int DoorsRenumbered,
    [property: JsonPropertyName("windowsRenumbered")] int WindowsRenumbered,
    [property: JsonPropertyName("changes")]    IReadOnlyList<RenumberChange> Changes);

public sealed record RenumberChange(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("oldNumber")] string? OldNumber,
    [property: JsonPropertyName("newNumber")] string NewNumber);

public sealed record ListOpeningsInModelArgs(
    [property: JsonPropertyName("kind")]        string Kind = "all",
    [property: JsonPropertyName("layerFilter")] string? LayerFilter = null);

public sealed record OpeningInfo(
    [property: JsonPropertyName("handle")]     string Handle,
    [property: JsonPropertyName("blockName")]  string BlockName,
    [property: JsonPropertyName("kind")]       string Kind,
    [property: JsonPropertyName("number")]     string? Number,
    [property: JsonPropertyName("type")]       string? Type,
    [property: JsonPropertyName("widthMm")]    double WidthMm,
    [property: JsonPropertyName("heightMm")]   double HeightMm,
    [property: JsonPropertyName("rei")]        int Rei,
    [property: JsonPropertyName("rc")]         int Rc,
    [property: JsonPropertyName("fireClass")]  string? FireClass,
    [property: JsonPropertyName("acousticDb")] int AcousticDb,
    [property: JsonPropertyName("leadShielded")] bool LeadShielded,
    [property: JsonPropertyName("roomFrom")]   string? RoomFrom,
    [property: JsonPropertyName("roomTo")]     string? RoomTo,
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("layer")]      string Layer);

public sealed record ListOpeningsInModelResult(
    [property: JsonPropertyName("openings")] IReadOnlyList<OpeningInfo> Openings,
    [property: JsonPropertyName("count")]    int Count);

public sealed record ExportScheduleArgs(
    [property: JsonPropertyName("kind")]        string Kind = "all",
    [property: JsonPropertyName("format")]      string Format = "csv",
    [property: JsonPropertyName("outputPath")]  string? OutputPath = null);

public sealed record ExportScheduleResult(
    [property: JsonPropertyName("kind")]       string Kind,
    [property: JsonPropertyName("format")]     string Format,
    [property: JsonPropertyName("outputPath")] string? OutputPath,
    [property: JsonPropertyName("rowCount")]   int RowCount,
    [property: JsonPropertyName("content")]    string Content);

public sealed record OpeningInsertResult(
    [property: JsonPropertyName("entity")]     EntityHandle Entity,
    [property: JsonPropertyName("blockName")]  string BlockName,
    [property: JsonPropertyName("created")]    bool Created,
    [property: JsonPropertyName("number")]     string? Number,
    [property: JsonPropertyName("widthMm")]    double WidthMm,
    [property: JsonPropertyName("heightMm")]   double HeightMm,
    [property: JsonPropertyName("wallOpening")] CutWallForOpeningResult? WallOpening = null);

public sealed record SketchResult(
    [property: JsonPropertyName("entities")] IReadOnlyList<EntityHandle> Entities);

#endregion
