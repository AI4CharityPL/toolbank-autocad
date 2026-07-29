// AutoCAD plugin handlers for the acad-blocks category.
// Registered under "acad.blocks.<verb>"; everything runs on the UI thread.
//
// Rules: 10, 11, 12, 19, 28-acad-blocks-layers-files-traps.md.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace AcadMcp.Plugin.Tools;

internal static class BlocksPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.blocks.define_block",                     DefineBlock);
        host.Register("acad.blocks.define_block_from_file",           DefineBlockFromFile);
        host.Register("acad.blocks.redefine_block",                   RedefineBlock);
        host.Register("acad.blocks.insert_block",                     InsertBlock);
        host.Register("acad.blocks.explode_block_reference",          ExplodeBlockReference);
        host.Register("acad.blocks.get_block_reference_attributes",   GetBlockReferenceAttributes);
        host.Register("acad.blocks.set_block_reference_attributes",   SetBlockReferenceAttributes);
        host.Register("acad.blocks.list_blocks",                      ListBlocks);
        host.Register("acad.blocks.extract_block_references",         ExtractBlockReferences);
        host.Register("acad.blocks.delete_block_definition",          DeleteBlockDefinition);
        host.Register("acad.blocks.purge_unused_blocks",              PurgeUnusedBlocks);
        host.Register("acad.blocks.rename_block",                     RenameBlock);
        host.Register("acad.blocks.library_register",                 LibraryRegister);
        host.Register("acad.blocks.library_list",                     LibraryList);
        host.Register("acad.blocks.bulk_insert",                      BulkInsert);
        host.Register("acad.blocks.swap_block",                       SwapBlock);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");
    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct, Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    // ─────────── helpers ───────────

    private static bool IsLayoutBlockName(string name) =>
        name.StartsWith("*Model_Space", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("*Paper_Space", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("*MODEL_SPACE", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("*PAPER_SPACE", StringComparison.OrdinalIgnoreCase);

    // ─────────── definition ───────────

    private static Task<ToolDispatchResult> DefineBlock(JsonObject args, CancellationToken ct) =>
        Run("acad.blocks.define_block", args, ct, (doc, db, tr) =>
        {
            var a = Read<DefineBlockArgsDto>(args);
            AcadEnv.ValidateSymbolName(a.Name, "Block");
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
            if (bt.Has(a.Name))
                throw new InvalidOperationException($"Block '{a.Name}' already exists. Use redefine_block to update its geometry.");

            var btr = new BlockTableRecord
            {
                Name   = a.Name,
                Origin = AcadEnv.ToPoint3d(a.Origin),
                Comments = a.Description ?? string.Empty,
            };
            var btrId = bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);

            int copied = 0;
            foreach (var h in a.MemberHandles)
            {
                var src = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForWrite);
                var clone = (Entity)src.Clone();
                btr.AppendEntity(clone);
                tr.AddNewlyCreatedDBObject(clone, true);
                if (a.EraseSource) src.Erase(true);
                copied++;
            }

            return Wrap(new BlockDefResultDto(a.Name, btrId.Handle.ToString(), copied));
        });

    private static Task<ToolDispatchResult> DefineBlockFromFile(JsonObject args, CancellationToken ct) =>
        Run("acad.blocks.define_block_from_file", args, ct, (doc, db, tr) =>
        {
            var a = Read<DefineBlockFromFileArgsDto>(args);
            AcadEnv.ValidateSymbolName(a.Name, "Block");
            if (!File.Exists(a.DwgPath)) throw new FileNotFoundException($"DWG '{a.DwgPath}' not found.", a.DwgPath);

            using var src = new Database(false, true);
            src.ReadDwgFile(a.DwgPath, FileShare.Read, allowCPConversion: true, password: "");
            // Trap rule 28 #4: Insert() merges source model space into a new BTR.
            var newId = db.Insert(a.Name, src, false);
            return Wrap(new BlockDefResultDto(a.Name, newId.Handle.ToString(), -1));
        });

    private static Task<ToolDispatchResult> RedefineBlock(JsonObject args, CancellationToken ct) =>
        Run("acad.blocks.redefine_block", args, ct, (doc, db, tr) =>
        {
            var a = Read<RedefineBlockArgsDto>(args);
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            if (!bt.Has(a.Name)) throw new ArgumentException($"Block '{a.Name}' does not exist. Use define_block first.");
            var btr = (BlockTableRecord)tr.GetObject(bt[a.Name], OpenMode.ForWrite);

            // Erase existing children of the BTR.
            foreach (ObjectId id in btr)
            {
                var e = (Entity)tr.GetObject(id, OpenMode.ForWrite);
                e.Erase(true);
            }
            btr.Origin = AcadEnv.ToPoint3d(a.Origin);

            int copied = 0;
            foreach (var h in a.MemberHandles)
            {
                var src = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForWrite);
                var clone = (Entity)src.Clone();
                btr.AppendEntity(clone);
                tr.AddNewlyCreatedDBObject(clone, true);
                if (a.EraseSource) src.Erase(true);
                copied++;
            }

            // Force regeneration of all references.
            db.TransactionManager.QueueForGraphicsFlush();
            return Wrap(new BlockDefResultDto(a.Name, btr.ObjectId.Handle.ToString(), copied));
        });

    // ─────────── insertion / explode ───────────

    private static Task<ToolDispatchResult> InsertBlock(JsonObject args, CancellationToken ct) =>
        Run("acad.blocks.insert_block", args, ct, (doc, db, tr) =>
        {
            var a = Read<InsertBlockArgsDto>(args);
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            if (!bt.Has(a.Name)) throw new ArgumentException($"Block '{a.Name}' is not defined.");
            var btrId = bt[a.Name];

            var br = new BlockReference(AcadEnv.ToPoint3d(a.Position), btrId)
            {
                ScaleFactors = new Scale3d(a.ScaleX, a.ScaleY, a.ScaleZ),
                Rotation     = a.RotationDeg * Math.PI / 180.0,
            };
            var handle = AcadEnv.Persist(db, tr, br, a.Layer);

            // Materialise attribute references for any AttributeDefinitions in the BTR (rule 28 trap #5).
            var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
            if (btr.HasAttributeDefinitions)
            {
                foreach (ObjectId id in btr)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead);
                    if (ent is AttributeDefinition def && !def.Constant)
                    {
                        var ar = new AttributeReference();
                        ar.SetAttributeFromBlock(def, br.BlockTransform);
                        if (a.Attributes is not null && a.Attributes.TryGetValue(def.Tag, out var v))
                            ar.TextString = v ?? "";
                        br.AttributeCollection.AppendAttribute(ar);
                        tr.AddNewlyCreatedDBObject(ar, true);
                    }
                }
            }
            return Wrap(new { entity = handle });
        });

    private static Task<ToolDispatchResult> ExplodeBlockReference(JsonObject args, CancellationToken ct) =>
        Run("acad.blocks.explode_block_reference", args, ct, (doc, db, tr) =>
        {
            var a = Read<ExplodeBlockRefArgsDto>(args);
            var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle), OpenMode.ForWrite);
            if (ent is not BlockReference br)
                throw new ArgumentException($"Handle '{a.Handle}' is a {ent.GetRXClass().Name}, not BlockReference.");

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            using var pieces = new DBObjectCollection();
            br.Explode(pieces);
            var handles = new List<EntityHandle>();
            foreach (DBObject obj in pieces)
            {
                if (obj is Entity e)
                {
                    ms.AppendEntity(e);
                    tr.AddNewlyCreatedDBObject(e, true);
                    handles.Add(AcadEnv.ToHandle(e));
                }
            }
            br.Erase(true);
            return Wrap(new { entities = handles });
        });

    // ─────────── attributes ───────────

    private static Task<ToolDispatchResult> GetBlockReferenceAttributes(JsonObject args, CancellationToken ct) =>
        Run("acad.blocks.get_block_reference_attributes", args, ct, (doc, db, tr) =>
        {
            var a = Read<GetBlockRefAttributesArgsDto>(args);
            var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle), OpenMode.ForRead);
            if (ent is not BlockReference br)
                throw new ArgumentException($"Handle '{a.Handle}' is a {ent.GetRXClass().Name}, not BlockReference.");
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (ObjectId id in br.AttributeCollection)
            {
                var ar = (AttributeReference)tr.GetObject(id, OpenMode.ForRead);
                dict[ar.Tag] = ar.TextString ?? "";
            }
            return Wrap(new { attributes = dict });
        });

    private static Task<ToolDispatchResult> SetBlockReferenceAttributes(JsonObject args, CancellationToken ct) =>
        Run("acad.blocks.set_block_reference_attributes", args, ct, (doc, db, tr) =>
        {
            var a = Read<SetBlockRefAttributesArgsDto>(args);
            var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle), OpenMode.ForWrite);
            if (ent is not BlockReference br)
                throw new ArgumentException($"Handle '{a.Handle}' is a {ent.GetRXClass().Name}, not BlockReference.");
            int n = 0;
            foreach (ObjectId id in br.AttributeCollection)
            {
                var ar = (AttributeReference)tr.GetObject(id, OpenMode.ForWrite);
                if (a.Attributes.TryGetValue(ar.Tag, out var v))
                {
                    ar.TextString = v ?? "";
                    n++;
                }
            }
            return Wrap(new { affected = n });
        });

    // ─────────── inventory / extract ───────────

    private static Task<ToolDispatchResult> ListBlocks(JsonObject args, CancellationToken ct) =>
        Run("acad.blocks.list_blocks", args, ct, (doc, db, tr) =>
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var list = new List<BlockDefSummaryDto>();
            foreach (ObjectId id in bt)
            {
                var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);
                if (IsLayoutBlockName(btr.Name)) continue;
                int count = 0; foreach (var _ in btr) count++;
                list.Add(new BlockDefSummaryDto(
                    Name: btr.Name,
                    IsAnonymous: btr.IsAnonymous,
                    IsDynamic: btr.IsDynamicBlock,
                    IsXref: btr.IsFromExternalReference || btr.IsFromOverlayReference,
                    EntityCount: count,
                    Description: string.IsNullOrEmpty(btr.Comments) ? null : btr.Comments));
            }
            return Wrap(new { blocks = list });
        });

    private static Task<ToolDispatchResult> ExtractBlockReferences(JsonObject args, CancellationToken ct) =>
        Run("acad.blocks.extract_block_references", args, ct, (doc, db, tr) =>
        {
            var a = Read<ExtractBlockReferencesArgsDto>(args);
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var refs = new List<BlockRefInfoDto>();
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                if (ent is not BlockReference br) continue;
                var btr = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                if (!string.IsNullOrWhiteSpace(a.Name) && !string.Equals(btr.Name, a.Name, StringComparison.OrdinalIgnoreCase))
                    continue;
                var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (ObjectId aId in br.AttributeCollection)
                {
                    var ar = (AttributeReference)tr.GetObject(aId, OpenMode.ForRead);
                    attrs[ar.Tag] = ar.TextString ?? "";
                }
                refs.Add(new BlockRefInfoDto(
                    Handle: br.Handle.ToString(),
                    Name: btr.Name,
                    Position: AcadEnv.FromPoint3d(br.Position),
                    RotationDeg: br.Rotation * 180.0 / Math.PI,
                    ScaleX: br.ScaleFactors.X,
                    ScaleY: br.ScaleFactors.Y,
                    ScaleZ: br.ScaleFactors.Z,
                    Layer: br.Layer,
                    Attributes: attrs));
            }
            return Wrap(new { references = refs });
        });

    // ─────────── delete / purge / rename ───────────

    private static Task<ToolDispatchResult> DeleteBlockDefinition(JsonObject args, CancellationToken ct) =>
        Run("acad.blocks.delete_block_definition", args, ct, (doc, db, tr) =>
        {
            var a = Read<BlockNameArgDto>(args);
            if (IsLayoutBlockName(a.Name))
                throw new InvalidOperationException($"Cannot delete protected block '{a.Name}'.");
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            if (!bt.Has(a.Name)) return Wrap(new { affected = 0 });
            var btr = (BlockTableRecord)tr.GetObject(bt[a.Name], OpenMode.ForWrite);
            using var refIds = btr.GetBlockReferenceIds(true, true);
            if (refIds.Count > 0)
                throw new InvalidOperationException($"Block '{a.Name}' has {refIds.Count} active reference(s). Erase them or use purge_unused_blocks.");
            btr.Erase(true);
            return Wrap(new { affected = 1 });
        });

    private static Task<ToolDispatchResult> PurgeUnusedBlocks(JsonObject args, CancellationToken ct) =>
        Run("acad.blocks.purge_unused_blocks", args, ct, (doc, db, tr) =>
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ids = new ObjectIdCollection();
            foreach (ObjectId id in bt) ids.Add(id);
            db.Purge(ids);
            int removed = 0;
            foreach (ObjectId id in ids)
            {
                try
                {
                    var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForWrite);
                    if (IsLayoutBlockName(btr.Name)) continue;
                    btr.Erase(true);
                    removed++;
                }
                catch { /* skip */ }
            }
            return Wrap(new { affected = removed });
        });

    private static Task<ToolDispatchResult> RenameBlock(JsonObject args, CancellationToken ct) =>
        Run("acad.blocks.rename_block", args, ct, (doc, db, tr) =>
        {
            var a = Read<RenameBlockArgsDto>(args);
            if (IsLayoutBlockName(a.OldName) || a.OldName.StartsWith("*"))
                throw new InvalidOperationException($"Block '{a.OldName}' is internal/anonymous and cannot be renamed.");
            AcadEnv.ValidateSymbolName(a.NewName, "Block");
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            if (!bt.Has(a.OldName)) throw new ArgumentException($"Block '{a.OldName}' does not exist.");
            if (bt.Has(a.NewName)) throw new InvalidOperationException($"Block '{a.NewName}' already exists.");
            var btr = (BlockTableRecord)tr.GetObject(bt[a.OldName], OpenMode.ForWrite);
            if (btr.IsFromExternalReference) throw new InvalidOperationException($"Block '{a.OldName}' is an Xref and cannot be renamed.");
            btr.Name = a.NewName;
            return Wrap(new BlockDefResultDto(a.NewName, btr.ObjectId.Handle.ToString(), -1));
        });

    // ─────────── D6: file-persistent block libraries ───────────

    private static string CatalogPath()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AcadMcp");
        Directory.CreateDirectory(root);
        return Path.Combine(root, "block-libraries.json");
    }

    private static Dictionary<string, LibraryEntry> LoadCatalog()
    {
        var path = CatalogPath();
        if (!File.Exists(path)) return new(StringComparer.OrdinalIgnoreCase);
        try
        {
            var json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize<List<LibraryEntry>>(json, Opts) ?? new();
            var dict = new Dictionary<string, LibraryEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in list) dict[e.LibraryName] = e;
            return dict;
        }
        catch { return new(StringComparer.OrdinalIgnoreCase); }
    }

    private static void SaveCatalog(Dictionary<string, LibraryEntry> cat)
    {
        var list = new List<LibraryEntry>(cat.Values);
        File.WriteAllText(CatalogPath(), JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static List<string> EnumerateDwgs(LibraryEntry lib)
    {
        if (!Directory.Exists(lib.Path)) return new List<string>();
        var opt = lib.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return new List<string>(Directory.EnumerateFiles(lib.Path, "*.dwg", opt));
    }

    private static string? FindDwgInLibraries(string blockName, string? restrictTo, Dictionary<string, LibraryEntry> cat)
    {
        var wanted = blockName + ".dwg";
        IEnumerable<LibraryEntry> candidates = restrictTo is null
            ? cat.Values
            : (cat.TryGetValue(restrictTo, out var one) ? new[] { one } : Array.Empty<LibraryEntry>());
        foreach (var lib in candidates)
        {
            foreach (var f in EnumerateDwgs(lib))
            {
                if (string.Equals(Path.GetFileName(f), wanted, StringComparison.OrdinalIgnoreCase))
                    return f;
            }
        }
        return null;
    }

    private static Task<ToolDispatchResult> LibraryRegister(JsonObject args, CancellationToken ct) =>
        Run("acad.blocks.library_register", args, ct, (doc, db, tr) =>
        {
            var a = Read<LibraryRegisterArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.LibraryName))
                throw new ArgumentException("libraryName must be non-empty.");
            if (!Directory.Exists(a.Path))
                throw new DirectoryNotFoundException($"Library path '{a.Path}' does not exist.");

            var cat = LoadCatalog();
            if (cat.ContainsKey(a.LibraryName) && !a.Overwrite)
                throw new InvalidOperationException($"Library '{a.LibraryName}' already registered. Pass overwrite=true to replace.");

            var entry = new LibraryEntry(a.LibraryName, a.Path, a.Recursive);
            cat[a.LibraryName] = entry;
            SaveCatalog(cat);
            int dwgCount = EnumerateDwgs(entry).Count;
            return Wrap(new { libraryName = a.LibraryName, path = a.Path, dwgCount, registered = true });
        });

    private static Task<ToolDispatchResult> LibraryList(JsonObject args, CancellationToken ct) =>
        Run("acad.blocks.library_list", args, ct, (doc, db, tr) =>
        {
            var a = Read<LibraryListArgsDto>(args);
            var cat = LoadCatalog();
            IEnumerable<LibraryEntry> entries = a.LibraryName is null
                ? cat.Values
                : (cat.TryGetValue(a.LibraryName, out var one) ? new[] { one } : Array.Empty<LibraryEntry>());

            var result = new List<object>();
            foreach (var lib in entries)
            {
                var files = EnumerateDwgs(lib);
                result.Add(new
                {
                    libraryName = lib.LibraryName,
                    path = lib.Path,
                    recursive = lib.Recursive,
                    dwgCount = files.Count,
                    files = a.IncludeFiles ? (object)files : null,
                });
            }
            return Wrap(new { libraries = result });
        });

    private static Task<ToolDispatchResult> BulkInsert(JsonObject args, CancellationToken ct) =>
        Run("acad.blocks.bulk_insert", args, ct, (doc, db, tr) =>
        {
            var a = Read<BulkInsertArgsDto>(args);
            if (a.Items is null || a.Items.Count == 0)
                throw new ArgumentException("items must be non-empty.");

            var cat = LoadCatalog();
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
            var imported = new List<string>();
            var inserted = new List<EntityHandle>();
            int skipped = 0;

            foreach (var item in a.Items)
            {
                if (!bt.Has(item.BlockName))
                {
                    if (!a.AutoImport) { skipped++; continue; }
                    var dwg = FindDwgInLibraries(item.BlockName, a.LibraryName, cat);
                    if (dwg is null) { skipped++; continue; }
                    using var src = new Database(false, true);
                    src.ReadDwgFile(dwg, FileShare.Read, allowCPConversion: true, password: "");
                    db.Insert(item.BlockName, src, false);
                    imported.Add(item.BlockName);
                }

                var btrId = bt[item.BlockName];
                var br = new BlockReference(AcadEnv.ToPoint3d(item.Position), btrId)
                {
                    ScaleFactors = new Scale3d(item.ScaleX, item.ScaleY, item.ScaleZ),
                    Rotation     = item.RotationDeg * Math.PI / 180.0,
                };
                var handle = AcadEnv.Persist(db, tr, br, item.Layer);

                var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                if (btr.HasAttributeDefinitions)
                {
                    foreach (ObjectId id in btr)
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead);
                        if (ent is AttributeDefinition def && !def.Constant)
                        {
                            var ar = new AttributeReference();
                            ar.SetAttributeFromBlock(def, br.BlockTransform);
                            if (item.Attributes is not null && item.Attributes.TryGetValue(def.Tag, out var v))
                                ar.TextString = v ?? "";
                            br.AttributeCollection.AppendAttribute(ar);
                            tr.AddNewlyCreatedDBObject(ar, true);
                        }
                    }
                }
                inserted.Add(handle);
            }

            return Wrap(new { inserted, imported, skipped });
        });

    private static Task<ToolDispatchResult> SwapBlock(JsonObject args, CancellationToken ct) =>
        Run("acad.blocks.swap_block", args, ct, (doc, db, tr) =>
        {
            var a = Read<SwapBlockArgsDto>(args);
            if (string.Equals(a.OldName, a.NewName, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("oldName and newName must differ. Use redefine_block to update geometry in-place.");

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
            if (!bt.Has(a.OldName)) throw new ArgumentException($"Block '{a.OldName}' is not defined.");

            bool importedNow = false;
            if (!bt.Has(a.NewName))
            {
                if (!a.AutoImport) throw new ArgumentException($"Block '{a.NewName}' is not defined and autoImport=false.");
                var cat = LoadCatalog();
                var dwg = FindDwgInLibraries(a.NewName, a.LibraryName, cat)
                          ?? throw new FileNotFoundException($"No .dwg named '{a.NewName}.dwg' found in registered libraries.");
                using var src = new Database(false, true);
                src.ReadDwgFile(dwg, FileShare.Read, allowCPConversion: true, password: "");
                db.Insert(a.NewName, src, false);
                importedNow = true;
            }
            var newBtrId = bt[a.NewName];
            var newBtr   = (BlockTableRecord)tr.GetObject(newBtrId, OpenMode.ForRead);

            // Pre-index AttributeDefinitions of the new BTR for attribute transfer.
            var newDefs = new List<AttributeDefinition>();
            if (newBtr.HasAttributeDefinitions)
            {
                foreach (ObjectId id in newBtr)
                {
                    var e = tr.GetObject(id, OpenMode.ForRead);
                    if (e is AttributeDefinition def && !def.Constant) newDefs.Add(def);
                }
            }

            var oldBtr = (BlockTableRecord)tr.GetObject(bt[a.OldName], OpenMode.ForRead);
            using var refIds = oldBtr.GetBlockReferenceIds(directOnly: true, forceValidity: true);

            int replaced = 0;
            foreach (ObjectId brId in refIds)
            {
                var br = (BlockReference)tr.GetObject(brId, OpenMode.ForWrite);
                var existingAttrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (a.KeepAttributes)
                {
                    foreach (ObjectId aId in br.AttributeCollection)
                    {
                        var ar = (AttributeReference)tr.GetObject(aId, OpenMode.ForRead);
                        existingAttrs[ar.Tag] = ar.TextString ?? "";
                    }
                }

                // Drop the old attribute refs before swapping the BTR so the collection matches the new definition.
                foreach (ObjectId aId in br.AttributeCollection)
                {
                    var ar = (AttributeReference)tr.GetObject(aId, OpenMode.ForWrite);
                    ar.Erase(true);
                }

                br.BlockTableRecord = newBtrId;

                foreach (var def in newDefs)
                {
                    var ar = new AttributeReference();
                    ar.SetAttributeFromBlock(def, br.BlockTransform);
                    if (existingAttrs.TryGetValue(def.Tag, out var v)) ar.TextString = v ?? "";
                    br.AttributeCollection.AppendAttribute(ar);
                    tr.AddNewlyCreatedDBObject(ar, true);
                }
                replaced++;
            }

            return Wrap(new { replaced, imported = importedNow });
        });
}

internal sealed record LibraryEntry(
    [property: System.Text.Json.Serialization.JsonPropertyName("libraryName")] string LibraryName,
    [property: System.Text.Json.Serialization.JsonPropertyName("path")]        string Path,
    [property: System.Text.Json.Serialization.JsonPropertyName("recursive")]   bool Recursive);

internal sealed record BlockDefResultDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("name")] string Name,
    [property: System.Text.Json.Serialization.JsonPropertyName("ownerHandle")] string OwnerHandle,
    [property: System.Text.Json.Serialization.JsonPropertyName("entityCount")] int EntityCount = -1);

internal sealed record BlockDefSummaryDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("name")]        string Name,
    [property: System.Text.Json.Serialization.JsonPropertyName("isAnonymous")] bool IsAnonymous,
    [property: System.Text.Json.Serialization.JsonPropertyName("isDynamic")]   bool IsDynamic,
    [property: System.Text.Json.Serialization.JsonPropertyName("isXref")]      bool IsXref,
    [property: System.Text.Json.Serialization.JsonPropertyName("entityCount")] int EntityCount,
    [property: System.Text.Json.Serialization.JsonPropertyName("description")] string? Description);

internal sealed record BlockRefInfoDto(
    [property: System.Text.Json.Serialization.JsonPropertyName("handle")]      string Handle,
    [property: System.Text.Json.Serialization.JsonPropertyName("name")]        string Name,
    [property: System.Text.Json.Serialization.JsonPropertyName("position")]    Point3dDto Position,
    [property: System.Text.Json.Serialization.JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: System.Text.Json.Serialization.JsonPropertyName("scaleX")]      double ScaleX,
    [property: System.Text.Json.Serialization.JsonPropertyName("scaleY")]      double ScaleY,
    [property: System.Text.Json.Serialization.JsonPropertyName("scaleZ")]      double ScaleZ,
    [property: System.Text.Json.Serialization.JsonPropertyName("layer")]       string Layer,
    [property: System.Text.Json.Serialization.JsonPropertyName("attributes")]  IReadOnlyDictionary<string, string> Attributes);
