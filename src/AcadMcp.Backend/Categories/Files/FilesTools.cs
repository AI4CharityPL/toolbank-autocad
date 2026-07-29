// AutoCAD acad-files category. 11 tools covering DWG document lifecycle (open / save / save as / close),
// DWG / DXF import (loaded as a new document or merged into the active one), export (DWG / DXF / PDF / DWF / IMAGE),
// document inspection (current document, listing all open documents) and database hygiene
// (purge of all unused records, audit with optional fix).
//
// Rules: 19, 28-acad-blocks-layers-files-traps.md.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Files;

public static class FilesTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;
    private const int T_SLOW = 30_000;
    private const int T_VERY_SLOW = 120_000;

    [McpTool("list_documents", "List every open AutoCAD document with its file path, modified flag, read-only flag and entity count, plus the active document name.", "files",
        Intent = new[] { "wylistuj otwarte rysunki", "list open documents", "show open dwgs", "wszystkie otwarte dokumenty", "what documents are open" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<DocumentsListResult> ListDocuments(IPluginGateway gw, FilesEmptyArgs args, CancellationToken ct)
        => FilesProxy.CallAsync<FilesEmptyArgs, DocumentsListResult>(gw, "acad.files.list_documents", args, T_FAST, ct);

    [McpTool("get_active_document", "Return descriptor of the currently active document (path, name, modified flag, DWG version, entity count).", "files",
        Intent = new[] { "info o aktywnym rysunku", "get active document", "describe current dwg", "active drawing info", "info o biezacym dokumencie" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<DocumentResult> GetActiveDocument(IPluginGateway gw, FilesEmptyArgs args, CancellationToken ct)
        => FilesProxy.CallAsync<FilesEmptyArgs, DocumentResult>(gw, "acad.files.get_active_document", args, T_FAST, ct);

    [McpTool("open_document", "Open an existing .dwg / .dxf file in the AutoCAD UI as a new document and make it active. Optional readOnly flag and password for encrypted DWG.", "files",
        Intent = new[] { "otworz rysunek", "open dwg file", "wczytaj dwg", "open drawing", "load dwg" },
        RequiresPlugin = true)]
    public static Task<DocumentResult> OpenDocument(IPluginGateway gw, OpenDocumentArgs args, CancellationToken ct)
        => FilesProxy.CallAsync<OpenDocumentArgs, DocumentResult>(gw, "acad.files.open_document", args, T_VERY_SLOW, ct);

    [McpTool("save_document", "Save the currently active document to its existing path (no-op if it has no path yet — call save_document_as instead).", "files",
        Intent = new[] { "zapisz rysunek", "save current dwg", "save active document", "save dwg", "zapisz aktywny dokument" },
        RequiresPlugin = true)]
    public static Task<DocumentResult> SaveDocument(IPluginGateway gw, FilesEmptyArgs args, CancellationToken ct)
        => FilesProxy.CallAsync<FilesEmptyArgs, DocumentResult>(gw, "acad.files.save_document", args, T_SLOW, ct);

    [McpTool("save_document_as", "Save the currently active document to a new path. Optional dwgVersion is one of \"AC1027\" (2013), \"AC1032\" (2018), \"AC1024\" (2010), etc. Defaults to current AutoCAD's native format.", "files",
        Intent = new[] { "zapisz jako", "save dwg as", "save active as", "save document to path", "zapisz dokument jako" },
        RequiresPlugin = true)]
    public static Task<DocumentResult> SaveDocumentAs(IPluginGateway gw, SaveAsArgs args, CancellationToken ct)
        => FilesProxy.CallAsync<SaveAsArgs, DocumentResult>(gw, "acad.files.save_document_as", args, T_SLOW, ct);

    [McpTool("close_document", "Close a document by its file path (or the active document if path is null). Set save=true to save before closing.", "files",
        Intent = new[] { "zamknij rysunek", "close document", "close active dwg", "zamknij aktywny dokument", "close drawing" },
        RequiresPlugin = true)]
    public static Task<FilesAffectedCount> CloseDocument(IPluginGateway gw, CloseDocumentArgs args, CancellationToken ct)
        => FilesProxy.CallAsync<CloseDocumentArgs, FilesAffectedCount>(gw, "acad.files.close_document", args, T_NORMAL, ct);

    [McpTool("import_file", "Import a .dwg / .dxf file into the currently active document at the optional insertion point (default: 0,0,0). DWG files are merged into model space; DXF respects its own units.", "files",
        Intent = new[] { "importuj plik", "import dwg into current", "import dxf", "merge dwg into active", "wstaw plik dwg" },
        RequiresPlugin = true)]
    public static Task<FilesAffectedCount> ImportFile(IPluginGateway gw, ImportFileArgs args, CancellationToken ct)
        => FilesProxy.CallAsync<ImportFileArgs, FilesAffectedCount>(gw, "acad.files.import_file", args, T_VERY_SLOW, ct);

    [McpTool("export_file", "Export the active document to the given path. Format is one of \"DWG\", \"DXF\", \"PDF\", \"DWF\", \"DWFX\", \"IMAGE\" / \"PNG\". Optional layout name (default: current). Scope is \"Display\" / \"Extents\" / \"Limits\" / \"Window\" / \"View\" / \"Layout\". When scope=\"Window\" you MUST supply the model-space rectangle in `window`: { xMin, yMin, xMax, yMax } in drawing units. For raster (PNG/IMAGE) and vector plots you may supply `widthPx` / `heightPx` to request an output resolution (PNG only; ignored for DWG/DXF). Typical usage for AI visual review: { format:\"PNG\", scope:\"Window\", window:{xMin:0,yMin:0,xMax:80000,yMax:60000}, widthPx:4000, heightPx:3000 }.", "files",
        Intent = new[] { "eksportuj plik", "export to pdf", "export to dxf", "export drawing to format", "save as pdf", "export drawing region to png", "render window to png", "screenshot a drawing area", "zrob zrzut fragmentu rysunku" },
        RequiresPlugin = true)]
    public static Task<FilePathResult> ExportFile(IPluginGateway gw, ExportFileArgs args, CancellationToken ct)
        => FilesProxy.CallAsync<ExportFileArgs, FilePathResult>(gw, "acad.files.export_file", args, T_VERY_SLOW, ct);

    [McpTool("purge_database", "Run a full database purge: removes every unused symbol-table record (blocks, layers, linetypes, text/dimstyle, mlinestyle, registered apps). Returns the count of records purged.", "files",
        Intent = new[] { "purge bazy", "purge database", "wyczysc cala baze", "remove all unused records", "drawing purge" },
        RequiresPlugin = true)]
    public static Task<FilesAffectedCount> PurgeDatabase(IPluginGateway gw, FilesEmptyArgs args, CancellationToken ct)
        => FilesProxy.CallAsync<FilesEmptyArgs, FilesAffectedCount>(gw, "acad.files.purge_database", args, T_SLOW, ct);

    [McpTool("audit_database", "Run AUDIT on the active document database. Reports the number of errors found and (if fix=true) fixed.", "files",
        Intent = new[] { "audyt bazy", "audit database", "sprawdz bledy w rysunku", "audit drawing", "fix database errors" },
        RequiresPlugin = true)]
    public static Task<AuditResult> AuditDatabase(IPluginGateway gw, AuditArgs args, CancellationToken ct)
        => FilesProxy.CallAsync<AuditArgs, AuditResult>(gw, "acad.files.audit_database", args, T_SLOW, ct);

    [McpTool("new_document", "Create a brand new empty document based on the default template (acad.dwt) and make it active.", "files",
        Intent = new[] { "nowy rysunek", "new document", "create new dwg", "stworz nowy rysunek", "fresh drawing" },
        RequiresPlugin = true)]
    public static Task<DocumentResult> NewDocument(IPluginGateway gw, FilesEmptyArgs args, CancellationToken ct)
        => FilesProxy.CallAsync<FilesEmptyArgs, DocumentResult>(gw, "acad.files.new_document", args, T_NORMAL, ct);
}
