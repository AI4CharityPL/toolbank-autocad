// AutoCAD acad-selection category. 12 tools covering selection by criteria,
// window/crossing/fence/polygon picks, named selection sets and filtering.
// All tools are read-only and idempotent.
//
// Rules: 19-tool-implementation-pattern.md, 20..25.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Selection;

public static class SelectionTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;

    [McpTool("select_all", "Select every entity currently in model space (no filtering).", "selection",
        Intent = new[] { "zaznacz wszystko", "select all entities", "select everything", "wszystkie obiekty", "all model space entities" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<SelectionResult> SelectAll(IPluginGateway gw, EmptyArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<EmptyArgs, SelectionResult>(gw, "acad.selection.select_all", args, T_NORMAL, ct);

    [McpTool("select_by_layer", "Select all entities on the given layer. Optionally restrict by frozen/thawed state.", "selection",
        Intent = new[] { "zaznacz po warstwie", "select by layer", "all entities on layer", "obiekty na warstwie", "filter by layer" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<SelectionResult> SelectByLayer(IPluginGateway gw, ByLayerArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<ByLayerArgs, SelectionResult>(gw, "acad.selection.select_by_layer", args, T_NORMAL, ct);

    [McpTool("select_by_color", "Select every entity whose colour matches, given either as true RGB or an ACI index from 1 to 255. This matches the colour as SET ON THE ENTITY, so anything drawn ByLayer will not match a search for the layer colour it appears in - which is the usual surprise here. To gather everything on a layer, use select_by_layer instead.", "selection",
        Intent = new[] { "zaznacz po kolorze", "select by color", "all entities of color", "filter entities by color", "select red entities" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<SelectionResult> SelectByColor(IPluginGateway gw, ByColorArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<ByColorArgs, SelectionResult>(gw, "acad.selection.select_by_color", args, T_NORMAL, ct);

    [McpTool("select_by_type", "Select entities by AutoCAD DXF entity name (e.g. \"LINE\", \"LWPOLYLINE\", \"CIRCLE\", \"3DSOLID\", \"INSERT\").", "selection",
        Intent = new[] { "zaznacz po typie", "select by entity type", "all lines", "all polylines", "select dxf type" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<SelectionResult> SelectByType(IPluginGateway gw, ByTypeArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<ByTypeArgs, SelectionResult>(gw, "acad.selection.select_by_type", args, T_NORMAL, ct);

    [McpTool("select_by_handle", "Resolve a list of entity handles into a single selection result. Validates each handle exists.", "selection",
        Intent = new[] { "zaznacz po uchwytach", "select by handle", "lookup by handles", "resolve handles", "find entities by handle" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<SelectionResult> SelectByHandle(IPluginGateway gw, ByHandleArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<ByHandleArgs, SelectionResult>(gw, "acad.selection.select_by_handle", args, T_FAST, ct);

    [McpTool("select_window", "Select entities fully inside (or, with crossing=true, intersecting) the WCS axis-aligned window from min to max.", "selection",
        Intent = new[] { "zaznacz oknem", "select by window", "window selection", "crossing selection", "make a window selection active" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<SelectionResult> SelectWindow(IPluginGateway gw, WindowArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<WindowArgs, SelectionResult>(gw, "acad.selection.select_window", args, T_NORMAL, ct);

    [McpTool("select_fence", "Select entities crossing a polyline fence defined by an ordered vertex list.", "selection",
        Intent = new[] { "zaznacz plotem", "select by fence", "fence selection", "polyline fence pick", "select crossing fence" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<SelectionResult> SelectFence(IPluginGateway gw, FenceArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<FenceArgs, SelectionResult>(gw, "acad.selection.select_fence", args, T_NORMAL, ct);

    [McpTool("select_polygon", "Select entities inside (crossing=false) or intersecting (crossing=true) a closed polygonal region.", "selection",
        Intent = new[] { "zaznacz wielokatem", "select by polygon", "polygon window", "polygon crossing", "select inside polygon" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<SelectionResult> SelectPolygon(IPluginGateway gw, PolygonArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<PolygonArgs, SelectionResult>(gw, "acad.selection.select_polygon", args, T_NORMAL, ct);

    [McpTool("filter_entities", "Apply an additional layer/type/color filter to a candidate set (or to all of model space if no handles supplied).", "selection",
        Intent = new[] { "filtruj zaznaczenie", "filter selection", "narrow selection by criteria", "filter entities", "apply filter to selection" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<SelectionResult> FilterEntitiesTool(IPluginGateway gw, FilterEntities args, CancellationToken ct)
        => SelectionProxy.CallAsync<FilterEntities, SelectionResult>(gw, "acad.selection.filter_entities", args, T_NORMAL, ct);

    [McpTool("save_selection_set", "Save a list of entity handles under a named selection set (stored in the AcadMcp xrecord dictionary on the drawing).", "selection",
        Intent = new[] { "zapisz zestaw selekcji", "save selection set", "ssget save", "store named selection", "save selection by name" },
        RequiresPlugin = true)]
    public static Task<CountResult> SaveSelectionSet(IPluginGateway gw, SaveSetArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<SaveSetArgs, CountResult>(gw, "acad.selection.save_selection_set", args, T_FAST, ct);

    [McpTool("load_selection_set", "Load a previously saved named selection set and return its handles. Validates each handle still exists.", "selection",
        Intent = new[] { "wczytaj zestaw selekcji", "load selection set", "ssget load", "restore named selection", "get saved selection by name" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<SetMembersResult> LoadSelectionSet(IPluginGateway gw, LoadSetArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<LoadSetArgs, SetMembersResult>(gw, "acad.selection.load_selection_set", args, T_FAST, ct);

    [McpTool("count_entities", "Count entities in model space (optionally filtered by DXF type).", "selection",
        Intent = new[] { "policz obiekty", "count entities", "how many entities", "count by type", "ile obiektow" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<CountResult> CountEntities(IPluginGateway gw, ByTypeArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<ByTypeArgs, CountResult>(gw, "acad.selection.count_entities", args, T_FAST, ct);

    // ── Phase 3.4 extensions ──
    //
    // quick_select_by_property is deliberately NOT here: filter_entities above and
    // data.query_by_property already cover it, and a third name for the same operation would only
    // make a router choose badly. select_previous is not here either - AutoCAD's previous
    // selection is the USER's, and nothing an agent does through this bank creates one, so the
    // tool would almost always answer nothing; save_selection_set is the honest equivalent.

    [McpTool("select_similar", "Find every entity like a reference one - AutoCAD's SELECTSIMILAR. Object class always has to match; `matchLayer` also matters by default, while `matchColor` and `matchLinetype` do not, which mirrors AutoCAD's own default. What counted as similar is REPORTED in the result rather than left implicit, because 'similar' is a choice and two people would make it differently. The reference entity is included in the result, since it is similar to itself and leaving it out would make the count disagree with what you see on screen. Read-only. Model space only.", "selection",
        Intent = new[] { "select similar objects", "find everything like this one",
                         "zaznacz podobne obiekty", "select all entities like this",
                         "znajdz obiekty podobne do tego", "select all the same kind of thing",
                         "selectsimilar" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<SimilarResult> SelectSimilar(IPluginGateway gw, SimilarArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<SimilarArgs, SimilarResult>(gw, "acad.selection.select_similar", args, T_NORMAL, ct);

    [McpTool("select_by_area_range", "Find closed curves whose enclosed area falls in a range - give min, max, or both, inclusive. Read-only. IMPORTANT for reading the result: only CLOSED curves have an area, so the result reports how many of the scanned entities were `measurable` at all. Without that number a count of zero would not distinguish 'nothing in range' from 'nothing in this drawing has an area'. A self-intersecting closed polyline reports the absolute value AutoCAD computes, which is not the area a person would measure by hand.", "selection",
        Intent = new[] { "select by area", "find shapes bigger than this area",
                         "zaznacz obiekty po polu powierzchni", "find rooms over 20 square metres",
                         "znajdz zamkniete ksztalty o danej powierzchni", "select small areas",
                         "filter entities by area" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<RangeResult> SelectByAreaRange(IPluginGateway gw, RangeArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<RangeArgs, RangeResult>(gw, "acad.selection.select_by_area_range", args, T_NORMAL, ct);

    [McpTool("select_by_length_range", "Find curves whose length falls in a range - give min, max, or both, inclusive. Read-only. Only CURVES have a length: a block insert or a piece of text does not, so the result reports how many entities were `measurable`, which is what tells a count of zero apart from a drawing full of things that cannot be measured. For a CLOSED curve the length reported is the perimeter, not zero.", "selection",
        Intent = new[] { "select by length", "find lines longer than this",
                         "zaznacz obiekty po dlugosci", "find all short segments",
                         "znajdz linie o danej dlugosci", "select curves in a length range",
                         "filter entities by length" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<RangeResult> SelectByLengthRange(IPluginGateway gw, RangeArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<RangeArgs, RangeResult>(gw, "acad.selection.select_by_length_range", args, T_NORMAL, ct);

    [McpTool("select_duplicates", "Report entities that look like duplicates of each other - the doubled-up lines OVERKILL exists for. Read-only: it finds and reports, it does NEVER deletes. Each group names one entity to `keep` and lists the rest as `duplicates`, so the handles can be passed to modify.delete_entities once you have looked at them. Duplicates are judged by object class, layer and bounding box within a tolerance, which is a HEURISTIC and is described as one - two different splines that happen to share a bounding box will be reported together, so read a group before acting on it.", "selection",
        Intent = new[] { "find duplicate entities", "are there doubled up lines",
                         "znajdz duplikaty obiektow", "find overlapping copies",
                         "sprawdz czy sa zdublowane linie", "overkill",
                         "clean up duplicate geometry" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<DuplicatesResult> SelectDuplicates(IPluginGateway gw, DuplicatesArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<DuplicatesArgs, DuplicatesResult>(gw, "acad.selection.select_duplicates", args, T_NORMAL, ct);

    [McpTool("select_last", "Return the entities most recently ADDED to model space - `count` of them, one by default. Read-only. This is the dependable meaning of 'last' for an agent: model space enumerates in creation order, and nothing this bank does creates a UI selection. AutoCAD's own Editor.SelectLast is consulted as well and reported SEPARATELY, so you can see when it has something and when it does not; in a scripted session it is usually empty, which is a fact about the editor rather than an error. Use this to grab what you have just drawn without tracking handles yourself.", "selection",
        Intent = new[] { "select the last thing i drew", "what did i just create",
                         "zaznacz ostatnio narysowany obiekt", "get the most recent entities",
                         "ostatnio dodane obiekty na rysunku", "select last",
                         "the entity i just added" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<LastResult> SelectLast(IPluginGateway gw, LastArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<LastArgs, LastResult>(gw, "acad.selection.select_last", args, T_NORMAL, ct);

    [McpTool("hide_objects", "Hide the named entities - AutoCAD's HIDEOBJECTS. They are STILL THERE: not erased, still found by the selection tools, and brought back by unisolate_objects. Each entity is read back after being hidden, and any that were already hidden are counted separately rather than reported as newly hidden.", "selection",
        Intent = new[] { "hide these objects", "make these entities invisible",
                         "ukryj te obiekty", "hide the selected entities",
                         "schowaj obiekty na rysunku", "hideobjects",
                         "get these out of the way" },
        RequiresPlugin = true)]
    public static Task<HideResult> HideObjects(IPluginGateway gw, HandlesArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<HandlesArgs, HideResult>(gw, "acad.selection.hide_objects", args, T_NORMAL, ct);

    [McpTool("isolate_objects", "Show only the named entities and hide everything else in model space - AutoCAD's ISOLATEOBJECTS. The named entities are made VISIBLE if they were hidden, since isolating something and leaving it hidden would be the wrong answer. Nothing is erased and unisolate_objects brings it all back. Model space only: entities in a layout are untouched. Refuses if any named handle is not in model space, rather than silently isolating fewer things than asked for.", "selection",
        Intent = new[] { "isolate these objects", "show only these entities",
                         "izoluj te obiekty", "hide everything except this",
                         "pokaz tylko te obiekty na rysunku", "isolateobjects",
                         "focus on just these" },
        RequiresPlugin = true)]
    public static Task<IsolateResult> IsolateObjects(IPluginGateway gw, HandlesArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<HandlesArgs, IsolateResult>(gw, "acad.selection.isolate_objects", args, T_NORMAL, ct);

    [McpTool("unisolate_objects", "Make everything in model space visible again - AutoCAD's UNISOLATEOBJECTS. IMPORTANT: it shows EVERYTHING; it does not restore some earlier state. Anything that was hidden for its own reasons before you isolated will therefore also reappear, and there is no operation that puts back exactly what was hidden before. That is the behaviour, not a defect. Reports how many were shown and how many were already visible.", "selection",
        Intent = new[] { "unisolate everything", "show all hidden objects again",
                         "pokaz wszystkie ukryte obiekty", "undo the isolate",
                         "odkryj obiekty na rysunku", "unisolateobjects",
                         "bring back what i hid" },
        RequiresPlugin = true)]
    public static Task<UnisolateResult> UnisolateObjects(IPluginGateway gw, SelExtNoArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<SelExtNoArgs, UnisolateResult>(gw, "acad.selection.unisolate_objects", args, T_NORMAL, ct);

    [McpTool("create_selection_filter", "Save a named set of selection criteria in the drawing, to be reused with apply_saved_filter. Criteria are layer, objectClass, colorIndex and a min/max range whose meaning is set by `rangeKind` - area or length. At least one criterion is required, since a filter with none would match everything. All criteria are ANDed when applied. The filter is stored in a dictionary inside the .dwg, so it travels with the drawing and is still there next session - one that lived only in memory would be useless for the job filters are for. Refuses a name already in use, because replacing one silently would change what every later call selects.", "selection",
        Intent = new[] { "create a selection filter", "save these selection criteria",
                         "utworz filtr zaznaczenia", "save a filter i can reuse",
                         "zapisz kryteria wyboru obiektow", "define a named filter",
                         "make a reusable selection rule" },
        RequiresPlugin = true)]
    public static Task<FilterCreateResult> CreateSelectionFilter(IPluginGateway gw, FilterCreateArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<FilterCreateArgs, FilterCreateResult>(gw, "acad.selection.create_selection_filter", args, T_NORMAL, ct);

    [McpTool("list_selection_filters", "List the selection filters saved in this drawing, with the criteria of each. Read-only. Filters live in a dictionary inside the .dwg, so they travel with the drawing rather than only lasting the session.", "selection",
        Intent = new[] { "list the saved selection filters", "what filters are saved",
                         "lista zapisanych filtrow", "show my selection filters",
                         "jakie filtry sa zapisane w rysunku", "find a saved filter",
                         "which selection rules exist" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<FilterListResult> ListSelectionFilters(IPluginGateway gw, SelExtNoArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<SelExtNoArgs, FilterListResult>(gw, "acad.selection.list_selection_filters", args, T_NORMAL, ct);

    [McpTool("apply_saved_filter", "Run a saved selection filter and return what it matches. Read-only. The criteria actually used are REPORTED, read back out of the stored filter rather than restated from the request - so a filter that was saved differently from how it was meant shows up here instead of quietly selecting the wrong things. All criteria are ANDed, and the number of entities scanned is reported alongside the number matched, so a small result can be told from an empty drawing.", "selection",
        Intent = new[] { "apply a saved filter", "run my selection filter",
                         "zastosuj zapisany filtr", "select using the saved criteria",
                         "uzyj zapisanego filtra na rysunku", "select with a named filter",
                         "reuse that selection rule" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<FilterApplyResult> ApplySavedFilter(IPluginGateway gw, FilterNameArgs args, CancellationToken ct)
        => SelectionProxy.CallAsync<FilterNameArgs, FilterApplyResult>(gw, "acad.selection.apply_saved_filter", args, T_NORMAL, ct);
}
