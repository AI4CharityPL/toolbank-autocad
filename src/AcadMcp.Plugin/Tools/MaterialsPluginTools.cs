// AutoCAD plugin handlers for the acad-materials category (roadmap 6.1, first tranche).
// Registered under "acad.materials.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern), 26 (traps).
//
// MEASURED API shape, and the namespace is the first surprise:
//
//   A Material lives in DatabaseServices, but every one of its CHANNELS is a
//   Autodesk.AutoCAD.GraphicsInterface type - MaterialColor, MaterialMap,
//   MaterialDiffuseComponent, MaterialSpecularComponent, MaterialOpacityComponent. Looking for
//   them beside Material is the natural mistake and they are not there.
//
//   MaterialColor takes an EntityColor, not a Color.
//
//   Material.Shininess does NOT exist. Shininess is the Gloss of the Specular component.
//
//   A channel is a STRUCT read whole and written whole: `m.Diffuse.Color = x` does not compile in
//   any useful sense, because m.Diffuse returns a copy. The component has to be rebuilt and
//   assigned back, which is why every setter here reads, rebuilds and writes.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using AcadRt = Autodesk.AutoCAD.Runtime;
using GI = Autodesk.AutoCAD.GraphicsInterface;

namespace AcadMcp.Plugin.Tools;

internal static class MaterialsPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.materials.create_material",   CreateMaterial);
        host.Register("acad.materials.modify_material",   ModifyMaterial);
        host.Register("acad.materials.list_materials",    ListMaterials);
        host.Register("acad.materials.assign_material",   AssignMaterial);
        host.Register("acad.materials.unassign_material", UnassignMaterial);
        host.Register("acad.materials.delete_material",   DeleteMaterial);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    private static DBDictionary Materials(Database db, Transaction tr, OpenMode mode) =>
        (DBDictionary)tr.GetObject(db.MaterialDictionaryId, mode);

    private static Material RequireMaterial(Database db, Transaction tr, string? name, OpenMode mode)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("name is required: which material.");
        var dict = Materials(db, tr, OpenMode.ForRead);
        if (!dict.Contains(name!))
            throw new ArgumentException(
                "No material called '" + name + "' in this drawing. list_materials shows what is " +
                "there; every drawing has at least Global.");
        return (Material)tr.GetObject(dict.GetAt(name!), mode);
    }

    private static GI.MaterialColor MakeColor(ColorDto? c, double? factor)
    {
        var ec = c is null
            ? new EntityColor(255, 255, 255)
            : new EntityColor((byte)Clamp255(c.R), (byte)Clamp255(c.G), (byte)Clamp255(c.B));
        // Method.Override means "use this colour", as against Method.Inherit which takes the
        // entity's own. Anything set here is meant literally, so Override is right.
        return new GI.MaterialColor(GI.Method.Override, factor ?? 1.0, ec);
    }

    private static int Clamp255(int v) => v < 0 ? 0 : v > 255 ? 255 : v;

    private static object DescribeColor(GI.MaterialColor c) => new
    {
        r = (int)c.Color.Red, g = (int)c.Color.Green, b = (int)c.Color.Blue,
        factor = c.Factor,
        method = c.Method.ToString(),
    };

    private static object Describe(Material m) => new
    {
        name = m.Name,
        description = m.Description,
        diffuse = DescribeColor(m.Diffuse.Color),
        specular = DescribeColor(m.Specular.Color),
        // MEASURED: there is no Material.Shininess - it is the Gloss of the specular component.
        gloss = m.Specular.Gloss,
        opacity = m.Opacity.Percentage,
        handle = m.Handle.ToString(),
    };

    // ─────────── making and changing ───────────

    private static Task<ToolDispatchResult> CreateMaterial(JsonObject args, CancellationToken ct) =>
        Run("acad.materials.create_material", args, ct, (doc, db, tr) =>
        {
            var a = Read<MaterialArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name))
                throw new ArgumentException("name is required: what to call the material.");
            if (a.Opacity is not null && (a.Opacity < 0 || a.Opacity > 1))
                throw new ArgumentException("opacity runs from 0 (clear) to 1 (solid).");
            if (a.Gloss is not null && (a.Gloss < 0 || a.Gloss > 1))
                throw new ArgumentException("gloss runs from 0 (matt) to 1 (mirror-sharp).");

            var dict = Materials(db, tr, OpenMode.ForWrite);
            if (dict.Contains(a.Name!))
                throw new ArgumentException(
                    "A material called '" + a.Name + "' already exists. modify_material changes " +
                    "one; replacing it silently would restyle every object already using it.");

            var m = new Material { Name = a.Name!, Description = a.Description ?? "" };
            var map = new GI.MaterialMap();
            m.Diffuse = new GI.MaterialDiffuseComponent(MakeColor(a.Diffuse, a.DiffuseFactor), map);
            m.Specular = new GI.MaterialSpecularComponent(
                MakeColor(a.Specular, null), map, a.Gloss ?? 0.5);
            m.Opacity = new GI.MaterialOpacityComponent(a.Opacity ?? 1.0, map);

            dict.SetAt(a.Name!, m);
            tr.AddNewlyCreatedDBObject(m, true);

            // Read back through a FRESH dictionary lookup rather than trusting the object written.
            var back = RequireMaterial(db, tr, a.Name, OpenMode.ForRead);
            return Wrap(new
            {
                material = Describe(back),
                note = "Created in the drawing's material dictionary. A material is not attached to " +
                       "anything until assign_material puts it on an entity. Note that gloss is " +
                       "the SPECULAR component's gloss - there is no Material.Shininess in the " +
                       "API, which is the property name most people reach for first. Read back " +
                       "through a fresh dictionary lookup rather than echoed.",
            });
        });

    private static Task<ToolDispatchResult> ModifyMaterial(JsonObject args, CancellationToken ct) =>
        Run("acad.materials.modify_material", args, ct, (doc, db, tr) =>
        {
            var a = Read<MaterialArgsDto>(args);
            if (a.Diffuse is null && a.Specular is null && a.Gloss is null && a.Opacity is null
                && a.Description is null && a.DiffuseFactor is null)
                throw new ArgumentException(
                    "Nothing to change. Give at least one of diffuse, diffuseFactor, specular, " +
                    "gloss, opacity or description.");
            if (a.Opacity is not null && (a.Opacity < 0 || a.Opacity > 1))
                throw new ArgumentException("opacity runs from 0 (clear) to 1 (solid).");
            if (a.Gloss is not null && (a.Gloss < 0 || a.Gloss > 1))
                throw new ArgumentException("gloss runs from 0 (matt) to 1 (mirror-sharp).");

            var m = RequireMaterial(db, tr, a.Name, OpenMode.ForWrite);
            var before = Describe(m);
            var changed = new List<string>();

            // Each channel is a STRUCT: m.Diffuse hands back a COPY, so mutating it changes
            // nothing. The component has to be rebuilt and assigned back whole, which is the
            // single most surprising thing about this API and the reason for the ceremony below.
            if (a.Diffuse is not null || a.DiffuseFactor is not null)
            {
                var cur = m.Diffuse;
                var col = a.Diffuse is not null
                    ? MakeColor(a.Diffuse, a.DiffuseFactor ?? cur.Color.Factor)
                    : new GI.MaterialColor(cur.Color.Method, a.DiffuseFactor!.Value, cur.Color.Color);
                m.Diffuse = new GI.MaterialDiffuseComponent(col, cur.Map);
                changed.Add(a.Diffuse is not null ? "diffuse" : "diffuseFactor");
            }
            if (a.Specular is not null || a.Gloss is not null)
            {
                var cur = m.Specular;
                var col = a.Specular is not null ? MakeColor(a.Specular, null) : cur.Color;
                m.Specular = new GI.MaterialSpecularComponent(col, cur.Map, a.Gloss ?? cur.Gloss);
                if (a.Specular is not null) changed.Add("specular");
                if (a.Gloss is not null) changed.Add("gloss");
            }
            if (a.Opacity is not null)
            {
                m.Opacity = new GI.MaterialOpacityComponent(a.Opacity.Value, m.Opacity.Map);
                changed.Add("opacity");
            }
            if (a.Description is not null) { m.Description = a.Description; changed.Add("description"); }

            var back = RequireMaterial(db, tr, a.Name, OpenMode.ForRead);
            return Wrap(new
            {
                changed,
                before,
                material = Describe(back),
                note = "Every object already using this material changes appearance with it - that " +
                       "is what a material is for, and it is worth knowing before editing a shared " +
                       "one. The previous values are reported so a change can be undone. Only the " +
                       "channels named are touched; the rest are rebuilt from what was already " +
                       "there, because each channel is a struct that has to be written back whole.",
            });
        });

    private static Task<ToolDispatchResult> ListMaterials(JsonObject args, CancellationToken ct) =>
        Run("acad.materials.list_materials", args, ct, (doc, db, tr) =>
        {
            var dict = Materials(db, tr, OpenMode.ForRead);
            var found = new List<object>();
            foreach (DBDictionaryEntry e in dict)
            {
                if (tr.GetObject(e.Value, OpenMode.ForRead) is Material m) found.Add(Describe(m));
            }
            return Wrap(new
            {
                count = found.Count,
                materials = found,
                note = "Every drawing has a material called Global, which is what an entity uses " +
                       "when nothing else has been assigned - so a count of one is an empty " +
                       "drawing rather than a broken tool. gloss comes from the specular " +
                       "component, there being no Material.Shininess.",
            });
        });

    // ─────────── attaching ───────────

    private static Task<ToolDispatchResult> AssignMaterial(JsonObject args, CancellationToken ct) =>
        Run("acad.materials.assign_material", args, ct, (doc, db, tr) =>
        {
            var a = Read<MaterialArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0)
                throw new ArgumentException("handles is required: which entities to assign it to.");
            var m = RequireMaterial(db, tr, a.Name, OpenMode.ForRead);
            var id = m.ObjectId;

            var done = new List<object>();
            foreach (var h in a.Handles)
            {
                var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForWrite);
                var before = ent.Material;
                ent.MaterialId = id;

                // Read back through Entity.Material - the NAME - which is a different property
                // from MaterialId that was written. A tool that set the id without it taking
                // would otherwise look identical to one that worked.
                if (!string.Equals(ent.Material, m.Name, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        "Entity " + h + " reads back with material '" + ent.Material +
                        "' rather than '" + m.Name + "'.");
                done.Add(new { handle = h, materialBefore = before, material = ent.Material });
            }

            return Wrap(new
            {
                name = m.Name,
                count = done.Count,
                entities = done,
                note = "Confirmed by reading Entity.Material - the NAME - back afterwards, which is " +
                       "a different property from the MaterialId that was written. The previous " +
                       "material of each entity is reported, so an assignment can be undone; " +
                       "unassign_material puts them back to ByLayer.",
            });
        });

    private static Task<ToolDispatchResult> UnassignMaterial(JsonObject args, CancellationToken ct) =>
        Run("acad.materials.unassign_material", args, ct, (doc, db, tr) =>
        {
            var a = Read<MaterialArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0)
                throw new ArgumentException("handles is required: which entities to clear.");

            // "No material" is not a null id - it is the ByLayer material, which every drawing
            // has. Setting MaterialId to ObjectId.Null would leave the entity in a state AutoCAD
            // does not expect.
            var dict = Materials(db, tr, OpenMode.ForRead);
            if (!dict.Contains("ByLayer"))
                throw new InvalidOperationException(
                    "This drawing has no ByLayer material, which every drawing should have.");
            var byLayer = dict.GetAt("ByLayer");

            var done = new List<object>();
            foreach (var h in a.Handles)
            {
                var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForWrite);
                var before = ent.Material;
                ent.MaterialId = byLayer;
                done.Add(new { handle = h, materialBefore = before, material = ent.Material });
            }

            return Wrap(new
            {
                count = done.Count,
                entities = done,
                note = "Set back to ByLayer, which is what 'no material' means in AutoCAD - there " +
                       "is no null state, and clearing the id outright would leave the entity in " +
                       "a condition AutoCAD does not expect. The material itself is untouched and " +
                       "still in the drawing; delete_material is what removes it.",
            });
        });

    private static Task<ToolDispatchResult> DeleteMaterial(JsonObject args, CancellationToken ct) =>
        Run("acad.materials.delete_material", args, ct, (doc, db, tr) =>
        {
            var a = Read<MaterialArgsDto>(args);
            var m = RequireMaterial(db, tr, a.Name, OpenMode.ForWrite);
            var name = m.Name;

            if (string.Equals(name, "Global", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "ByLayer", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "ByBlock", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    "'" + name + "' is one of AutoCAD's own materials and cannot be deleted - it " +
                    "is what entities fall back to when nothing else is assigned.");

            // Which entities would be left pointing at a deleted material? Say so rather than
            // leaving the caller to find out by looking at the drawing.
            var users = new List<string>();
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId eid in ms)
            {
                if (eid.IsErased) continue;                    // rule 26 §8
                if (tr.GetObject(eid, OpenMode.ForRead) is not Entity e) continue;
                if (e.MaterialId == m.ObjectId) users.Add(e.Handle.ToString());
            }
            if (users.Count > 0 && a.Force != true)
                throw new ArgumentException(
                    users.Count + " entities are using '" + name + "'. Deleting it would leave " +
                    "them pointing at a material that is gone. Unassign them first with " +
                    "unassign_material, or pass force true if that is really what you mean. The " +
                    "handles are: " + string.Join(", ", users.Take(10)) +
                    (users.Count > 10 ? " and more" : ""));

            var dict = Materials(db, tr, OpenMode.ForWrite);
            dict.Remove(name);
            m.Erase();

            var dict2 = Materials(db, tr, OpenMode.ForRead);
            if (dict2.Contains(name))
                throw new InvalidOperationException("The material still reads back from the dictionary.");

            return Wrap(new
            {
                name,
                deleted = true,
                wasUsedBy = users.Count,
                remaining = dict2.Count,
                note = "Removed from the dictionary AND erased, because removing the name alone " +
                       "would leave the object in the drawing with nothing referring to it. " +
                       "AutoCAD's own Global, ByLayer and ByBlock materials are refused outright - " +
                       "they are what an entity falls back to. Entities that were using a deleted " +
                       "material are counted above.",
            });
        });
}
