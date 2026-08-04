// MCP tools for the acad-annotative category.
//
// The problem this solves: a 2.5 mm text height is right on a 1:50 sheet and half the size it
// should be on 1:100. Without annotative scaling the usual answer is duplicated geometry - one
// set of labels per scale, on layers frozen per viewport. An annotative object instead carries
// one representation PER SCALE, and each viewport draws the one matching its own scale.
//
// Three levels, and confusing them is the main source of "why is my text still wrong":
//   drawing scale list  - which scales EXIST in this drawing (add_scale_to_list, ...)
//   current scale       - CANNOSCALE; what new annotative objects get, and what model space shows
//   per-object scales   - which representations one entity actually carries
// An object only appears at a scale it has a representation for. Setting the current scale does
// not retroactively give existing objects that representation - add_annotation_scale does.
//
// Pairs with acad-viewports: set_viewport_annotation_scale was deferred waiting on this
// category and is now buildable.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Annotative;

public static class AnnotativeTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 20_000;

    // ─────────────── per-object ───────────────

    [McpTool("set_annotative", "Turn the annotative flag on or off for one or more entities. Turning it ON gives the object a representation at the CURRENT annotation scale only - use add_annotation_scale for the others. Turning it OFF collapses it back to a single fixed-size object.", "annotative",
        Intent = new[] { "ustaw obiekt jako adnotacyjny", "wlacz adnotacyjnosc tekstu", "make object annotative",
                         "set annotative flag", "turn annotative off", "obiekt skalowany adnotacyjnie" },
        RequiresPlugin = true)]
    public static Task<AnnotativeAffected> SetAnnotative(IPluginGateway gw, SetAnnotativeArgs args, CancellationToken ct)
        => AnnotativeProxy.CallAsync<SetAnnotativeArgs, AnnotativeAffected>(gw, "acad.annotative.set_annotative", args, T_NORMAL, ct);

    [McpTool("add_annotation_scale", "Give annotative objects a representation at one or more scales, so they appear in viewports set to those scales. An annotative object is invisible in a viewport whose scale it has no representation for - this is the tool that fixes 'my text disappeared on the 1:100 sheet'.", "annotative",
        Intent = new[] { "dodaj skale do obiektu adnotacyjnego", "tekst ma byc widoczny w skali 1:100", "add annotation scale to object",
                         "give object another scale representation", "text missing in viewport scale", "reprezentacja w skali" },
        RequiresPlugin = true)]
    public static Task<AnnotativeAffected> AddAnnotationScale(IPluginGateway gw, ObjectScalesArgs args, CancellationToken ct)
        => AnnotativeProxy.CallAsync<ObjectScalesArgs, AnnotativeAffected>(gw, "acad.annotative.add_annotation_scale", args, T_NORMAL, ct);

    [McpTool("remove_annotation_scale", "Remove scale representations from annotative objects. The object stops appearing in viewports at those scales. An object's last remaining representation cannot be removed - use set_annotative false instead.", "annotative",
        Intent = new[] { "usun skale z obiektu adnotacyjnego", "ukryj tekst w danej skali", "remove annotation scale from object",
                         "drop scale representation", "hide annotative object at scale", "usun reprezentacje skali" },
        RequiresPlugin = true)]
    public static Task<AnnotativeAffected> RemoveAnnotationScale(IPluginGateway gw, ObjectScalesArgs args, CancellationToken ct)
        => AnnotativeProxy.CallAsync<ObjectScalesArgs, AnnotativeAffected>(gw, "acad.annotative.remove_annotation_scale", args, T_NORMAL, ct);

    [McpTool("list_object_annotation_scales", "List which scale representations each given entity carries, plus whether it is annotative at all. Read-only. Call this before wondering why an object is missing from a sheet.", "annotative",
        Intent = new[] { "jakie skale ma obiekt", "wylistuj skale obiektu adnotacyjnego", "list object annotation scales",
                         "which scales does this text have", "show scale representations", "reprezentacje skal encji" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<AnnotativeObjectResult> ListObjectAnnotationScales(IPluginGateway gw, HandlesArgs args, CancellationToken ct)
        => AnnotativeProxy.CallAsync<HandlesArgs, AnnotativeObjectResult>(gw, "acad.annotative.list_object_annotation_scales", args, T_FAST, ct);

    [McpTool("list_annotative_objects", "Enumerate every annotative object in model space with the scales it carries. Pass scale to list only objects having a representation at that scale. Read-only.", "annotative",
        Intent = new[] { "wylistuj obiekty adnotacyjne", "co jest adnotacyjne w rysunku", "list annotative objects",
                         "find all annotative entities", "which objects have scale 1:50", "obiekty skalowane" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<AnnotativeObjectResult> ListAnnotativeObjects(IPluginGateway gw, ScaleFilterArgs args, CancellationToken ct)
        => AnnotativeProxy.CallAsync<ScaleFilterArgs, AnnotativeObjectResult>(gw, "acad.annotative.list_annotative_objects", args, T_NORMAL, ct);

    [McpTool("sync_scale_positions", "Reset every scale representation of the given objects back to the position of the current scale's representation. Use after moving annotation at one scale and wanting the others to follow rather than drift.", "annotative",
        Intent = new[] { "zsynchronizuj pozycje skal", "wyrownaj polozenie reprezentacji", "sync annotative scale positions",
                         "reset scale positions", "make all scales match current", "ujednolic pozycje adnotacji" },
        RequiresPlugin = true)]
    public static Task<AnnotativeAffected> SyncScalePositions(IPluginGateway gw, HandlesArgs args, CancellationToken ct)
        => AnnotativeProxy.CallAsync<HandlesArgs, AnnotativeAffected>(gw, "acad.annotative.sync_scale_positions", args, T_NORMAL, ct);

    // ─────────────── drawing scale list ───────────────

    [McpTool("list_scale_list", "List every annotation scale defined in this drawing, with its paper:drawing ratio and which one is current. Read-only. These are the scales available to add_annotation_scale and to viewports.", "annotative",
        Intent = new[] { "wylistuj skale rysunku", "jakie skale sa dostepne", "list annotation scales",
                         "show drawing scale list", "available scales", "lista skal w rysunku" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<ScaleListResult> ListScaleList(IPluginGateway gw, EmptyAnnotativeArgs args, CancellationToken ct)
        => AnnotativeProxy.CallAsync<EmptyAnnotativeArgs, ScaleListResult>(gw, "acad.annotative.list_scale_list", args, T_FAST, ct);

    [McpTool("add_scale_to_list", "Add an annotation scale to the drawing, e.g. name '1:25' with paperUnits 1 and drawingUnits 25. Idempotent - an existing name is updated rather than duplicated.", "annotative",
        Intent = new[] { "dodaj skale do rysunku", "utworz skale 1:25", "add annotation scale to drawing",
                         "define new scale", "create scale entry", "nowa skala na liscie" },
        RequiresPlugin = true)]
    public static Task<ScaleResult> AddScaleToList(IPluginGateway gw, AddScaleArgs args, CancellationToken ct)
        => AnnotativeProxy.CallAsync<AddScaleArgs, ScaleResult>(gw, "acad.annotative.add_scale_to_list", args, T_NORMAL, ct);

    [McpTool("delete_scale_from_list", "Remove an annotation scale from the drawing's list. Fails if the scale is in use by an annotative object or a viewport, rather than silently orphaning them.", "annotative",
        Intent = new[] { "usun skale z listy", "skasuj skale rysunku", "delete annotation scale",
                         "remove scale from drawing", "drop unused scale", "usun niepotrzebna skale" },
        RequiresPlugin = true)]
    public static Task<AnnotativeAffected> DeleteScaleFromList(IPluginGateway gw, ScaleNameArgs args, CancellationToken ct)
        => AnnotativeProxy.CallAsync<ScaleNameArgs, AnnotativeAffected>(gw, "acad.annotative.delete_scale_from_list", args, T_NORMAL, ct);

    [McpTool("reset_scale_list", "Reset the drawing's scale list to AutoCAD's defaults, dropping custom entries that are not in use. Scales still referenced by objects or viewports are kept and reported.", "annotative",
        Intent = new[] { "zresetuj liste skal", "przywroc domyslne skale", "reset scale list",
                         "restore default scales", "clean up scale list", "wyczysc liste skal" },
        RequiresPlugin = true)]
    public static Task<ScaleListResult> ResetScaleList(IPluginGateway gw, EmptyAnnotativeArgs args, CancellationToken ct)
        => AnnotativeProxy.CallAsync<EmptyAnnotativeArgs, ScaleListResult>(gw, "acad.annotative.reset_scale_list", args, T_NORMAL, ct);

    // ─────────────── current scale / visibility ───────────────

    [McpTool("set_current_annotation_scale", "Set CANNOSCALE - the scale new annotative objects are created at, and the one model space displays. Does NOT retroactively add this scale to existing objects; use add_annotation_scale for that.", "annotative",
        Intent = new[] { "ustaw biezaca skale adnotacji", "zmien cannoscale", "set current annotation scale",
                         "change active annotation scale", "set model space scale", "aktualna skala adnotacyjna" },
        RequiresPlugin = true)]
    public static Task<ScaleResult> SetCurrentAnnotationScale(IPluginGateway gw, ScaleNameArgs args, CancellationToken ct)
        => AnnotativeProxy.CallAsync<ScaleNameArgs, ScaleResult>(gw, "acad.annotative.set_current_annotation_scale", args, T_NORMAL, ct);

    [McpTool("get_current_annotation_scale", "Return the current annotation scale (CANNOSCALE) with its ratio. Read-only.", "annotative",
        Intent = new[] { "jaka jest biezaca skala adnotacji", "pokaz cannoscale", "get current annotation scale",
                         "what annotation scale is active", "current scale", "aktualna skala" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<ScaleResult> GetCurrentAnnotationScale(IPluginGateway gw, EmptyAnnotativeArgs args, CancellationToken ct)
        => AnnotativeProxy.CallAsync<EmptyAnnotativeArgs, ScaleResult>(gw, "acad.annotative.get_current_annotation_scale", args, T_FAST, ct);

    [McpTool("set_annotation_visibility", "ANNOALLVISIBLE: show every annotative object regardless of whether it has a representation at the current scale, or show only those that do. Turning it OFF is how you check a sheet for annotation that will be missing at that scale.", "annotative",
        Intent = new[] { "pokaz wszystkie adnotacje", "annoallvisible", "set annotation visibility",
                         "show only current scale annotations", "hide annotations without this scale", "widocznosc adnotacji" },
        RequiresPlugin = true)]
    public static Task<AnnotationVisibilityResult> SetAnnotationVisibility(IPluginGateway gw, BoolFlagArgs args, CancellationToken ct)
        => AnnotativeProxy.CallAsync<BoolFlagArgs, AnnotationVisibilityResult>(gw, "acad.annotative.set_annotation_visibility", args, T_FAST, ct);

    [McpTool("set_auto_add_scale", "ANNOAUTOSCALE: whether annotative objects automatically gain a representation when the current annotation scale changes. On is convenient while drafting and dangerous on an issued set, because objects silently acquire scales nobody asked for.", "annotative",
        Intent = new[] { "automatyczne dodawanie skal", "annoautoscale", "set auto add annotation scale",
                         "auto add scale to annotative objects", "stop objects gaining scales", "automatyczna skala" },
        RequiresPlugin = true)]
    public static Task<AnnotationVisibilityResult> SetAutoAddScale(IPluginGateway gw, BoolFlagArgs args, CancellationToken ct)
        => AnnotativeProxy.CallAsync<BoolFlagArgs, AnnotationVisibilityResult>(gw, "acad.annotative.set_auto_add_scale", args, T_FAST, ct);

    [McpTool("get_annotation_settings", "Report ANNOALLVISIBLE and ANNOAUTOSCALE, decoded. Read-only.", "annotative",
        Intent = new[] { "pokaz ustawienia adnotacji", "jakie sa annoallvisible i annoautoscale", "get annotation settings",
                         "read annotative sysvars", "annotation visibility settings", "ustawienia adnotacyjne" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<AnnotationVisibilityResult> GetAnnotationSettings(IPluginGateway gw, EmptyAnnotativeArgs args, CancellationToken ct)
        => AnnotativeProxy.CallAsync<EmptyAnnotativeArgs, AnnotationVisibilityResult>(gw, "acad.annotative.get_annotation_settings", args, T_FAST, ct);
}
