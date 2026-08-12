// DTOs for the 5 composite tools in the acad-schedules category.
// All numeric units are millimetres to match the architectural DWG convention.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Schedules;

// ─────────── shared ───────────

public sealed record ScheduleGenerationSummary(
    [property: JsonPropertyName("title")]       string Title,
    [property: JsonPropertyName("rows")]        int Rows,
    [property: JsonPropertyName("cols")]        int Cols,
    [property: JsonPropertyName("tableHandle")] string TableHandle,
    [property: JsonPropertyName("styleName")]   string StyleName,
    [property: JsonPropertyName("position")]    Point2dDto Position);

// ─────────── generate_door_schedule ───────────

public sealed record GenerateDoorScheduleArgs(
    [property: JsonPropertyName("position")]        Point2dDto Position,
    [property: JsonPropertyName("styleName")]       string StyleName = SchedulesPalette.StyleHospital,
    [property: JsonPropertyName("layer")]           string Layer = SchedulesPalette.LayerTables,
    [property: JsonPropertyName("textStyle")]       string TextStyle = "Standard",
    [property: JsonPropertyName("layerFilter")]     string? LayerFilter = null,
    [property: JsonPropertyName("ensureStyle")]     bool EnsureStyle = true,
    [property: JsonPropertyName("emptyPlaceholder")] string EmptyPlaceholder = "—");

public sealed record GenerateDoorScheduleResult(
    [property: JsonPropertyName("summary")]       ScheduleGenerationSummary Summary,
    [property: JsonPropertyName("doorCount")]     int DoorCount,
    [property: JsonPropertyName("complianceNotes")] IReadOnlyList<string> ComplianceNotes);

// ─────────── generate_window_schedule ───────────

public sealed record GenerateWindowScheduleArgs(
    [property: JsonPropertyName("position")]        Point2dDto Position,
    [property: JsonPropertyName("styleName")]       string StyleName = SchedulesPalette.StyleHospital,
    [property: JsonPropertyName("layer")]           string Layer = SchedulesPalette.LayerTables,
    [property: JsonPropertyName("textStyle")]       string TextStyle = "Standard",
    [property: JsonPropertyName("layerFilter")]     string? LayerFilter = null,
    [property: JsonPropertyName("ensureStyle")]     bool EnsureStyle = true,
    [property: JsonPropertyName("emptyPlaceholder")] string EmptyPlaceholder = "—");

public sealed record GenerateWindowScheduleResult(
    [property: JsonPropertyName("summary")]       ScheduleGenerationSummary Summary,
    [property: JsonPropertyName("windowCount")]   int WindowCount,
    [property: JsonPropertyName("complianceNotes")] IReadOnlyList<string> ComplianceNotes);

// ─────────── generate_room_schedule ───────────

public sealed record GenerateRoomScheduleArgs(
    [property: JsonPropertyName("position")]        Point2dDto Position,
    [property: JsonPropertyName("styleName")]       string StyleName = SchedulesPalette.StyleHospital,
    [property: JsonPropertyName("layer")]           string Layer = SchedulesPalette.LayerTables,
    [property: JsonPropertyName("textStyle")]       string TextStyle = "Standard",
    [property: JsonPropertyName("labelLayers")]     IReadOnlyList<string>? LabelLayers = null,
    [property: JsonPropertyName("boundaryLayer")]   string? BoundaryLayer = "A-ROOM-BNDY",
    [property: JsonPropertyName("ensureStyle")]     bool EnsureStyle = true,
    [property: JsonPropertyName("autoNumber")]      bool AutoNumber = true,
    [property: JsonPropertyName("emptyPlaceholder")] string EmptyPlaceholder = "—",
    // Overrides the flood-fill's automatic raster cell size (mm), which scales with the extent
    // of EVERY wall in the model, not the room being measured - on a large, multi-floor drawing
    // this can silently lose 5-10%+ accuracy on an individual room. Pass a small explicit value
    // (e.g. 50-100mm) for tighter accuracy on a big model; null (default) keeps automatic sizing.
    [property: JsonPropertyName("cellMm")]          double? CellMm = null);

public sealed record GenerateRoomScheduleResult(
    [property: JsonPropertyName("summary")]   ScheduleGenerationSummary Summary,
    [property: JsonPropertyName("roomCount")] int RoomCount,
    [property: JsonPropertyName("totalAreaM2")] double TotalAreaM2);

// ─────────── get_room_data (read-only) ───────────

public sealed record GetRoomDataArgs(
    [property: JsonPropertyName("query")]          string Query,
    // By default scans labels on EVERY layer and uses any closed polyline as a boundary, so it finds
    // rooms regardless of the layer naming convention. Narrow with labelLayers/boundaryLayer if needed.
    [property: JsonPropertyName("allLayers")]      bool AllLayers = true,
    [property: JsonPropertyName("labelLayers")]    IReadOnlyList<string>? LabelLayers = null,
    [property: JsonPropertyName("boundaryLayer")]  string? BoundaryLayer = null,
    // Margin (mm) added around the room bbox when assigning openings/furniture that sit on/near a wall.
    [property: JsonPropertyName("marginMm")]       double MarginMm = 250.0,
    // See GenerateRoomScheduleArgs.CellMm - same override, same reason.
    [property: JsonPropertyName("cellMm")]         double? CellMm = null);

public sealed record RoomOpeningDto(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("kind")]     string Kind,
    [property: JsonPropertyName("number")]   string? Number,
    [property: JsonPropertyName("blockName")] string? BlockName,
    [property: JsonPropertyName("widthMm")]  double WidthMm,
    [property: JsonPropertyName("heightMm")] double HeightMm,
    [property: JsonPropertyName("wall")]     string Wall,
    [property: JsonPropertyName("position")] Point2dDto Position);

public sealed record RoomFurnitureDto(
    [property: JsonPropertyName("handle")]    string Handle,
    [property: JsonPropertyName("blockName")] string BlockName,
    [property: JsonPropertyName("type")]      string? Type,
    [property: JsonPropertyName("layer")]     string? Layer,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("position")]  Point2dDto Position);

public sealed record GetRoomDataResult(
    [property: JsonPropertyName("found")]        bool Found,
    [property: JsonPropertyName("number")]       string? Number,
    [property: JsonPropertyName("name")]         string? Name,
    [property: JsonPropertyName("labels")]       IReadOnlyList<string> Labels,
    [property: JsonPropertyName("areaM2")]       double? AreaM2,
    [property: JsonPropertyName("widthMm")]      double? WidthMm,
    [property: JsonPropertyName("depthMm")]      double? DepthMm,
    [property: JsonPropertyName("doors")]        IReadOnlyList<RoomOpeningDto> Doors,
    [property: JsonPropertyName("windows")]      IReadOnlyList<RoomOpeningDto> Windows,
    [property: JsonPropertyName("furniture")]    IReadOnlyList<RoomFurnitureDto> Furniture,
    [property: JsonPropertyName("note")]         string? Note,
    // Area parsed from the label text (e.g. "200 m²"); reported alongside the measured area so the
    // agent can show both and never silently assume the stated figure equals the real geometry.
    [property: JsonPropertyName("labelAreaM2")]  double? LabelAreaM2 = null,
    // How the boundary was found: "flood", "raycast", "polyline" or "none".
    [property: JsonPropertyName("method")]       string? Method = null);

// ─────────── correct_room_area ───────────

public sealed record CorrectRoomAreaArgs(
    [property: JsonPropertyName("query")]          string Query,
    // When set, force this exact area (m²) onto the label. Otherwise the MEASURED area is used.
    [property: JsonPropertyName("explicitAreaM2")] double? ExplicitAreaM2 = null,
    // Only rewrite when |measured − labelled| / labelled exceeds this %, unless explicitAreaM2 is given.
    [property: JsonPropertyName("tolerancePct")]   double TolerancePct = 2.0,
    // false = dry-run: compute and report what WOULD change without editing the drawing.
    [property: JsonPropertyName("apply")]          bool Apply = true,
    // Decimal places for the written "N m²" value.
    [property: JsonPropertyName("decimals")]       int Decimals = 0,
    [property: JsonPropertyName("allLayers")]      bool AllLayers = true,
    [property: JsonPropertyName("labelLayers")]    IReadOnlyList<string>? LabelLayers = null,
    [property: JsonPropertyName("boundaryLayer")]  string? BoundaryLayer = null,
    // When true and the label text is corrected, ALSO replace the boundary polygon (the closed
    // polyline on boundaryLayer / A-ROOM-BNDY containing this room) with the flood-fill's own
    // measured outline - so the drawing's visible outline agrees with the corrected number
    // instead of a numerically-right label sitting over a stale shape. Opt-in: false preserves
    // this tool's original text-only contract for existing callers.
    [property: JsonPropertyName("syncBoundary")]   bool SyncBoundary = false,
    // See GenerateRoomScheduleArgs.CellMm - same override, same reason.
    [property: JsonPropertyName("cellMm")]         double? CellMm = null);

public sealed record CorrectRoomAreaResult(
    [property: JsonPropertyName("found")]          bool Found,
    [property: JsonPropertyName("handle")]         string? Handle,
    [property: JsonPropertyName("oldText")]        string? OldText,
    [property: JsonPropertyName("newText")]        string? NewText,
    [property: JsonPropertyName("labelAreaM2")]    double? LabelAreaM2,
    [property: JsonPropertyName("measuredAreaM2")] double? MeasuredAreaM2,
    [property: JsonPropertyName("appliedAreaM2")]  double? AppliedAreaM2,
    [property: JsonPropertyName("method")]         string? Method,
    [property: JsonPropertyName("changed")]        bool Changed,
    [property: JsonPropertyName("note")]           string? Note,
    [property: JsonPropertyName("boundaryResynced")] bool BoundaryResynced = false,
    [property: JsonPropertyName("boundaryOldHandle")] string? BoundaryOldHandle = null,
    [property: JsonPropertyName("boundaryNewHandle")] string? BoundaryNewHandle = null,
    [property: JsonPropertyName("boundaryNote")]      string? BoundaryNote = null);

// ─────────── audit_all_rooms (read-only batch) ───────────

public sealed record AuditAllRoomsArgs(
    [property: JsonPropertyName("allLayers")]      bool AllLayers = true,
    [property: JsonPropertyName("labelLayers")]    IReadOnlyList<string>? LabelLayers = null,
    [property: JsonPropertyName("tolerancePct")]   double TolerancePct = 10.0,
    [property: JsonPropertyName("marginMm")]       double MarginMm = 250.0,
    // Optional path to write CSV; defaults to %LOCALAPPDATA%\\AcadMcp\\reports\\ when set to "auto".
    [property: JsonPropertyName("exportCsvPath")]  string? ExportCsvPath = null,
    // See GenerateRoomScheduleArgs.CellMm - same override, same reason. MEASURED live: a 75-room,
    // 5-floor drawing of otherwise identical rooms reported labelMismatch on 60/75 purely from
    // automatic cell sizing scaling with the whole model's extent.
    [property: JsonPropertyName("cellMm")]         double? CellMm = null);

public sealed record RoomAuditRowDto(
    [property: JsonPropertyName("handle")]         string Handle,
    [property: JsonPropertyName("query")]          string Query,
    [property: JsonPropertyName("layer")]          string Layer,
    [property: JsonPropertyName("labelAreaM2")]    double? LabelAreaM2,
    [property: JsonPropertyName("measuredAreaM2")] double? MeasuredAreaM2,
    [property: JsonPropertyName("deltaPct")]       double? DeltaPct,
    [property: JsonPropertyName("method")]         string Method,
    [property: JsonPropertyName("doorCount")]      int DoorCount,
    [property: JsonPropertyName("windowCount")]    int WindowCount,
    [property: JsonPropertyName("furnitureCount")] int FurnitureCount,
    [property: JsonPropertyName("flags")]          IReadOnlyList<string> Flags);

public sealed record AuditAllRoomsResult(
    [property: JsonPropertyName("total")]          int Total,
    [property: JsonPropertyName("mismatches")]     int Mismatches,
    [property: JsonPropertyName("leaks")]          int Leaks,
    [property: JsonPropertyName("emptyDoors")]     int EmptyDoors,
    [property: JsonPropertyName("furnitureIssues")] int FurnitureIssues,
    [property: JsonPropertyName("rows")]           IReadOnlyList<RoomAuditRowDto> Rows,
    [property: JsonPropertyName("exportPath")]     string? ExportPath,
    [property: JsonPropertyName("note")]           string? Note);

// ─────────── correct_all_room_areas (batch write) ───────────

public sealed record CorrectAllRoomAreasArgs(
    [property: JsonPropertyName("tolerancePct")]   double TolerancePct = 10.0,
    [property: JsonPropertyName("apply")]          bool Apply = false,
    [property: JsonPropertyName("decimals")]       int Decimals = 0,
    [property: JsonPropertyName("allLayers")]      bool AllLayers = true,
    [property: JsonPropertyName("labelLayers")]    IReadOnlyList<string>? LabelLayers = null,
    // When true, only rows flagged labelMismatch by audit logic are corrected.
    [property: JsonPropertyName("onlyMismatches")] bool OnlyMismatches = true,
    // Forwarded to each correct_room_area call - see its own syncBoundary for what it does.
    [property: JsonPropertyName("syncBoundary")]   bool SyncBoundary = false,
    // See GenerateRoomScheduleArgs.CellMm - same override, same reason.
    [property: JsonPropertyName("cellMm")]         double? CellMm = null);

public sealed record CorrectAllRoomAreasEntry(
    [property: JsonPropertyName("query")]          string Query,
    [property: JsonPropertyName("changed")]        bool Changed,
    [property: JsonPropertyName("labelAreaM2")]    double? LabelAreaM2,
    [property: JsonPropertyName("measuredAreaM2")] double? MeasuredAreaM2,
    [property: JsonPropertyName("note")]           string? Note);

public sealed record CorrectAllRoomAreasResult(
    [property: JsonPropertyName("scanned")]        int Scanned,
    [property: JsonPropertyName("changed")]        int Changed,
    [property: JsonPropertyName("skipped")]        int Skipped,
    [property: JsonPropertyName("entries")]        IReadOnlyList<CorrectAllRoomAreasEntry> Entries,
    [property: JsonPropertyName("note")]           string? Note);

// ─────────── generate_finish_legend ───────────

public sealed record GenerateFinishLegendArgs(
    [property: JsonPropertyName("position")]     Point2dDto Position,
    [property: JsonPropertyName("styleName")]    string StyleName = SchedulesPalette.StyleHospital,
    [property: JsonPropertyName("layer")]        string Layer = SchedulesPalette.LayerLegend,
    [property: JsonPropertyName("textStyle")]    string TextStyle = "Standard",
    [property: JsonPropertyName("extraRows")]    IReadOnlyList<IReadOnlyList<string>>? ExtraRows = null,
    [property: JsonPropertyName("ensureStyle")]  bool EnsureStyle = true);

public sealed record GenerateFinishLegendResult(
    [property: JsonPropertyName("summary")]   ScheduleGenerationSummary Summary,
    [property: JsonPropertyName("rowCount")]  int RowCount);

// ─────────── update_schedules ───────────

public sealed record UpdateSchedulesArgs(
    [property: JsonPropertyName("titleContains")] string? TitleContains = null,
    [property: JsonPropertyName("layerFilter")]   string? LayerFilter = null,
    [property: JsonPropertyName("styleName")]     string StyleName = SchedulesPalette.StyleHospital,
    [property: JsonPropertyName("textStyle")]     string TextStyle = "Standard",
    [property: JsonPropertyName("labelLayers")]   IReadOnlyList<string>? LabelLayers = null,
    [property: JsonPropertyName("boundaryLayer")] string? BoundaryLayer = "A-ROOM-BNDY");

public sealed record UpdateScheduleEntry(
    [property: JsonPropertyName("oldHandle")]   string OldHandle,
    [property: JsonPropertyName("newHandle")]   string NewHandle,
    [property: JsonPropertyName("title")]       string Title,
    [property: JsonPropertyName("kind")]        string Kind,
    [property: JsonPropertyName("rowsBefore")]  int RowsBefore,
    [property: JsonPropertyName("rowsAfter")]   int RowsAfter);

public sealed record UpdateSchedulesResult(
    [property: JsonPropertyName("updated")]    IReadOnlyList<UpdateScheduleEntry> Updated,
    [property: JsonPropertyName("skipped")]    IReadOnlyList<string> Skipped,
    [property: JsonPropertyName("tablesScanned")] int TablesScanned);
