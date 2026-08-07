// AutoCAD acad-annotations category. 12 tools covering single-line text (DBText/DTEXT),
// multi-line text (MText) with inline formatting, multi-leaders (MLeader, both text and block content),
// AutoCAD Tables (cell data and updates), and text style management.
//
// Rules: 19-tool-implementation-pattern.md, 27-acad-text-and-table-traps.md.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Annotations;

public static class AnnotationsTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;
    private const int T_SLOW = 30_000;

    // ─────────── DBText / DTEXT ───────────

    [McpTool("add_dbtext", "Add a single-line text entity (DBText / DTEXT) at the given position. Height defaults to 2.5 mm; alignment is one of Left, Center, Right, Middle, BaseLeft, BaseCenter, BaseRight, TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight.", "annotations",
        Intent = new[] { "dodaj tekst", "wstaw napis dtext", "add single line text", "place dbtext", "dodaj dtext" },
        RequiresPlugin = true)]
    public static Task<AnnEntityResult> AddDBText(IPluginGateway gw, AddDBTextArgs args, CancellationToken ct)
        => AnnotationsProxy.CallAsync<AddDBTextArgs, AnnEntityResult>(gw, "acad.annotations.add_dbtext", args, T_NORMAL, ct);

    [McpTool("update_dbtext", "Replace the contents of an existing DBText entity by handle.", "annotations",
        Intent = new[] { "zmien tekst dtext", "edit single line text", "update dbtext content", "popraw napis", "set dbtext text" },
        RequiresPlugin = true)]
    public static Task<AnnEntityResult> UpdateDBText(IPluginGateway gw, UpdateDBTextArgs args, CancellationToken ct)
        => AnnotationsProxy.CallAsync<UpdateDBTextArgs, AnnEntityResult>(gw, "acad.annotations.update_dbtext", args, T_FAST, ct);

    // ─────────── MText ───────────

    [McpTool("add_mtext", "Add a multi-line text (MText) entity at the given position. widthFactor=0 disables word-wrap (auto width); attachmentPoint is e.g. TopLeft / MiddleCenter / BottomRight (defaults to TopLeft). Inline MText formatting codes are passed through (\\\\Pnewline, \\\\Lunderline, \\\\C2red).", "annotations",
        Intent = new[] { "dodaj mtext", "wstaw tekst wieloliniowy", "add multiline text", "place mtext", "wrapped text block" },
        RequiresPlugin = true)]
    public static Task<AnnEntityResult> AddMText(IPluginGateway gw, AddMTextArgs args, CancellationToken ct)
        => AnnotationsProxy.CallAsync<AddMTextArgs, AnnEntityResult>(gw, "acad.annotations.add_mtext", args, T_NORMAL, ct);

    [McpTool("update_mtext", "Replace the contents string of an existing MText entity by handle. Inline formatting codes are preserved as written.", "annotations",
        Intent = new[] { "zmien mtext", "edit multiline text", "update mtext content", "popraw mtext", "set mtext text" },
        RequiresPlugin = true)]
    public static Task<AnnEntityResult> UpdateMText(IPluginGateway gw, UpdateMTextArgs args, CancellationToken ct)
        => AnnotationsProxy.CallAsync<UpdateMTextArgs, AnnEntityResult>(gw, "acad.annotations.update_mtext", args, T_FAST, ct);

    // ─────────── MLeader / Leader ───────────

    [McpTool("add_mleader_text", "Add a multi-leader (MLeader) with MText content. Single segment from arrowTip to textPosition; the dogleg between the leader and the text block is enabled by default.", "annotations",
        Intent = new[] { "dodaj wynos", "dodaj mleader z tekstem", "add multileader text", "place mleader with text", "callout with text" },
        RequiresPlugin = true)]
    public static Task<AnnEntityResult> AddMLeaderText(IPluginGateway gw, AddMLeaderArgs args, CancellationToken ct)
        => AnnotationsProxy.CallAsync<AddMLeaderArgs, AnnEntityResult>(gw, "acad.annotations.add_mleader_text", args, T_NORMAL, ct);

    [McpTool("add_mleader_block", "Add a multi-leader (MLeader) whose content is a block reference (e.g. detail bubble). The block must already be defined in the drawing.", "annotations",
        Intent = new[] { "dodaj mleader z blokiem", "wynos z blokiem", "add multileader with block", "place mleader block", "callout with block" },
        RequiresPlugin = true)]
    public static Task<AnnEntityResult> AddMLeaderBlock(IPluginGateway gw, AddBlockMLeaderArgs args, CancellationToken ct)
        => AnnotationsProxy.CallAsync<AddBlockMLeaderArgs, AnnEntityResult>(gw, "acad.annotations.add_mleader_block", args, T_NORMAL, ct);

    // ─────────── Tables ───────────

    [McpTool("add_table", "Insert an AutoCAD Table at the given position with rows × cols cells. Optional 2D data array fills cell text top-to-bottom, left-to-right. rowHeight/colWidth are in current units.", "annotations",
        Intent = new[] { "dodaj tabele", "wstaw tabele", "add table", "create table from data", "tabela rxc" },
        RequiresPlugin = true)]
    public static Task<AnnEntityResult> AddTable(IPluginGateway gw, AddTableArgs args, CancellationToken ct)
        => AnnotationsProxy.CallAsync<AddTableArgs, AnnEntityResult>(gw, "acad.annotations.add_table", args, T_SLOW, ct);

    [McpTool("set_table_cell", "Set the text content of a single Table cell by (row, col), 0-based.", "annotations",
        Intent = new[] { "ustaw komorke tabeli", "set table cell text", "edit table cell", "zmien komorke", "table cell update" },
        RequiresPlugin = true)]
    public static Task<AnnEntityResult> SetTableCell(IPluginGateway gw, SetTableCellArgs args, CancellationToken ct)
        => AnnotationsProxy.CallAsync<SetTableCellArgs, AnnEntityResult>(gw, "acad.annotations.set_table_cell", args, T_FAST, ct);

    // ─────────── text styles ───────────

    [McpTool("create_text_style", "Create a new text style (TextStyleTableRecord) by name with the given font (.shx or TTF face name). height=0 makes the style annotative-friendly (text height set per-entity).", "annotations",
        Intent = new[] { "stworz styl tekstu", "create text style", "dodaj textstyle", "new text style", "make ttf style" },
        RequiresPlugin = true)]
    public static Task<TextStyleNameArg> CreateTextStyle(IPluginGateway gw, CreateTextStyleArgs args, CancellationToken ct)
        => AnnotationsProxy.CallAsync<CreateTextStyleArgs, TextStyleNameArg>(gw, "acad.annotations.create_text_style", args, T_NORMAL, ct);

    [McpTool("set_current_text_style", "Set the active text style for new DBText / MText entities; subsequent text creation defaults to it.", "annotations",
        Intent = new[] { "ustaw aktualny styl tekstu", "set current text style", "switch active textstyle", "make textstyle current", "wybierz styl tekstu" },
        RequiresPlugin = true)]
    public static Task<TextStyleNameArg> SetCurrentTextStyle(IPluginGateway gw, TextStyleNameArg args, CancellationToken ct)
        => AnnotationsProxy.CallAsync<TextStyleNameArg, TextStyleNameArg>(gw, "acad.annotations.set_current_text_style", args, T_FAST, ct);

    [McpTool("list_text_styles", "List every text style defined in the active drawing plus the current style name.", "annotations",
        Intent = new[] { "wylistuj style tekstu", "list text styles", "show all textstyles", "wszystkie style tekstu", "what text styles exist" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<TextStyleListResult> ListTextStyles(IPluginGateway gw, AnnotationsEmptyArgs args, CancellationToken ct)
        => AnnotationsProxy.CallAsync<AnnotationsEmptyArgs, TextStyleListResult>(gw, "acad.annotations.list_text_styles", args, T_FAST, ct);

    [McpTool("delete_text_style", "Delete a text style by name. Standard cannot be deleted; the style must be unused (no DBText/MText/Dim references).", "annotations",
        Intent = new[] { "usun styl tekstu", "delete text style", "skasuj textstyle", "remove text style", "drop textstyle" },
        RequiresPlugin = true)]
    public static Task<AnnAffectedCount> DeleteTextStyle(IPluginGateway gw, TextStyleNameArg args, CancellationToken ct)
        => AnnotationsProxy.CallAsync<TextStyleNameArg, AnnAffectedCount>(gw, "acad.annotations.delete_text_style", args, T_NORMAL, ct);

    // ─────────── roadmap 3.3: finding text across a drawing ───────────

    [McpTool("list_text_by_pattern", "Find every piece of text in the drawing matching a pattern, WITHOUT changing anything. Reads all six places text lives - single-line text, MText, MLeaders, block attributes, table cells and dimension text overrides - and reports scannedByType, so 'no matches' in a drawing full of text can be told from 'no matches in the two types a lesser search bothered to look at'. Matching runs against the RENDERED text, not the stored string: a search over MText contents would hit words inside formatting codes and report matches nobody can see on the sheet. Set regex, matchCase or wholeWord as needed.", "annotations",
        Intent = new[] { "znajdz tekst w rysunku", "wyszukaj napisy", "gdzie jest ten tekst",
                         "find text in the drawing", "search all text",
                         "list text matching a pattern", "szukaj po wzorcu" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<TextSearchResult> ListTextByPattern(IPluginGateway gw, TextSearchArgs args, CancellationToken ct)
        => AnnotationsProxy.CallAsync<TextSearchArgs, TextSearchResult>(gw, "acad.annotations.list_text_by_pattern", args, T_SLOW, ct);

    [McpTool("find_replace_text", "Find and replace text across the whole drawing - AutoCAD's FIND. Covers single-line text, MText, MLeaders, block attributes, table cells and dimension text overrides. Every write is READ BACK and anything that did not take is listed as skipped rather than counted. Text carrying MText formatting codes is only changed when the pattern matches the same number of times in the stored string as in the rendered one; otherwise the replacement would be landing inside a code - changing a font rather than the words - and it is skipped with that reason. Pass dryRun true to see exactly what would change without writing anything.", "annotations",
        Intent = new[] { "zamien tekst w calym rysunku", "znajdz i zamien", "popraw nazwe wszedzie",
                         "find and replace text", "rename across the drawing",
                         "replace text everywhere", "zamiana napisow" },
        RequiresPlugin = true)]
    public static Task<FindReplaceResult> FindReplaceText(IPluginGateway gw, FindReplaceArgs args, CancellationToken ct)
        => AnnotationsProxy.CallAsync<FindReplaceArgs, FindReplaceResult>(gw, "acad.annotations.find_replace_text", args, T_SLOW, ct);

    [McpTool("export_text_content", "Write every piece of text in the drawing to a file - single-line text, MText, MLeaders, block attributes, table cells and dimension overrides. format csv carries handle, type, layer and text; txt is the text alone, one item per line. The text column is what a reader sees, with MText formatting codes already resolved. Written UTF-8 with a byte order mark so a spreadsheet opens accented characters intact rather than as mojibake, and the file is checked to be non-empty before this reports success.", "annotations",
        Intent = new[] { "wyeksportuj teksty z rysunku", "zapisz wszystkie napisy do pliku",
                         "export all text", "dump drawing text to csv",
                         "lista tekstow do pliku", "extract text content" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<ExportTextResult> ExportTextContent(IPluginGateway gw, ExportTextArgs args, CancellationToken ct)
        => AnnotationsProxy.CallAsync<ExportTextArgs, ExportTextResult>(gw, "acad.annotations.export_text_content", args, T_SLOW, ct);
}
