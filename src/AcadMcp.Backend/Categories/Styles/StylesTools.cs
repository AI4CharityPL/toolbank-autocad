// MCP tools for the acad-styles category — authoring the styles a drawing is held to.
// Roadmap 2.3, first tranche: dimension styles.
//
// The split against acad-dimensions is placing versus defining, not old versus new.
// acad-dimensions places dimensions and keeps list_dimstyles, set_entity_dimstyle and
// ensure_architectural_dimstyle (which creates one hard-coded ARCH-ISO and is still the fastest
// way to get a sane architectural style). This category defines styles with chosen properties.
//
// The settable properties live in AcadMcp.Shared.Catalogs.DimStyleProperties rather than in the
// plugin, so CI can hold what list_dimstyle_properties advertises and what create/modify accept
// to each other on every push. A properties dictionary is precisely the shape that produced
// four "the catalogue advertises what the tool refuses" defects in an earlier review.
//
// Later tranches of roadmap 2.3, not attempted here: mleader styles, table styles, mline
// styles, point display, visual style authoring and layer filters. import_dimstyle_from_dwg is
// deliberately last — cross-drawing cloning is the mechanism that defeated
// publish.import_page_setup, and it should be solved once rather than badly twice.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Styles;

public static class StylesTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;

    [McpTool("list_dimstyle_properties", "List every dimension-style property this bank can set, with the AutoCAD DIMVAR behind it, what it does, and its valid range. Read-only. Call this first: the property names here are plain (textHeight, arrowSize, decimalPlaces) rather than DIMVAR spellings, because nobody should have to know that a text height is called DIMTXT in order to set one.", "styles",
        Intent = new[] { "jakie wlasciwosci stylu wymiarowego moge ustawic", "lista parametrow stylu wymiarowania",
                         "list dimension style properties", "what can I set on a dimstyle",
                         "dimstyle property names", "co da sie zmienic w stylu wymiarowym" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<DimStylePropertyListResult> ListDimStyleProperties(IPluginGateway gw, EmptyStylesArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<EmptyStylesArgs, DimStylePropertyListResult>(gw, "acad.styles.list_dimstyle_properties", args, T_FAST, ct);

    [McpTool("create_dimstyle", "Create a named dimension style with chosen properties - text height, arrow size, decimal places, overall scale and the rest. Pass properties as a name-to-value map; use list_dimstyle_properties for the names and ranges. An unknown property name or an out-of-range value is an error, never silently skipped, because skipping would report success over a style that is not what was asked for. Refuses an existing name unless overwrite is true.", "styles",
        Intent = new[] { "utworz styl wymiarowy", "zdefiniuj nowy dimstyle", "create dimension style",
                         "new dimstyle with text height", "define dimension style for 1:50",
                         "wlasny styl wymiarowania" },
        RequiresPlugin = true)]
    public static Task<DimStyleResult> CreateDimStyle(IPluginGateway gw, CreateDimStyleArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<CreateDimStyleArgs, DimStyleResult>(gw, "acad.styles.create_dimstyle", args, T_NORMAL, ct);

    [McpTool("modify_dimstyle", "Change properties on an existing dimension style, leaving the rest alone. The stored style changes immediately; dimensions already placed pick it up on the next regen, and the result says so - an unchanged screen after this call is not a failed call.", "styles",
        Intent = new[] { "zmien styl wymiarowy", "popraw wysokosc tekstu w stylu", "modify dimension style",
                         "change dimstyle text height", "edit existing dimstyle", "zmiana parametrow wymiarowania" },
        RequiresPlugin = true)]
    public static Task<DimStyleResult> ModifyDimStyle(IPluginGateway gw, ModifyDimStyleArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<ModifyDimStyleArgs, DimStyleResult>(gw, "acad.styles.modify_dimstyle", args, T_NORMAL, ct);

    [McpTool("copy_dimstyle", "Duplicate a dimension style under a new name, optionally overriding some properties in the same call. This is how a 1:100 style is made from a 1:50 one: copy it, override scale, done - and the two changes stay atomic instead of leaving a half-made style behind if the second call fails.", "styles",
        Intent = new[] { "skopiuj styl wymiarowy", "zduplikuj dimstyle pod nowa nazwa", "copy dimension style",
                         "duplicate dimstyle for another scale", "clone dimstyle with changes",
                         "styl wymiarowy dla innej skali" },
        RequiresPlugin = true)]
    public static Task<DimStyleResult> CopyDimStyle(IPluginGateway gw, CopyDimStyleArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<CopyDimStyleArgs, DimStyleResult>(gw, "acad.styles.copy_dimstyle", args, T_NORMAL, ct);

    [McpTool("delete_dimstyle", "Delete a dimension style. Refuses to delete 'Standard', refuses to delete the current style, and refuses a style still in use - with the reason, and a pointer to dimensions.set_entity_dimstyle for moving the dimensions off it first.", "styles",
        Intent = new[] { "usun styl wymiarowy", "skasuj dimstyle", "delete dimension style",
                         "remove unused dimstyle", "get rid of dimension style", "wykasuj styl wymiarowania" },
        RequiresPlugin = true)]
    public static Task<StylesAffected> DeleteDimStyle(IPluginGateway gw, DimStyleNameArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<DimStyleNameArgs, StylesAffected>(gw, "acad.styles.delete_dimstyle", args, T_NORMAL, ct);

    [McpTool("set_current_dimstyle", "Make a dimension style the current one, so dimensions placed afterwards use it. Returns the style with all its properties, so the caller can confirm what they just switched to rather than trusting the name.", "styles",
        Intent = new[] { "ustaw biezacy styl wymiarowy", "przelacz na styl wymiarowania", "set current dimension style",
                         "make this dimstyle active", "switch dimstyle", "aktywny styl wymiarowy" },
        RequiresPlugin = true)]
    public static Task<DimStyleResult> SetCurrentDimStyle(IPluginGateway gw, DimStyleNameArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<DimStyleNameArgs, DimStyleResult>(gw, "acad.styles.set_current_dimstyle", args, T_NORMAL, ct);

    // ─────────── multileader styles ───────────
    //
    // Same properties-map shape as the dimension-style tools, on purpose. A caller who has
    // learned one has learned both, and the property table lives in Shared for the same reason.

    [McpTool("list_mleaderstyle_properties", "List every multileader-style property this bank can set, with the API member behind it, what it does and its valid range. Read-only. Booleans travel as 0 or 1 so the whole properties argument stays one map of names to numbers - two value types in one dictionary would be two ways to be wrong about it.", "styles",
        Intent = new[] { "jakie wlasciwosci stylu odnosnika moge ustawic", "parametry stylu multileader",
                         "list mleader style properties", "what can I set on an mleader style",
                         "multileader style property names", "co da sie zmienic w stylu odnosnika" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<MLeaderStylePropertyListResult> ListMLeaderStyleProperties(IPluginGateway gw, EmptyStylesArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<EmptyStylesArgs, MLeaderStylePropertyListResult>(gw, "acad.styles.list_mleaderstyle_properties", args, T_FAST, ct);

    [McpTool("list_mleaderstyles", "List the multileader styles defined in this drawing with all their properties, and which one is current. Read-only.", "styles",
        Intent = new[] { "wylistuj style odnosnikow", "jakie sa style multileader", "list mleader styles",
                         "show multileader styles", "what leader styles exist", "pokaz style odnosnika" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<MLeaderStyleListResult> ListMLeaderStyles(IPluginGateway gw, EmptyStylesArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<EmptyStylesArgs, MLeaderStyleListResult>(gw, "acad.styles.list_mleaderstyles", args, T_FAST, ct);

    [McpTool("create_mleaderstyle", "Create a named multileader style with chosen properties - text height, arrow size, dogleg length, whether the leader has a landing, how many points it may have. Pass properties as a name-to-value map; an unknown name or an out-of-range value is an error rather than a silent skip. Refuses an existing name unless overwrite is true.", "styles",
        Intent = new[] { "utworz styl odnosnika", "zdefiniuj styl multileader", "create mleader style",
                         "new leader style with 2.5 mm text", "define multileader style", "wlasny styl odnosnikow" },
        RequiresPlugin = true)]
    public static Task<MLeaderStyleResult> CreateMLeaderStyle(IPluginGateway gw, CreateMLeaderStyleArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<CreateMLeaderStyleArgs, MLeaderStyleResult>(gw, "acad.styles.create_mleaderstyle", args, T_NORMAL, ct);

    [McpTool("modify_mleaderstyle", "Change properties on an existing multileader style, leaving the rest alone. The stored style changes immediately; multileaders already placed pick it up on the next regen, and the result says so.", "styles",
        Intent = new[] { "zmien styl odnosnika", "popraw dlugosc poziomki w stylu", "modify mleader style",
                         "change leader style text height", "edit multileader style", "zmiana parametrow odnosnika" },
        RequiresPlugin = true)]
    public static Task<MLeaderStyleResult> ModifyMLeaderStyle(IPluginGateway gw, ModifyMLeaderStyleArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<ModifyMLeaderStyleArgs, MLeaderStyleResult>(gw, "acad.styles.modify_mleaderstyle", args, T_NORMAL, ct);

    [McpTool("delete_mleaderstyle", "Delete a multileader style. Refuses to delete 'Standard', refuses to delete the current style, and refuses one still in use - with the reason rather than a bare AutoCAD error code.", "styles",
        Intent = new[] { "usun styl odnosnika", "skasuj styl multileader", "delete mleader style",
                         "remove unused leader style", "get rid of multileader style", "wykasuj styl odnosnikow" },
        RequiresPlugin = true)]
    public static Task<StylesAffected> DeleteMLeaderStyle(IPluginGateway gw, MLeaderStyleNameArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<MLeaderStyleNameArgs, StylesAffected>(gw, "acad.styles.delete_mleaderstyle", args, T_NORMAL, ct);

    [McpTool("set_current_mleaderstyle", "Make a multileader style the current one, so leaders placed afterwards use it. Returns the style with all its properties, so the caller can confirm what they switched to rather than trusting the name.", "styles",
        Intent = new[] { "ustaw biezacy styl odnosnika", "przelacz na styl multileader", "set current mleader style",
                         "make this leader style active", "switch multileader style", "aktywny styl odnosnika" },
        RequiresPlugin = true)]
    public static Task<MLeaderStyleResult> SetCurrentMLeaderStyle(IPluginGateway gw, MLeaderStyleNameArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<MLeaderStyleNameArgs, MLeaderStyleResult>(gw, "acad.styles.set_current_mleaderstyle", args, T_NORMAL, ct);

    // ─────────── table styles ───────────
    //
    // Third style family, same properties-map shape. What differs is that a table has three
    // kinds of row - title, header, data - each with its own text height, so the property names
    // carry the row rather than pretending a table has one text size.

    [McpTool("list_tablestyle_properties", "List every table-style property this bank can set, with the API member behind it, which row it applies to, what it does and its range. Read-only. Text heights are per row - titleTextHeight, headerTextHeight, dataTextHeight - because a schedule's caption, its column headings and its content are three different sizes and pretending otherwise is how tables end up unreadable.", "styles",
        Intent = new[] { "jakie wlasciwosci stylu tabeli moge ustawic", "parametry stylu tabeli",
                         "list table style properties", "what can I set on a table style",
                         "table style property names", "co da sie zmienic w stylu tabeli" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<TableStylePropertyListResult> ListTableStyleProperties(IPluginGateway gw, EmptyStylesArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<EmptyStylesArgs, TableStylePropertyListResult>(gw, "acad.styles.list_tablestyle_properties", args, T_FAST, ct);

    [McpTool("list_tablestyles", "List the table styles defined in this drawing with all their properties, and which one is current. Read-only. The schedules family draws into whichever style is current, so this is what tells you what a generated door or room schedule will look like before you generate it.", "styles",
        Intent = new[] { "wylistuj style tabel", "jakie sa style tabeli", "list table styles",
                         "show table styles", "what schedule styles exist", "pokaz style tabel" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<TableStyleListResult> ListTableStyles(IPluginGateway gw, EmptyStylesArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<EmptyStylesArgs, TableStyleListResult>(gw, "acad.styles.list_tablestyles", args, T_FAST, ct);

    [McpTool("create_tablestyle", "Create a named table style with chosen properties - cell margins, flow direction, and text height per row type. This is what makes a generated door or room schedule match the rest of the set instead of arriving at AutoCAD's defaults. Refuses an existing name unless overwrite is true.", "styles",
        Intent = new[] { "utworz styl tabeli", "zdefiniuj styl zestawienia", "create table style",
                         "new table style for schedules", "define schedule table style", "wlasny styl tabelki" },
        RequiresPlugin = true)]
    public static Task<TableStyleResult> CreateTableStyle(IPluginGateway gw, CreateTableStyleArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<CreateTableStyleArgs, TableStyleResult>(gw, "acad.styles.create_tablestyle", args, T_NORMAL, ct);

    [McpTool("modify_tablestyle", "Change properties on an existing table style, leaving the rest alone. The stored style changes immediately; tables already placed pick it up on the next regen, and the result says so.", "styles",
        Intent = new[] { "zmien styl tabeli", "popraw wysokosc tekstu w tabeli", "modify table style",
                         "change table style text height", "edit schedule style", "zmiana parametrow tabeli" },
        RequiresPlugin = true)]
    public static Task<TableStyleResult> ModifyTableStyle(IPluginGateway gw, ModifyTableStyleArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<ModifyTableStyleArgs, TableStyleResult>(gw, "acad.styles.modify_tablestyle", args, T_NORMAL, ct);

    [McpTool("delete_tablestyle", "Delete a table style. Refuses to delete 'Standard', refuses to delete the current style, and refuses one still in use - with the reason rather than a bare AutoCAD error code.", "styles",
        Intent = new[] { "usun styl tabeli", "skasuj styl zestawienia", "delete table style",
                         "remove unused table style", "get rid of schedule style", "wykasuj styl tabelki" },
        RequiresPlugin = true)]
    public static Task<StylesAffected> DeleteTableStyle(IPluginGateway gw, TableStyleNameArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<TableStyleNameArgs, StylesAffected>(gw, "acad.styles.delete_tablestyle", args, T_NORMAL, ct);

    [McpTool("set_current_tablestyle", "Make a table style the current one, so tables created afterwards use it - including the ones the schedules family generates. Returns the style with all its properties so the caller can confirm what they switched to.", "styles",
        Intent = new[] { "ustaw biezacy styl tabeli", "przelacz na styl zestawienia", "set current table style",
                         "make this table style active", "switch table style", "aktywny styl tabeli" },
        RequiresPlugin = true)]
    public static Task<TableStyleResult> SetCurrentTableStyle(IPluginGateway gw, TableStyleNameArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<TableStyleNameArgs, TableStyleResult>(gw, "acad.styles.set_current_tablestyle", args, T_NORMAL, ct);

    // ─────────── multiline styles (roadmap 2.3) ───────────

    [McpTool("list_mlinestyles", "List every multiline (MLINE) style in the drawing with its parallel line elements, total width, end caps and whether anything is currently drawn with it. Read-only. inUse matters before deleting or redefining one: AutoCAD refuses to change a style that existing MLINE entities reference, so this is the call that tells you why a redefinition would fail.", "styles",
        Intent = new[] { "lista stylow multilinii", "jakie style mline sa w rysunku", "list multiline styles",
                         "show mline styles", "wielolinie dostepne style", "what mline styles exist" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<MlineStyleListResult> ListMlineStyles(IPluginGateway gw, EmptyStylesArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<EmptyStylesArgs, MlineStyleListResult>(gw, "acad.styles.list_mlinestyles", args, T_FAST, ct);

    [McpTool("create_mlinestyle", "Define a named multiline (MLINE) style from a list of parallel line elements, each given an offset from the centreline plus an optional colour and linetype. This is how a wall type is defined once and drawn many times: a 200mm wall is two elements at +100 and -100. Offsets are in drawing units and may be negative. Refuses an existing name unless overwrite is true, and refuses to redefine a style that entities already use, because AutoCAD does not allow that and reporting success would be a lie.", "styles",
        Intent = new[] { "utworz styl multilinii", "zdefiniuj styl sciany mline", "create multiline style",
                         "define mline style for a 200mm wall", "nowy styl wielolinii", "wall type as mline style" },
        RequiresPlugin = true)]
    public static Task<MlineStyleResult> CreateMlineStyle(IPluginGateway gw, CreateMlineStyleArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<CreateMlineStyleArgs, MlineStyleResult>(gw, "acad.styles.create_mlinestyle", args, T_NORMAL, ct);

    [McpTool("modify_mlinestyle", "Change an existing multiline style, leaving anything you do not pass alone. Passing elements REPLACES the whole element list rather than merging into it - a partial merge has no meaning when the elements are an ordered geometric set. Refuses a style that MLINE entities already reference, which is an AutoCAD restriction and not a choice made here.", "styles",
        Intent = new[] { "zmien styl multilinii", "popraw offsety w stylu mline", "modify multiline style",
                         "change mline style elements", "edytuj styl wielolinii", "adjust mline widths" },
        RequiresPlugin = true)]
    public static Task<MlineStyleResult> ModifyMlineStyle(IPluginGateway gw, ModifyMlineStyleArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<ModifyMlineStyleArgs, MlineStyleResult>(gw, "acad.styles.modify_mlinestyle", args, T_NORMAL, ct);

    // ─────────── layer filters (roadmap 2.3) ───────────
    //
    // apply_layer_filter was planned and is NOT here: LayerFilterTree.Current is get-only and
    // there is no managed type that sets it. Which filter the Layer Properties Manager displays
    // is palette state. See StylesLayerFilterPluginTools for the full reasoning; the short
    // version is that a tool which assigns nothing must not be given a name that promises it.

    [McpTool("list_layer_filters", "List every layer filter in the drawing - both kinds - with the expression or layer list behind it and how many layers it currently selects. Read-only. matchCount is the field to read after creating one: an expression can be perfectly valid, be stored, be listed, and select nothing, which no return code can tell you.", "styles",
        Intent = new[] { "lista filtrow warstw", "jakie filtry warstw sa w rysunku", "list layer filters",
                         "show layer filters", "filtry warstw", "what layer filters exist" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<LayerFilterListResult> ListLayerFilters(IPluginGateway gw, EmptyStylesArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<EmptyStylesArgs, LayerFilterListResult>(gw, "acad.styles.list_layer_filters", args, T_FAST, ct);

    [McpTool("create_layer_filter", "Create a PROPERTY layer filter from an expression, so layers created later that match it join automatically. Expressions look like NAME==\"A-*\" or COLOR==\"1\", combined with AND / OR / NOT. Nest it under an existing filter with parent. The result reports matchCount - check it, because a valid expression that selects nothing is stored and listed exactly like one that works.", "styles",
        Intent = new[] { "utworz filtr warstw", "filtr warstw po nazwie", "create layer filter",
                         "filter layers by name pattern", "grupuj warstwy wyrazeniem",
                         "layer filter for architectural layers" },
        RequiresPlugin = true)]
    public static Task<LayerFilterResult> CreateLayerFilter(IPluginGateway gw, CreateLayerFilterArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<CreateLayerFilterArgs, LayerFilterResult>(gw, "acad.styles.create_layer_filter", args, T_NORMAL, ct);

    [McpTool("create_layer_group_filter", "Create a GROUP layer filter holding a fixed list of named layers. Unlike a property filter this never changes on its own - a layer added to the drawing afterwards does not join it. Use this when the set is a decision rather than a pattern. Every named layer must already exist; naming one that does not is an error rather than a silently smaller group.", "styles",
        Intent = new[] { "utworz grupe warstw", "filtr grupowy warstw", "create layer group filter",
                         "group specific layers together", "stala lista warstw jako filtr",
                         "layer group from a list" },
        RequiresPlugin = true)]
    public static Task<LayerFilterResult> CreateLayerGroupFilter(IPluginGateway gw, CreateLayerGroupFilterArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<CreateLayerGroupFilterArgs, LayerFilterResult>(gw, "acad.styles.create_layer_group_filter", args, T_NORMAL, ct);

    [McpTool("delete_layer_filter", "Delete a layer filter. Deleting one that has nested filters takes those with it, and the result names them, because a filter count that dropped further than expected is otherwise a mystery. Refuses AutoCAD's built-in filters. Layers themselves are never touched - a filter is a view of them, not a container.", "styles",
        Intent = new[] { "usun filtr warstw", "skasuj filtr warstw", "delete layer filter",
                         "remove a layer filter", "usun grupe warstw", "drop layer filter" },
        RequiresPlugin = true)]
    public static Task<LayerFilterDeleteResult> DeleteLayerFilter(IPluginGateway gw, LayerFilterNameArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<LayerFilterNameArgs, LayerFilterDeleteResult>(gw, "acad.styles.delete_layer_filter", args, T_NORMAL, ct);

    // ─────────── table cell styles, visual styles, point display (roadmap 2.3) ───────────

    [McpTool("set_table_cell_style", "Set the per-cell properties of a table style - text height, alignment, text colour and background - for one cell class. A table style keeps a separate set of these for each class, typically _TITLE, _HEADER and _DATA, which is why the style-wide create/modify tools cannot reach them. Pass backgroundColorIndex as -1 to clear a background rather than set one. The result reports the cell's full state afterwards, so a caller sees what the other properties still are.", "styles",
        Intent = new[] { "ustaw styl komorki tabeli", "zmien wysokosc tekstu w naglowku tabeli",
                         "set table cell style", "table header text height and alignment",
                         "kolor tla komorki tabeli", "format the title row of a table style" },
        RequiresPlugin = true)]
    public static Task<TableCellStyleResult> SetTableCellStyle(IPluginGateway gw, SetTableCellStyleArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<SetTableCellStyleArgs, TableCellStyleResult>(gw, "acad.styles.set_table_cell_style", args, T_NORMAL, ct);

    [McpTool("list_visual_styles", "List every visual style in the drawing with the preset it derives from, plus the full set of preset names available to create_visual_style. Read-only. Styles AutoCAD keeps for its own rendering passes are flagged internalUseOnly rather than hidden, because omitting them would misreport what the drawing contains.", "styles",
        Intent = new[] { "lista stylow wizualnych", "jakie style wizualizacji sa dostepne",
                         "list visual styles", "show visual styles", "style wyswietlania 3d",
                         "what visual styles exist" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<VisualStyleListResult> ListVisualStyles(IPluginGateway gw, EmptyStylesArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<EmptyStylesArgs, VisualStyleListResult>(gw, "acad.styles.list_visual_styles", args, T_FAST, ct);

    [McpTool("create_visual_style", "Create a named visual style derived from one of AutoCAD's presets - Conceptual, Realistic, Shaded, Hidden, Wireframe2D and the rest. Call list_visual_styles first for the full preset list. This deliberately does NOT expose per-trait authoring: DBVisualStyle offers only an untyped trait API with no property catalogue to advertise, so a tool promising arbitrary edits could not tell a caller what it accepts. Apply the result to a viewport with set_viewport_visual_style.", "styles",
        Intent = new[] { "utworz styl wizualny", "nowy styl wyswietlania", "create visual style",
                         "make a conceptual visual style", "wlasny styl wizualizacji",
                         "visual style based on realistic" },
        RequiresPlugin = true)]
    public static Task<VisualStyleResult> CreateVisualStyle(IPluginGateway gw, CreateVisualStyleArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<CreateVisualStyleArgs, VisualStyleResult>(gw, "acad.styles.create_visual_style", args, T_NORMAL, ct);

    [McpTool("set_point_display", "Set how POINT entities are drawn, drawing-wide. Give a plain glyph name - dot, none, plus, cross, tick - with an optional surround of circle, square or both, and this works out the PDMODE bit code for you; pass mode instead if you already know it. size sets PDSIZE: positive is absolute drawing units, negative is a percentage of the viewport. This is NOT a style object - AutoCAD has no per-point style, only these two system variables, so the change applies to every point in the drawing.", "styles",
        Intent = new[] { "zmien wyglad punktow", "jak wyswietlac punkty", "set point display style",
                         "make points show as crosses", "rozmiar punktow pdsize",
                         "point display mode pdmode" },
        RequiresPlugin = true)]
    public static Task<PointDisplayResult> SetPointDisplay(IPluginGateway gw, SetPointDisplayArgs args, CancellationToken ct)
        => StylesProxy.CallAsync<SetPointDisplayArgs, PointDisplayResult>(gw, "acad.styles.set_point_display", args, T_NORMAL, ct);
}
