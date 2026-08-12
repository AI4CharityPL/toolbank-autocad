// AutoCAD acad-openings category. 10 tools covering professional door and window
// placement with fire (REI), acoustic, burglary (RC) and lead-shield ratings,
// plus automatic numbering D-001 / W-001 and schedule export to CSV/JSON.
//
// Thin proxies over plugin handlers "acad.openings.<verb>".
// Rules: 19-tool-implementation-pattern, 65-door-window-schedule.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Openings;

public static class OpeningsTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;
    private const int T_SLOW = 30_000;

    [McpTool("list_opening_catalog",
        "Enumerate the built-in doors + windows block catalog. Read-only. Covers: single/double/sliding/fire/hospital/lead-lined doors and fixed/casement/tilt/hospital/fire windows. Returns family name, default width/height, kind (door|window), and capability flags (supportsFire, supportsBurglary, supportsLeadShield).",
        "openings",
        Intent = new[]
        {
            "lista katalogu drzwi i okien", "katalog otworow", "list opening catalog",
            "show available doors windows", "enumerate door window blocks", "opening families"
        },
        ReadOnly = true,
        RequiresPlugin = true)]
    public static Task<ListOpeningCatalogResult> ListOpeningCatalog(IPluginGateway gw, ListOpeningCatalogArgs args, CancellationToken ct)
        => OpeningsProxy.CallAsync<ListOpeningCatalogArgs, ListOpeningCatalogResult>(gw, "acad.openings.list_opening_catalog", args, T_FAST, ct);

    [McpTool("insert_door",
        "THE OPENINGS ONE: places a numbered BLOCK carrying the attributes generate_door_schedule / audit_all_rooms / get_room_data read. architecture.insert_door is the other one, which draws the door as plain primitives on the layer standard and is INVISIBLE to every schedule/audit tool in this bank (list_openings_in_model only finds block names starting with DOOR-/WIN-) - if the door needs to appear in a schedule or be counted by an audit, it MUST be placed with this tool, not the architecture one. Insert a door block at a wall opening. Pass wallHandle to also cut the host wall at the door's own axis span (position ± widthMm/2 along rotationDeg) before placing the block - added 2026-08-12 so this tool alone can do what previously needed architecture.insert_door for the cut and this one for the schedule entry. Types: 'single' (900x2100 hinged), 'double' (1600x2100 two-leaf), 'sliding' (1000x2100), 'fire' (REI 30/60/90/120 EI marker), 'hospital' (double-swing with trajectory arrows), 'lead' (radiological Pb-marker). Auto-assigns number D-001, D-002... (skipped by number= or autoNumber=false). Attributes: NUMBER, TYPE, WIDTH_MM, HEIGHT_MM, REI, LEAF_DIR, SWING_DIR, ROOM_FROM, ROOM_TO, ACOUSTIC_DB, LEAD, LINTEL_TYPE. Pass lintelType (the lintelTypeTag from acad-structural.insert_lintel, e.g. 'HEB160' or 'RC-150x250') to record it on the door's own schedule row - purely a tag, this tool does not size or draw the lintel itself. Layer defaults to A-DOOR (or A-DOOR-FIRE / A-DOOR-LEAD depending on type).",
        "openings",
        Intent = new[]
        {
            "wstaw drzwi REI 60", "dodaj drzwi przeciwpozarowe", "insert door",
            "place fire door REI 120", "drzwi przesuwne", "sliding door", "hospital door",
            "lead lined door", "drzwi olowiane RTG", "drzwi pojedyncze szerokosc 900"
        },
        RequiresPlugin = true)]
    public static Task<OpeningInsertResult> InsertDoor(IPluginGateway gw, InsertDoorArgs args, CancellationToken ct)
        => OpeningsProxy.CallAsync<InsertDoorArgs, OpeningInsertResult>(gw, "acad.openings.insert_door", args, T_NORMAL, ct);

    [McpTool("insert_window",
        "THE OPENINGS ONE: places a numbered BLOCK carrying the attributes generate_window_schedule / audit_all_rooms / get_room_data read. architecture.insert_window is the other one, which draws the window as plain primitives on the layer standard and is INVISIBLE to every schedule/audit tool in this bank (list_openings_in_model only finds block names starting with DOOR-/WIN-) - if the window needs to appear in a schedule or be counted by an audit, it MUST be placed with this tool, not the architecture one. Insert a window block at a wall opening. Pass wallHandle to also cut the host wall at the window's own axis span (position ± widthMm/2 along rotationDeg) before placing the block - added 2026-08-12 so this tool alone can do what previously needed architecture.insert_window for the cut and this one for the schedule entry. Types: 'fixed' (non-opening), 'casement' (side-hung), 'tilt' (tilt & turn), 'hospital' (fire-rated E/EI30/EI60), 'fire' (EI30/EI60/EI120). Burglary rating (RC 1..6 per PN-EN 1627) and fire class supported per type. Auto-assigns W-001, W-002... Attributes: NUMBER, TYPE, WIDTH_MM, HEIGHT_MM, SILL_MM, RC, FIRE_CLASS, ROOM, LINTEL_TYPE. Pass lintelType (the lintelTypeTag from acad-structural.insert_lintel) to record it on the window's own schedule row. Layer defaults to A-GLAZ.",
        "openings",
        Intent = new[]
        {
            "wstaw okno", "dodaj okno przeciwpozarowe", "insert window",
            "place window RC3 burglary", "casement window", "okno uchylno-rozwierne",
            "okno szpitalne EI30", "okno stale", "fire window EI60"
        },
        RequiresPlugin = true)]
    public static Task<OpeningInsertResult> InsertWindow(IPluginGateway gw, InsertWindowArgs args, CancellationToken ct)
        => OpeningsProxy.CallAsync<InsertWindowArgs, OpeningInsertResult>(gw, "acad.openings.insert_window", args, T_NORMAL, ct);

    [McpTool("insert_opening_generic",
        "Insert any opening block (door or window) by its canonical name (e.g. 'DOOR-FIRE-1200-2100', 'WIN-HOSP-1800-1500'). Generic escape-hatch after list_opening_catalog; most callers prefer insert_door / insert_window.",
        "openings",
        Intent = new[]
        {
            "wstaw otwor z nazwy bloku", "insert opening by block name",
            "place opening by canonical name", "add catalog opening block",
            "generic insert of DOOR-* or WIN-*"
        },
        RequiresPlugin = true)]
    public static Task<OpeningInsertResult> InsertOpeningGeneric(IPluginGateway gw, InsertOpeningGenericArgs args, CancellationToken ct)
        => OpeningsProxy.CallAsync<InsertOpeningGenericArgs, OpeningInsertResult>(gw, "acad.openings.insert_opening_generic", args, T_NORMAL, ct);

    [McpTool("draw_door_by_points",
        "Quick-sketch a door leaf + swing arc without creating a BlockReference. Provide hingePoint (p1) and leafEnd (p2); plugin draws a line p1->p2 plus a 90-deg arc centered at p1. Useful when precise block library is overkill (concept studies, mark-ups). Layer defaults to A-DOOR.",
        "openings",
        Intent = new[]
        {
            "narysuj drzwi szybko", "szkicowy lisc drzwi", "sketch door swing",
            "draw door by two points", "quick door", "mark up door"
        },
        RequiresPlugin = true)]
    public static Task<SketchResult> DrawDoorByPoints(IPluginGateway gw, DrawDoorByPointsArgs args, CancellationToken ct)
        => OpeningsProxy.CallAsync<DrawDoorByPointsArgs, SketchResult>(gw, "acad.openings.draw_door_by_points", args, T_FAST, ct);

    [McpTool("draw_window_by_points",
        "Quick-sketch a window as 2 parallel lines (inner + outer wall face) + a center glass line between jamb1 and jamb2. wallThickness (mm, default 250) controls offset. Layer defaults to A-GLAZ.",
        "openings",
        Intent = new[]
        {
            "narysuj okno szybko", "szkicowe okno", "sketch window",
            "draw window by two points", "quick window", "mark up window"
        },
        RequiresPlugin = true)]
    public static Task<SketchResult> DrawWindowByPoints(IPluginGateway gw, DrawWindowByPointsArgs args, CancellationToken ct)
        => OpeningsProxy.CallAsync<DrawWindowByPointsArgs, SketchResult>(gw, "acad.openings.draw_window_by_points", args, T_FAST, ct);

    [McpTool("cut_wall_for_opening",
        "Split an existing wall (Line or 2-vertex Polyline, referenced by handle) into two segments with a gap between jamb1 and jamb2. Projects both jamb points onto the wall axis; the leftHandle / rightHandle returned identify the surviving pieces. The original wall entity is erased. Fails (tool error) for closed polylines or walls with >2 vertices; use D6 'split_wall_at_opening' for polyline walls.",
        "openings",
        Intent = new[]
        {
            "przetnij sciane dla drzwi", "zrob otwor w scianie", "cut wall for opening",
            "split wall at jambs", "open hole in wall", "rozciac sciane na otwor"
        },
        RequiresPlugin = true)]
    public static Task<CutWallForOpeningResult> CutWallForOpening(IPluginGateway gw, CutWallForOpeningArgs args, CancellationToken ct)
        => OpeningsProxy.CallAsync<CutWallForOpeningArgs, CutWallForOpeningResult>(gw, "acad.openings.cut_wall_for_opening", args, T_NORMAL, ct);

    [McpTool("renumber_openings",
        "Rewrite NUMBER attribute across all doors and/or windows in model-space. kind='doors'|'windows'|'all'. order='insertion' (creation order) | 'spatial' (sort by Y descending then X ascending so numbering reads 'room-by-room'). startAt starts sequence (default 1). Returns change log per entity.",
        "openings",
        Intent = new[]
        {
            "przenumeruj drzwi", "przenumeruj okna", "renumber doors windows",
            "renumber openings spatially", "reset D-001 numbering", "uporzadkuj numeracje otworow"
        },
        RequiresPlugin = true)]
    public static Task<RenumberOpeningsResult> RenumberOpenings(IPluginGateway gw, RenumberOpeningsArgs args, CancellationToken ct)
        => OpeningsProxy.CallAsync<RenumberOpeningsArgs, RenumberOpeningsResult>(gw, "acad.openings.renumber_openings", args, T_NORMAL, ct);

    [McpTool("list_openings_in_model",
        "Enumerate all opening BlockReferences currently in model-space (block names starting with 'DOOR-' or 'WIN-'). kind='doors'|'windows'|'all'. Optional layerFilter. Returns handle, blockName, kind, number, type, width/height, rei, rc, fireClass, acousticDb, leadShielded, roomFrom, roomTo, lintelType, position, rotation, layer. Read-only. Returns COUNT 0 for doors/windows drawn with architecture.insert_door/insert_window (or draw_door_by_points/draw_window_by_points) - those draw plain primitives, not blocks, and are invisible here by design, not a bug. If this comes back empty on a drawing you can SEE doors/windows in, that is almost always the cause.",
        "openings",
        Intent = new[]
        {
            "lista drzwi i okien w modelu", "enumerate openings in model",
            "list placed doors", "find windows on layer", "show openings inventory"
        },
        ReadOnly = true,
        RequiresPlugin = true)]
    public static Task<ListOpeningsInModelResult> ListOpeningsInModel(IPluginGateway gw, ListOpeningsInModelArgs args, CancellationToken ct)
        => OpeningsProxy.CallAsync<ListOpeningsInModelArgs, ListOpeningsInModelResult>(gw, "acad.openings.list_openings_in_model", args, T_FAST, ct);

    [McpTool("export_schedule",
        "Export a door or window schedule to CSV or JSON string (optionally write to disk). kind='doors'|'windows'|'all'. format='csv'|'json'. CSV columns: NUMBER,TYPE,WIDTH_MM,HEIGHT_MM,REI,RC,FIRE_CLASS,ACOUSTIC_DB,LEAD,ROOM_FROM,ROOM_TO,LINTEL_TYPE,LAYER,HANDLE. Returns the rendered content (also written to outputPath when supplied). Read-only.",
        "openings",
        Intent = new[]
        {
            "eksportuj harmonogram drzwi", "eksportuj harmonogram okien",
            "export door schedule CSV", "export window schedule JSON",
            "wygeneruj tabele stolarki", "door and window schedule"
        },
        ReadOnly = true,
        RequiresPlugin = true)]
    public static Task<ExportScheduleResult> ExportSchedule(IPluginGateway gw, ExportScheduleArgs args, CancellationToken ct)
        => OpeningsProxy.CallAsync<ExportScheduleArgs, ExportScheduleResult>(gw, "acad.openings.export_schedule", args, T_SLOW, ct);
}
