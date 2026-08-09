// AutoCAD acad-plumbing category. 8 tools covering sanitary fixtures for
// hospitals, offices and residential buildings. All fixtures comply with
// Polish WT-2019 (Warunki Techniczne §79, §82, §86) and accessible variants
// with PN-EN 17210 (toilet footprint 1500×1800 min + 80 cm approach +
// grab bars + 600 mm sink approach).
//
// Blocks are generated on demand on first use; zero binary DWG assets ship.
// Thin proxies over plugin handlers "acad.plumbing.<verb>".
//
// Rules: 19-tool-implementation-pattern, 63-sanitary-fixtures-wt.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Plumbing;

public static class PlumbingTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;
    private const int T_SLOW = 30_000;

    [McpTool("list_plumbing_catalog",
        "Enumerate the built-in sanitary-fixture catalog (WCs, basins, showers, bathtubs, urinals, sinks + accessible variants per PN-EN 17210). Returns name, category, domain (hospital/office/residential), default width/depth in mm, accessible flag, Polish/EN normative reference, and description. Read-only.",
        "plumbing",
        Intent = new[]
        {
            "lista urzadzen sanitarnych", "katalog plumbing", "list plumbing catalog",
            "show available plumbing fixtures", "enumerate sanitary blocks",
            "co mamy w katalogu lazienkowym", "what sanitary fixtures available"
        },
        ReadOnly = true,
        RequiresPlugin = true)]
    public static Task<ListPlumbingCatalogResult> ListPlumbingCatalog(IPluginGateway gw, ListPlumbingCatalogArgs args, CancellationToken ct)
        => PlumbingProxy.CallAsync<ListPlumbingCatalogArgs, ListPlumbingCatalogResult>(gw, "acad.plumbing.list_plumbing_catalog", args, T_FAST, ct);

    [McpTool("insert_plumbing",
        "Insert any catalog sanitary block by fully-qualified name (e.g. 'PLMB-WC-FS', 'PLMB-BSN-ACC-700-550'). Generic entry-point; most callers prefer specialised insert_wc / insert_basin / insert_shower / ... which map type + size to the canonical name.",
        "plumbing",
        Intent = new[]
        {
            "wstaw urzadzenie sanitarne katalog", "insert plumbing block by name",
            "place sanitary fixture from catalog", "add catalog plumbing to drawing",
            "generic sanitary fixture insert"
        },
        RequiresPlugin = true)]
    public static Task<PlumbingInsertResult> InsertPlumbing(IPluginGateway gw, InsertPlumbingArgs args, CancellationToken ct)
        => PlumbingProxy.CallAsync<InsertPlumbingArgs, PlumbingInsertResult>(gw, "acad.plumbing.insert_plumbing", args, T_NORMAL, ct);

    [McpTool("insert_wc",
        "Insert a toilet / WC. Types: 'floor-standing' (PLMB-WC-FS 370x650), 'wall-hung' (PLMB-WC-WH 370x540 — lower footprint), 'bidet-combo' (PLMB-WC-BID 370x550 with bidet spray). Set accessible=true for PN-EN 17210 compliant unit (PLMB-WC-ACC 800x800 with grab-bar indicators). Defaults floor-standing. Layer A-PLMB-WC.",
        "plumbing",
        Intent = new[]
        {
            "wstaw WC", "dodaj miska ustepowa", "insert toilet", "insert WC",
            "place accessible WC", "wstaw WC dla niepelnosprawnych", "wall-hung toilet",
            "bidet kombi"
        },
        RequiresPlugin = true)]
    public static Task<PlumbingInsertResult> InsertWc(IPluginGateway gw, InsertWcArgs args, CancellationToken ct)
        => PlumbingProxy.CallAsync<InsertWcArgs, PlumbingInsertResult>(gw, "acad.plumbing.insert_wc", args, T_NORMAL, ct);

    [McpTool("insert_basin",
        "Insert a wash basin (umywalka). Types: 'standard' (600x450), 'double' (1200x450 — two faucet positions). Set accessible=true for PN-EN 17210 700x550 with knee-clearance marker. Width is configurable. Layer A-PLMB-BSN.",
        "plumbing",
        Intent = new[]
        {
            "wstaw umywalke", "dodaj podwojna umywalke", "insert wash basin",
            "place accessible basin", "place double basin", "umywalka dla niepelnosprawnych",
            "wall-mounted basin", "umywalka dwustanowiskowa"
        },
        RequiresPlugin = true)]
    public static Task<PlumbingInsertResult> InsertBasin(IPluginGateway gw, InsertBasinArgs args, CancellationToken ct)
        => PlumbingProxy.CallAsync<InsertBasinArgs, PlumbingInsertResult>(gw, "acad.plumbing.insert_basin", args, T_NORMAL, ct);

    [McpTool("insert_shower",
        "Insert a shower (prysznic). Shape: 'square' / 'rectangle'. walkIn=true draws a walk-in (open-side curtain indicator, no raised tray). Standard sizes 800x800, 900x900, 1200x900 walk-in. Drain indicator always at the geometric centre. Layer A-PLMB-SHW.",
        "plumbing",
        Intent = new[]
        {
            "wstaw prysznic", "dodaj kabine prysznicowa", "insert shower",
            "walk-in shower", "prysznic bezbarierowy", "shower tray",
            "square shower", "rectangular shower"
        },
        RequiresPlugin = true)]
    public static Task<PlumbingInsertResult> InsertShower(IPluginGateway gw, InsertShowerArgs args, CancellationToken ct)
        => PlumbingProxy.CallAsync<InsertShowerArgs, PlumbingInsertResult>(gw, "acad.plumbing.insert_shower", args, T_NORMAL, ct);

    [McpTool("insert_bathtub",
        "Insert a bathtub (wanna). Types: 'standard' (1700x700), 'mini' (1500x700), 'corner' (1400x1400 quarter-round + splash wall). Configurable width/depth. Draws bathtub outline + drain + faucet-end indicator. Layer A-PLMB-BT.",
        "plumbing",
        Intent = new[]
        {
            "wstaw wanne", "dodaj wanne narozna", "insert bathtub",
            "corner bathtub", "wanna prostokatna", "rectangle bathtub",
            "mini bathtub", "freestanding bathtub"
        },
        RequiresPlugin = true)]
    public static Task<PlumbingInsertResult> InsertBathtub(IPluginGateway gw, InsertBathtubArgs args, CancellationToken ct)
        => PlumbingProxy.CallAsync<InsertBathtubArgs, PlumbingInsertResult>(gw, "acad.plumbing.insert_bathtub", args, T_NORMAL, ct);

    [McpTool("insert_urinal",
        "Insert a urinal (pisuar). Standard 380x340 at sprayer height 650 mm (PLMB-UR-STD). Set accessible=true for lower-rim 380x450 variant at 450 mm height (PLMB-UR-ACC, PN-EN 17210 §U4.3). Layer A-PLMB-UR.",
        "plumbing",
        Intent = new[]
        {
            "wstaw pisuar", "dodaj pisuar dla niepelnosprawnych", "insert urinal",
            "wall-hung urinal", "accessible urinal", "pisuar akustyczny",
            "pisuar o obnizonej wysokosci"
        },
        RequiresPlugin = true)]
    public static Task<PlumbingInsertResult> InsertUrinal(IPluginGateway gw, InsertUrinalArgs args, CancellationToken ct)
        => PlumbingProxy.CallAsync<InsertUrinalArgs, PlumbingInsertResult>(gw, "acad.plumbing.insert_urinal", args, T_NORMAL, ct);

    [McpTool("populate_bathroom",
        "Places SANITARY FITTINGS and their clearances. For ordinary furniture in an ordinary room, that is furniture.populate_room. Auto-populate a bathroom/WC with a sanitary preset. Room identified by closed-polyline handle OR bbox. Presets: 'wc-public' (WC + basin, single cubicle), 'wc-accessible' (PN-EN 17210 accessible WC + accessible basin + grab-bar markers, min 1500x1800), 'bathroom-residential' (WC + basin + bathtub OR shower), 'bathroom-hospital-patient' (wall-hung WC + basin + walk-in shower + grab bars), 'shower-room' (shower + basin), 'wc-block-staff' (2x WC + 2x basin + urinal). accessible=true overrides with accessible variants.",
        "plumbing",
        Intent = new[]
        {
            "zaludnij lazienke preset", "wypelnij WC preset", "populate bathroom with preset",
            "auto-furnish WC", "lazienka szpitalna preset", "WC ogolnodostepne preset",
            "wc accessible preset", "bathroom residential preset", "lazienka szpitalna pacjenta"
        },
        RequiresPlugin = true)]
    public static Task<PopulateBathroomResult> PopulateBathroom(IPluginGateway gw, PopulateBathroomArgs args, CancellationToken ct)
        => PlumbingProxy.CallAsync<PopulateBathroomArgs, PopulateBathroomResult>(gw, "acad.plumbing.populate_bathroom", args, T_SLOW, ct);

    [McpTool("list_plumbing_in_model",
        "Enumerate all sanitary BlockReferences currently in model-space (block names starting with 'PLMB-'). Filter by layer or exact block name. Returns handle, block name, layer, position, rotation, INV_ID / TYPE attribute values + ACCESSIBLE flag. Read-only.",
        "plumbing",
        Intent = new[]
        {
            "lista urzadzen sanitarnych w rysunku", "enumerate plumbing in model",
            "list placed sanitary fixtures", "find plumbing on layer",
            "show all WCs in drawing", "show all basins in drawing"
        },
        ReadOnly = true,
        RequiresPlugin = true)]
    public static Task<ListPlumbingInModelResult> ListPlumbingInModel(IPluginGateway gw, ListPlumbingInModelArgs args, CancellationToken ct)
        => PlumbingProxy.CallAsync<ListPlumbingInModelArgs, ListPlumbingInModelResult>(gw, "acad.plumbing.list_plumbing_in_model", args, T_FAST, ct);
}
