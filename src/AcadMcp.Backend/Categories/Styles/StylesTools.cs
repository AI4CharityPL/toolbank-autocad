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
}
