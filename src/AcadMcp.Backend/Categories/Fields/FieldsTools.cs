// MCP tools for the acad-fields category: text that stays correct when the drawing changes.
//
// Why this category matters more than its size suggests: callouts.insert_title_block and the
// whole schedules.* family currently write frozen strings. A sheet number, a date or a room
// area written as plain text is wrong the moment anything moves, and
// schedules.correct_all_room_areas exists specifically to go round afterwards fixing them.
// Fields remove the need for that pass.
//
// A field is an MText whose contents carry an %<\AcVar ...>% expression. AutoCAD evaluates it;
// the tools here build the expression, place the text and evaluate once so the caller gets the
// resolved value back immediately rather than a code they cannot check.
//
// Deliberately not here:
//   insert_field_sheetset_property - needs acad-sheetsets (Phase 2.1)
//   data-link fields               - need acad-data (Phase 5.2)

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Fields;

public static class FieldsTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;

    // ─────────────── insertion ───────────────

    [McpTool("insert_field_date", "Place a date field that re-evaluates rather than freezing. format is a .NET/AutoCAD date pattern (default yyyy-MM-dd). Use this for the date cell of a title block instead of writing today's date as text.", "fields",
        Intent = new[] { "wstaw pole z data", "data ktora sie aktualizuje", "insert date field",
                         "add auto-updating date", "title block date field", "dynamiczna data na rysunku" },
        RequiresPlugin = true)]
    public static Task<FieldResult> InsertFieldDate(IPluginGateway gw, FieldDateArgs args, CancellationToken ct)
        => FieldsProxy.CallAsync<FieldDateArgs, FieldResult>(gw, "acad.fields.insert_field_date", args, T_NORMAL, ct);

    [McpTool("insert_field_filename", "Place a field showing this drawing's file name, optionally with its full path and extension. Survives Save As, which a typed file name does not.", "fields",
        Intent = new[] { "wstaw pole z nazwa pliku", "nazwa rysunku jako pole", "insert filename field",
                         "show drawing name automatically", "file name field", "sciezka pliku na rysunku" },
        RequiresPlugin = true)]
    public static Task<FieldResult> InsertFieldFilename(IPluginGateway gw, FieldFilenameArgs args, CancellationToken ct)
        => FieldsProxy.CallAsync<FieldFilenameArgs, FieldResult>(gw, "acad.fields.insert_field_filename", args, T_NORMAL, ct);

    [McpTool("insert_field_layout_name", "Place a field showing the name of the layout the field sits on. This is the sheet-number cell of a title block: rename the tab and every sheet updates itself.", "fields",
        Intent = new[] { "wstaw pole z nazwa arkusza", "numer arkusza jako pole", "insert layout name field",
                         "sheet number field", "tab name field", "nazwa ukladu w tabelce" },
        RequiresPlugin = true)]
    public static Task<FieldResult> InsertFieldLayoutName(IPluginGateway gw, FieldPlacementArgs args, CancellationToken ct)
        => FieldsProxy.CallAsync<FieldPlacementArgs, FieldResult>(gw, "acad.fields.insert_field_layout_name", args, T_NORMAL, ct);

    [McpTool("insert_field_object_property", "Place a field bound to a property of an existing entity by handle - Area, Length, Radius, Layer, Color and so on. The text follows the object: edit the geometry and the number changes with it. This is what makes a room-area label self-maintaining.", "fields",
        Intent = new[] { "wstaw pole z wlasciwoscia obiektu", "powierzchnia jako pole dynamiczne", "insert object property field",
                         "field bound to entity area", "auto-updating area label", "pole powiazane z geometria" },
        RequiresPlugin = true)]
    public static Task<FieldResult> InsertFieldObjectProperty(IPluginGateway gw, FieldObjectPropertyArgs args, CancellationToken ct)
        => FieldsProxy.CallAsync<FieldObjectPropertyArgs, FieldResult>(gw, "acad.fields.insert_field_object_property", args, T_NORMAL, ct);

    [McpTool("insert_field_system_variable", "Place a field showing an AutoCAD system variable (DWGNAME, LOGINNAME, CTAB, DWGPREFIX, ...). Useful for drawn-by and drawing-status cells.", "fields",
        Intent = new[] { "wstaw pole ze zmienna systemowa", "kto rysowal jako pole", "insert system variable field",
                         "sysvar field", "loginname field", "zmienna systemowa na rysunku" },
        RequiresPlugin = true)]
    public static Task<FieldResult> InsertFieldSystemVariable(IPluginGateway gw, FieldSystemVariableArgs args, CancellationToken ct)
        => FieldsProxy.CallAsync<FieldSystemVariableArgs, FieldResult>(gw, "acad.fields.insert_field_system_variable", args, T_NORMAL, ct);

    [McpTool("insert_field_expression", "Place a field from a raw AcVar expression, for anything the typed tools do not cover. Escape hatch - the expression is passed through unvalidated, and the evaluated result is returned so a wrong one is visible immediately rather than at plot time.", "fields",
        Intent = new[] { "wstaw pole z wyrazeniem", "wlasne pole acvar", "insert raw field expression",
                         "custom field code", "arbitrary field expression", "surowe wyrazenie pola" },
        RequiresPlugin = true)]
    public static Task<FieldResult> InsertFieldExpression(IPluginGateway gw, FieldRawArgs args, CancellationToken ct)
        => FieldsProxy.CallAsync<FieldRawArgs, FieldResult>(gw, "acad.fields.insert_field_expression", args, T_NORMAL, ct);

    // ─────────────── maintenance ───────────────

    [McpTool("list_fields", "List every text entity in model space that contains a field, with its raw expression and its currently evaluated value. Read-only. Call this to find title-block cells that are still frozen text.", "fields",
        Intent = new[] { "wylistuj pola", "gdzie sa pola dynamiczne", "list fields in drawing",
                         "show all fields", "which text is a field", "jakie pola sa na rysunku" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<FieldListResult> ListFields(IPluginGateway gw, EmptyFieldsArgs args, CancellationToken ct)
        => FieldsProxy.CallAsync<EmptyFieldsArgs, FieldListResult>(gw, "acad.fields.list_fields", args, T_FAST, ct);

    [McpTool("update_fields", "Re-evaluate fields now. Pass handles to update specific ones, or omit them to update every field in the drawing. Returns how many were evaluated.", "fields",
        Intent = new[] { "zaktualizuj pola", "przelicz pola teraz", "update fields",
                         "refresh all fields", "recalculate field values", "odswiez wartosci pol" },
        RequiresPlugin = true)]
    public static Task<FieldAffected> UpdateFields(IPluginGateway gw, UpdateFieldsArgs args, CancellationToken ct)
        => FieldsProxy.CallAsync<UpdateFieldsArgs, FieldAffected>(gw, "acad.fields.update_fields", args, T_NORMAL, ct);

    [McpTool("convert_field_to_text", "Freeze a field into plain text at its current value. One-way and deliberate: use it when issuing a drawing that must not change afterwards, never as a way to 'fix' a field showing the wrong thing.", "fields",
        Intent = new[] { "zamien pole na tekst", "zamroz wartosc pola", "convert field to static text",
                         "freeze field value", "make field permanent text", "usun powiazanie pola" },
        RequiresPlugin = true)]
    public static Task<FieldAffected> ConvertFieldToText(IPluginGateway gw, FieldHandleArgs args, CancellationToken ct)
        => FieldsProxy.CallAsync<FieldHandleArgs, FieldAffected>(gw, "acad.fields.convert_field_to_text", args, T_NORMAL, ct);

    [McpTool("get_field_expression", "Return the raw AcVar expression and the evaluated value behind one text entity. Read-only. Use it to see what a field is actually bound to before trusting the number it shows.", "fields",
        Intent = new[] { "pokaz wyrazenie pola", "do czego odwoluje sie pole", "get field expression",
                         "what is this field bound to", "inspect field code", "kod pola" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<FieldResult> GetFieldExpression(IPluginGateway gw, FieldHandleArgs args, CancellationToken ct)
        => FieldsProxy.CallAsync<FieldHandleArgs, FieldResult>(gw, "acad.fields.get_field_expression", args, T_FAST, ct);

    [McpTool("set_field_evaluation_mode", "Control when fields re-evaluate: on open, save, plot and/or regen (the FIELDEVAL bitmask). Turning them all off is how a drawing is frozen for issue without converting every field to text.", "fields",
        Intent = new[] { "ustaw kiedy pola sie przeliczaja", "wylacz automatyczne pola", "set field evaluation mode",
                         "fieldeval setting", "when do fields update", "tryb aktualizacji pol" },
        RequiresPlugin = true)]
    public static Task<FieldEvalModeResult> SetFieldEvaluationMode(IPluginGateway gw, FieldEvalModeArgs args, CancellationToken ct)
        => FieldsProxy.CallAsync<FieldEvalModeArgs, FieldEvalModeResult>(gw, "acad.fields.set_field_evaluation_mode", args, T_FAST, ct);

    [McpTool("get_field_evaluation_mode", "Report when fields currently re-evaluate (the FIELDEVAL bitmask, decoded). Read-only.", "fields",
        Intent = new[] { "sprawdz tryb aktualizacji pol", "jakie jest fieldeval", "get field evaluation mode",
                         "when do fields update currently", "read fieldeval", "aktualny tryb pol" },
        RequiresPlugin = true, ReadOnly = true)]
    public static Task<FieldEvalModeResult> GetFieldEvaluationMode(IPluginGateway gw, EmptyFieldsArgs args, CancellationToken ct)
        => FieldsProxy.CallAsync<EmptyFieldsArgs, FieldEvalModeResult>(gw, "acad.fields.get_field_evaluation_mode", args, T_FAST, ct);
}
