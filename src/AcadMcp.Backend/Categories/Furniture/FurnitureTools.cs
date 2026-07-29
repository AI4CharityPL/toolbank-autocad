// AutoCAD acad-furniture category. 10 tools covering parametric furniture blocks
// for hospitals and offices: beds, chairs, desks, cabinets, sofas, tables,
// generic insert, catalog enumeration, model-space enumeration, per-room
// populators. Blocks are generated on demand (first call creates the
// BlockTableRecord; subsequent calls insert BlockReferences) so the library
// ships zero binary DWG assets.
//
// Thin proxies over plugin handlers "acad.furniture.<verb>".
// Rules: 19-tool-implementation-pattern, 64-furniture-density-per-room.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Furniture;

public static class FurnitureTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;
    private const int T_SLOW = 30_000;

    [McpTool("list_furniture_catalog",
        "Enumerate the built-in furniture block catalog (hospital + office + residential). Read-only. Returns name, category (bed/chair/desk/cabinet/sofa/table/misc), domain (hospital/office/residential), default width/depth in mm, and a one-line description.",
        "furniture",
        Intent = new[]
        {
            "lista mebli katalog", "co mamy w katalogu mebli", "list furniture catalog",
            "show available furniture", "enumerate furniture blocks", "katalog bloczkow meblowych"
        },
        ReadOnly = true,
        RequiresPlugin = true)]
    public static Task<ListFurnitureCatalogResult> ListFurnitureCatalog(IPluginGateway gw, ListFurnitureCatalogArgs args, CancellationToken ct)
        => FurnitureProxy.CallAsync<ListFurnitureCatalogArgs, ListFurnitureCatalogResult>(gw, "acad.furniture.list_furniture_catalog", args, T_FAST, ct);

    [McpTool("insert_furniture",
        "Insert any catalog furniture block by its canonical name (e.g. 'FURN-BED-STD', 'FURN-CHAIR-OFFICE'). Generic entry-point used after list_furniture_catalog; most callers prefer the specialised insert_bed/insert_chair/... tools that infer a type.",
        "furniture",
        Intent = new[]
        {
            "wstaw mebel z katalogu", "insert furniture block by name",
            "place furniture from catalog", "add catalog block to drawing",
            "generic insert of any FURN-* block"
        },
        RequiresPlugin = true)]
    public static Task<FurnitureInsertResult> InsertFurniture(IPluginGateway gw, InsertFurnitureArgs args, CancellationToken ct)
        => FurnitureProxy.CallAsync<InsertFurnitureArgs, FurnitureInsertResult>(gw, "acad.furniture.insert_furniture", args, T_NORMAL, ct);

    [McpTool("insert_bed",
        "Insert a hospital/residential bed. Types: 'standard' (900x2000), 'icu' (1000x2200 + head-monitor strip), 'bariatric' (1200x2200), 'pediatric' (700x1500), 'or' (operating table 550x2100 + trendelenburg), 'labour' (1050x2300 + stirrups). Defaults to layer A-FURN-BED + attributes {inv_id, type, room}.",
        "furniture",
        Intent = new[]
        {
            "wstaw lozko", "dodaj lozko ICU", "wstaw stol operacyjny", "insert bed",
            "place ICU bed", "add hospital bed", "operating table", "lozko pediatryczne",
            "lozko bariatryczne", "labour bed"
        },
        RequiresPlugin = true)]
    public static Task<FurnitureInsertResult> InsertBed(IPluginGateway gw, InsertBedArgs args, CancellationToken ct)
        => FurnitureProxy.CallAsync<InsertBedArgs, FurnitureInsertResult>(gw, "acad.furniture.insert_bed", args, T_NORMAL, ct);

    [McpTool("insert_chair",
        "Insert a chair. Types: 'office' (550x550 swivel), 'armchair' (800x800), 'stool' (450x450 round), 'examination' (600x600 medical stool), 'wheelchair' (700x1100). Defaults to layer A-FURN-CHR.",
        "furniture",
        Intent = new[]
        {
            "wstaw krzeslo", "dodaj fotel", "insert chair", "place office chair",
            "wheelchair", "stolek badawczy", "armchair"
        },
        RequiresPlugin = true)]
    public static Task<FurnitureInsertResult> InsertChair(IPluginGateway gw, InsertChairArgs args, CancellationToken ct)
        => FurnitureProxy.CallAsync<InsertChairArgs, FurnitureInsertResult>(gw, "acad.furniture.insert_chair", args, T_NORMAL, ct);

    [McpTool("insert_desk",
        "Insert a desk at given position with configurable width/depth. Types: 'office', 'reception' (L-shaped counter 2400x800 + 1200x400 overhang), 'nurse-station' (3000x900 with raised edge). Defaults width=1600 depth=800 layer=A-FURN-DSK.",
        "furniture",
        Intent = new[]
        {
            "wstaw biurko", "dodaj stanowisko pielegniarki", "insert desk",
            "reception desk", "nurse station", "biurko recepcyjne"
        },
        RequiresPlugin = true)]
    public static Task<FurnitureInsertResult> InsertDesk(IPluginGateway gw, InsertDeskArgs args, CancellationToken ct)
        => FurnitureProxy.CallAsync<InsertDeskArgs, FurnitureInsertResult>(gw, "acad.furniture.insert_desk", args, T_NORMAL, ct);

    [McpTool("insert_cabinet",
        "Insert a cabinet / storage unit. Types: 'storage' (generic), 'medical' (with glass-door indicator), 'file' (with drawer lines), 'wardrobe' (with hanger-rail indicator). Configurable width/depth. Defaults width=800 depth=400 layer=A-FURN-CBT.",
        "furniture",
        Intent = new[]
        {
            "wstaw szafe", "dodaj szafke medyczna", "insert cabinet", "file cabinet",
            "wardrobe", "medical cabinet", "szafa na dokumenty"
        },
        RequiresPlugin = true)]
    public static Task<FurnitureInsertResult> InsertCabinet(IPluginGateway gw, InsertCabinetArgs args, CancellationToken ct)
        => FurnitureProxy.CallAsync<InsertCabinetArgs, FurnitureInsertResult>(gw, "acad.furniture.insert_cabinet", args, T_NORMAL, ct);

    [McpTool("insert_sofa",
        "Insert a sofa. Types: 'lounge' (cushioned), 'clinic' (waiting-room, vinyl). Seats: 2, 3. Defaults seats=3 type=lounge layer=A-FURN-SFA.",
        "furniture",
        Intent = new[]
        {
            "wstaw sofe", "dodaj kanape poczekalnia", "insert sofa", "couch",
            "waiting room sofa", "sofa kliniczna"
        },
        RequiresPlugin = true)]
    public static Task<FurnitureInsertResult> InsertSofa(IPluginGateway gw, InsertSofaArgs args, CancellationToken ct)
        => FurnitureProxy.CallAsync<InsertSofaArgs, FurnitureInsertResult>(gw, "acad.furniture.insert_sofa", args, T_NORMAL, ct);

    [McpTool("insert_table",
        "Insert a table. Shape: 'rectangle' / 'round' / 'square'. Types: 'meeting', 'coffee', 'dining', 'exam' (medical exam table - height strip + roll paper slot). Configurable width/depth. Defaults rectangle 1200x800 meeting, layer=A-FURN-TBL.",
        "furniture",
        Intent = new[]
        {
            "wstaw stol", "dodaj stol konferencyjny", "insert table", "round table",
            "coffee table", "meeting table", "stol badawczy"
        },
        RequiresPlugin = true)]
    public static Task<FurnitureInsertResult> InsertTable(IPluginGateway gw, InsertTableArgs args, CancellationToken ct)
        => FurnitureProxy.CallAsync<InsertTableArgs, FurnitureInsertResult>(gw, "acad.furniture.insert_table", args, T_NORMAL, ct);

    [McpTool("populate_room",
        "Auto-populate a room with a furniture preset. Room is identified either by a closed polyline handle OR explicit bbox (min+max). Presets: 'ward-room' (2 beds + 2 nightstands + 1 armchair), 'icu-room' (1 ICU-bed + monitor cabinet + visitor chair), 'or-room' (OR-table + anaesthesia + instrument trolley), 'office' (desk + chair + file cabinet), 'reception' (reception-desk + 3 waiting chairs), 'waiting' (3 sofas + 1 coffee-table), 'consult' (desk + 2 chairs + exam table + cabinet). Returns handles of inserted items plus per-item layer assignment warnings.",
        "furniture",
        Intent = new[]
        {
            "zaludnij pokoj meblami", "wypelnij meblami preset", "populate room with preset",
            "auto-furnish room", "wstaw meble w pokoj przez preset", "ICU room preset",
            "sala chorych preset", "gabinet lekarski preset", "office furniture preset"
        },
        RequiresPlugin = true)]
    public static Task<PopulateRoomResult> PopulateRoom(IPluginGateway gw, PopulateRoomArgs args, CancellationToken ct)
        => FurnitureProxy.CallAsync<PopulateRoomArgs, PopulateRoomResult>(gw, "acad.furniture.populate_room", args, T_SLOW, ct);

    [McpTool("list_furniture_in_model",
        "Enumerate all furniture BlockReferences currently in model-space (block names starting with 'FURN-'). Optionally filter by layer or by exact block name. Returns handle, block name, layer, position, rotation and any {inv_id, type, note} attribute values. Read-only.",
        "furniture",
        Intent = new[]
        {
            "lista mebli w rysunku", "enumerate furniture in model", "list placed furniture",
            "find furniture on layer", "show all beds in drawing"
        },
        ReadOnly = true,
        RequiresPlugin = true)]
    public static Task<ListFurnitureInModelResult> ListFurnitureInModel(IPluginGateway gw, ListFurnitureInModelArgs args, CancellationToken ct)
        => FurnitureProxy.CallAsync<ListFurnitureInModelArgs, ListFurnitureInModelResult>(gw, "acad.furniture.list_furniture_in_model", args, T_FAST, ct);
}
