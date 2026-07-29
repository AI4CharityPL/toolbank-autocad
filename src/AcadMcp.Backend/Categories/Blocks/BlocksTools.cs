// AutoCAD acad-blocks category. 12 tools covering BlockTableRecord (BTR) definition
// from existing entities or external DWG, redefinition (in-place), insertion of BlockReference
// with attribute payload, explode, attribute read/write, listing of all defined blocks,
// extracting all block-reference instances of a definition, and deletion / purge.
//
// Rules: 19, 28-acad-blocks-layers-files-traps.md.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Blocks;

public static class BlocksTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;
    private const int T_SLOW = 30_000;

    [McpTool("define_block", "Define a new BlockTableRecord (BTR) in the active drawing from a list of existing entity handles. The 'origin' point becomes the block's insertion (base) point. By default the source entities are erased after copying into the BTR.", "blocks",
        Intent = new[] { "stworz blok", "zdefiniuj blok z encji", "create block from selection", "define block btr", "make block from entities" },
        RequiresPlugin = true)]
    public static Task<BlockDefResult> DefineBlock(IPluginGateway gw, DefineBlockArgs args, CancellationToken ct)
        => BlocksProxy.CallAsync<DefineBlockArgs, BlockDefResult>(gw, "acad.blocks.define_block", args, T_NORMAL, ct);

    [McpTool("define_block_from_file", "Import an external .dwg file as a new block definition; the entire model space of the source DWG becomes the block.", "blocks",
        Intent = new[] { "import bloku z dwg", "wczytaj blok z pliku", "define block from dwg", "import block from file", "block from external dwg" },
        RequiresPlugin = true)]
    public static Task<BlockDefResult> DefineBlockFromFile(IPluginGateway gw, DefineBlockFromFileArgs args, CancellationToken ct)
        => BlocksProxy.CallAsync<DefineBlockFromFileArgs, BlockDefResult>(gw, "acad.blocks.define_block_from_file", args, T_SLOW, ct);

    [McpTool("redefine_block", "Replace the geometry of an existing block definition with a new entity set; existing block references are updated automatically. Source entities are erased by default.", "blocks",
        Intent = new[] { "redefinuj blok", "podmien geometrie bloku", "redefine block", "update block definition", "replace block geometry" },
        RequiresPlugin = true)]
    public static Task<BlockDefResult> RedefineBlock(IPluginGateway gw, RedefineBlockArgs args, CancellationToken ct)
        => BlocksProxy.CallAsync<RedefineBlockArgs, BlockDefResult>(gw, "acad.blocks.redefine_block", args, T_NORMAL, ct);

    [McpTool("insert_block", "Insert a BlockReference of a defined block at the given position with optional non-uniform scale and rotation. Attributes are populated from the {tag: value} dictionary; missing tags fall back to defaults.", "blocks",
        Intent = new[] { "wstaw blok", "insert block reference", "insertuj blok", "place block at point", "block reference with attributes" },
        RequiresPlugin = true)]
    public static Task<BlockEntityResult> InsertBlock(IPluginGateway gw, InsertBlockArgs args, CancellationToken ct)
        => BlocksProxy.CallAsync<InsertBlockArgs, BlockEntityResult>(gw, "acad.blocks.insert_block", args, T_NORMAL, ct);

    [McpTool("explode_block_reference", "Explode a BlockReference into its constituent entities in model space. Attributes are converted into DBText. Returns the list of newly created entity handles.", "blocks",
        Intent = new[] { "rozbij blok", "explode block", "explode insert", "rozbij wstawienie bloku", "decompose block reference" },
        RequiresPlugin = true)]
    public static Task<BlockEntitiesResult> ExplodeBlockReference(IPluginGateway gw, ExplodeBlockRefArgs args, CancellationToken ct)
        => BlocksProxy.CallAsync<ExplodeBlockRefArgs, BlockEntitiesResult>(gw, "acad.blocks.explode_block_reference", args, T_NORMAL, ct);

    [McpTool("get_block_reference_attributes", "Return all attribute reference (AttributeReference) tag/value pairs of a single BlockReference by handle.", "blocks",
        Intent = new[] { "pobierz atrybuty bloku", "get block attributes", "read block attribute values", "list attributes of insert", "attrext one block" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<BlockAttributesResult> GetBlockReferenceAttributes(IPluginGateway gw, GetBlockRefAttributesArgs args, CancellationToken ct)
        => BlocksProxy.CallAsync<GetBlockRefAttributesArgs, BlockAttributesResult>(gw, "acad.blocks.get_block_reference_attributes", args, T_FAST, ct);

    [McpTool("set_block_reference_attributes", "Update one or more attribute reference text strings on an existing BlockReference. Tags not present in the BlockReference are silently skipped. Returns the number of attributes that were actually updated.", "blocks",
        Intent = new[] { "ustaw atrybuty bloku", "set block attributes", "update block reference attribs", "edit attribute values", "popraw atrybuty insertu" },
        RequiresPlugin = true)]
    public static Task<BlockAffectedCount> SetBlockReferenceAttributes(IPluginGateway gw, SetBlockRefAttributesArgs args, CancellationToken ct)
        => BlocksProxy.CallAsync<SetBlockRefAttributesArgs, BlockAffectedCount>(gw, "acad.blocks.set_block_reference_attributes", args, T_NORMAL, ct);

    [McpTool("list_blocks", "List every block definition in the drawing (excluding *Model_Space, *Paper_Space and other AutoCAD-internal records). Reports anonymous, dynamic and Xref flags.", "blocks",
        Intent = new[] { "wylistuj bloki", "list all blocks", "show block definitions", "wszystkie bloki", "what blocks exist" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<BlockListResult> ListBlocks(IPluginGateway gw, BlocksEmptyArgs args, CancellationToken ct)
        => BlocksProxy.CallAsync<BlocksEmptyArgs, BlockListResult>(gw, "acad.blocks.list_blocks", args, T_NORMAL, ct);

    [McpTool("extract_block_references", "Find every BlockReference (insert) of a given block definition (or all blocks if name is omitted). Returns each insert's position, scale, rotation, layer and attributes.", "blocks",
        Intent = new[] { "znajdz wstawienia bloku", "extract block references", "list inserts of block", "find all instances of block", "wszystkie inserty bloku" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<BlockReferencesResult> ExtractBlockReferences(IPluginGateway gw, ExtractBlockReferencesArgs args, CancellationToken ct)
        => BlocksProxy.CallAsync<ExtractBlockReferencesArgs, BlockReferencesResult>(gw, "acad.blocks.extract_block_references", args, T_SLOW, ct);

    [McpTool("delete_block_definition", "Delete a block definition (BTR). Only succeeds if no BlockReference uses it. Use purge_unused_blocks to remove every unused block in one call.", "blocks",
        Intent = new[] { "usun definicje bloku", "delete block definition", "skasuj blok", "remove block btr", "drop block" },
        RequiresPlugin = true)]
    public static Task<BlockAffectedCount> DeleteBlockDefinition(IPluginGateway gw, BlockNameArg args, CancellationToken ct)
        => BlocksProxy.CallAsync<BlockNameArg, BlockAffectedCount>(gw, "acad.blocks.delete_block_definition", args, T_NORMAL, ct);

    [McpTool("purge_unused_blocks", "Purge every block definition that has no BlockReference and is not an internal record (Model_Space, Paper_Space, anonymous *D / *E records). Returns the count removed.", "blocks",
        Intent = new[] { "wyczysc bloki", "purge unused blocks", "remove empty block defs", "wyczysc nieuzywane bloki", "block purge" },
        RequiresPlugin = true)]
    public static Task<BlockAffectedCount> PurgeUnusedBlocks(IPluginGateway gw, BlocksEmptyArgs args, CancellationToken ct)
        => BlocksProxy.CallAsync<BlocksEmptyArgs, BlockAffectedCount>(gw, "acad.blocks.purge_unused_blocks", args, T_NORMAL, ct);

    [McpTool("rename_block", "Rename a block definition. Cannot rename Model_Space / Paper_Space, anonymous blocks (starting with '*') or Xrefs.", "blocks",
        Intent = new[] { "zmien nazwe bloku", "rename block", "przemianuj blok", "change block name", "rename block to" },
        RequiresPlugin = true)]
    public static Task<BlockDefResult> RenameBlock(IPluginGateway gw, RenameBlockArgs args, CancellationToken ct)
        => BlocksProxy.CallAsync<RenameBlockArgs, BlockDefResult>(gw, "acad.blocks.rename_block", args, T_NORMAL, ct);

    [McpTool("library_register", "Register a filesystem folder as a named block library. The path is scanned for .dwg files (recursively by default) and persisted to a user-scoped catalog so subsequent sessions remember it. Libraries are consumed by bulk_insert and swap_block (auto-import).", "blocks",
        Intent = new[] { "zarejestruj biblioteke blokow", "register block library", "add block library path", "dodaj folder blokow", "link dwg folder as library" },
        RequiresPlugin = true)]
    public static Task<LibraryRegisterResult> LibraryRegister(IPluginGateway gw, LibraryRegisterArgs args, CancellationToken ct)
        => BlocksProxy.CallAsync<LibraryRegisterArgs, LibraryRegisterResult>(gw, "acad.blocks.library_register", args, T_NORMAL, ct);

    [McpTool("library_list", "List every registered block library. When libraryName is given, only that library is returned and its .dwg file list is enumerated (when includeFiles=true, default).", "blocks",
        Intent = new[] { "wylistuj biblioteki blokow", "list block libraries", "show registered block folders", "what libraries", "enumerate dwg files in library" },
        RequiresPlugin = true,
        ReadOnly = true)]
    public static Task<LibraryListResult> LibraryList(IPluginGateway gw, LibraryListArgs args, CancellationToken ct)
        => BlocksProxy.CallAsync<LibraryListArgs, LibraryListResult>(gw, "acad.blocks.library_list", args, T_FAST, ct);

    [McpTool("bulk_insert", "Insert many BlockReferences in one pass. For each item: if the block name is already defined it is reused; otherwise (autoImport=true) the plugin searches every registered library (or only libraryName if given) for a matching <blockName>.dwg and imports it as a new definition before inserting. Attributes / scale / rotation / layer per item.", "blocks",
        Intent = new[] { "wstaw masowo bloki", "bulk insert blocks", "insert many blocks at once", "wstaw wiele blokow", "mass-insert block references" },
        RequiresPlugin = true)]
    public static Task<BulkInsertResult> BulkInsert(IPluginGateway gw, BulkInsertArgs args, CancellationToken ct)
        => BlocksProxy.CallAsync<BulkInsertArgs, BulkInsertResult>(gw, "acad.blocks.bulk_insert", args, T_SLOW, ct);

    [McpTool("swap_block", "Globally replace every BlockReference of oldName with newName, preserving position, rotation, scale and layer. When keepAttributes=true, compatible attribute tag/value pairs are copied onto the new BlockReference. If newName is not defined yet and autoImport=true, it is imported from the registered libraries first.", "blocks",
        Intent = new[] { "podmien blok", "swap block", "replace all inserts of block", "podmien wszystkie wstawienia bloku", "substitute block definition globally" },
        RequiresPlugin = true)]
    public static Task<SwapBlockResult> SwapBlock(IPluginGateway gw, SwapBlockArgs args, CancellationToken ct)
        => BlocksProxy.CallAsync<SwapBlockArgs, SwapBlockResult>(gw, "acad.blocks.swap_block", args, T_SLOW, ct);
}

public sealed record RenameBlockArgs(
    [property: System.Text.Json.Serialization.JsonPropertyName("oldName")] string OldName,
    [property: System.Text.Json.Serialization.JsonPropertyName("newName")] string NewName);
