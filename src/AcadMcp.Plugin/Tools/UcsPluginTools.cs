// AutoCAD plugin handlers for the acad-ucs category.
//
// Two things about UCS in the .NET API that are easy to get wrong:
//
//   * The CURRENT UCS is editor state (ed.CurrentUserCoordinateSystem, a Matrix3d), while
//     NAMED UCSs are database records (UcsTable / UcsTableRecord with Origin + XAxis + YAxis).
//     They are set and read through completely different objects, and a change to one does
//     not touch the other unless you make it.
//
//   * The matrix returned by CurrentUserCoordinateSystem maps UCS -> WCS. To convert a point
//     the caller expressed in UCS into WCS you TransformBy it; to go the other way you invert
//     first. Getting that backwards produces coordinates that look plausible and are mirrored
//     about the origin, which is exactly the kind of silently-wrong output rule 43 exists to
//     prevent.

using System;
using System.Collections.Generic;
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

internal static class UcsPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.ucs.create_ucs_3point", Create3Point);
        host.Register("acad.ucs.create_ucs_origin", CreateOrigin);
        host.Register("acad.ucs.create_ucs_zaxis", CreateZAxis);
        host.Register("acad.ucs.rotate_ucs", RotateUcs);
        host.Register("acad.ucs.create_ucs_from_entity", CreateFromEntity);
        host.Register("acad.ucs.set_ucs_world", SetWorld);
        host.Register("acad.ucs.save_ucs", SaveUcs);
        host.Register("acad.ucs.restore_ucs", RestoreUcs);
        host.Register("acad.ucs.delete_ucs", DeleteUcs);
        host.Register("acad.ucs.rename_ucs", RenameUcs);
        host.Register("acad.ucs.get_current_ucs", GetCurrent);
        host.Register("acad.ucs.list_ucs", ListUcs);
        host.Register("acad.ucs.transform_point", TransformPoint);
    }

    private static T Read<T>(JsonObject a) => JsonSerializer.Deserialize<T>(a, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");
    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();
    private static Task<ToolDispatchResult> Run(string key, JsonObject a, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(key, ct, work);

    // ─────────── helpers ───────────

    private static object Pt(Point3d p) => new { x = p.X, y = p.Y, z = p.Z };
    private static object Vec(Vector3d v) => new { x = v.X, y = v.Y, z = v.Z };

    /// <summary>Current UCS as a matrix mapping UCS -> WCS.</summary>
    private static Matrix3d CurrentMatrix(Document doc) => doc.Editor.CurrentUserCoordinateSystem;

    private static void SetCurrent(Document doc, Point3d origin, Vector3d x, Vector3d y)
    {
        var z = x.CrossProduct(y);
        if (z.Length < 1e-12)
            throw new ArgumentException("The X and Y directions are parallel, so they define no plane.");
        doc.Editor.CurrentUserCoordinateSystem =
            Matrix3d.AlignCoordinateSystem(Point3d.Origin, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis,
                                           origin, x.GetNormal(), y.GetNormal(), z.GetNormal());
    }

    private static object InfoFromMatrix(Matrix3d m, string name, bool isCurrent)
    {
        var cs = m.CoordinateSystem3d;
        bool world = cs.Origin.DistanceTo(Point3d.Origin) < 1e-9
                     && cs.Xaxis.IsParallelTo(Vector3d.XAxis) && cs.Xaxis.DotProduct(Vector3d.XAxis) > 0
                     && cs.Yaxis.IsParallelTo(Vector3d.YAxis) && cs.Yaxis.DotProduct(Vector3d.YAxis) > 0;
        return new
        {
            name,
            origin = Pt(cs.Origin),
            xAxis = Vec(cs.Xaxis),
            yAxis = Vec(cs.Yaxis),
            zAxis = Vec(cs.Zaxis),
            isCurrent,
            isWorld = world,
        };
    }

    private static object CurrentInfo(Document doc) =>
        InfoFromMatrix(CurrentMatrix(doc), "*CURRENT", isCurrent: true);

    private static JsonObject Finish(Document doc, Database db, Transaction tr, string? name, bool makeCurrent)
    {
        if (!string.IsNullOrWhiteSpace(name)) SaveNamed(db, tr, doc, name!, overwrite: true);
        if (!makeCurrent) { /* the caller only wanted it saved */ }
        return Wrap(new { ucs = CurrentInfo(doc) });
    }

    private static void SaveNamed(Database db, Transaction tr, Document doc, string name, bool overwrite)
    {
        AcadEnv.ValidateSymbolName(name, "Ucs");
        var cs = CurrentMatrix(doc).CoordinateSystem3d;
        var table = (UcsTable)tr.GetObject(db.UcsTableId, OpenMode.ForWrite);

        if (table.Has(name))
        {
            if (!overwrite)
                throw new ArgumentException($"A UCS named '{name}' already exists. Pass overwrite=true to replace it.");
            var existing = (UcsTableRecord)tr.GetObject(table[name], OpenMode.ForWrite);
            existing.Origin = cs.Origin; existing.XAxis = cs.Xaxis; existing.YAxis = cs.Yaxis;
            return;
        }
        var rec = new UcsTableRecord { Name = name, Origin = cs.Origin, XAxis = cs.Xaxis, YAxis = cs.Yaxis };
        table.Add(rec);
        tr.AddNewlyCreatedDBObject(rec, true);
    }

    private static Matrix3d MatrixOfNamed(Database db, Transaction tr, string name)
    {
        var table = (UcsTable)tr.GetObject(db.UcsTableId, OpenMode.ForRead);
        if (!table.Has(name))
        {
            var known = new List<string>();
            foreach (ObjectId id in table)
                known.Add(((UcsTableRecord)tr.GetObject(id, OpenMode.ForRead)).Name);
            known.Sort(StringComparer.OrdinalIgnoreCase);
            throw new ArgumentException(
                $"No UCS named '{name}'. Saved: " + (known.Count == 0 ? "(none)" : string.Join(", ", known)) + ".");
        }
        var rec = (UcsTableRecord)tr.GetObject(table[name], OpenMode.ForRead);
        var z = rec.XAxis.CrossProduct(rec.YAxis).GetNormal();
        return Matrix3d.AlignCoordinateSystem(Point3d.Origin, Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis,
                                              rec.Origin, rec.XAxis.GetNormal(), rec.YAxis.GetNormal(), z);
    }

    /// <summary>Resolve "world" / "current" / a saved name to a UCS -> WCS matrix.</summary>
    private static Matrix3d ResolveSystem(Document doc, Database db, Transaction tr, string spec)
    {
        if (string.IsNullOrWhiteSpace(spec) || spec.Equals("world", StringComparison.OrdinalIgnoreCase))
            return Matrix3d.Identity;
        if (spec.Equals("current", StringComparison.OrdinalIgnoreCase))
            return CurrentMatrix(doc);
        return MatrixOfNamed(db, tr, spec);
    }

    // ─────────── creation ───────────

    private static Task<ToolDispatchResult> Create3Point(JsonObject args, CancellationToken ct) =>
        Run("acad.ucs.create_ucs_3point", args, ct, (doc, db, tr) =>
        {
            var a = Read<Ucs3PointArgsDto>(args);
            var o = AcadEnv.ToPoint3d(a.Origin);
            SetCurrent(doc, o, AcadEnv.ToPoint3d(a.XAxisPoint) - o, AcadEnv.ToPoint3d(a.YAxisPoint) - o);
            return Finish(doc, db, tr, a.Name, a.MakeCurrent);
        });

    private static Task<ToolDispatchResult> CreateOrigin(JsonObject args, CancellationToken ct) =>
        Run("acad.ucs.create_ucs_origin", args, ct, (doc, db, tr) =>
        {
            var a = Read<UcsOriginArgsDto>(args);
            var cs = CurrentMatrix(doc).CoordinateSystem3d;
            SetCurrent(doc, AcadEnv.ToPoint3d(a.Origin), cs.Xaxis, cs.Yaxis);
            return Finish(doc, db, tr, a.Name, a.MakeCurrent);
        });

    private static Task<ToolDispatchResult> CreateZAxis(JsonObject args, CancellationToken ct) =>
        Run("acad.ucs.create_ucs_zaxis", args, ct, (doc, db, tr) =>
        {
            var a = Read<UcsZAxisArgsDto>(args);
            var o = AcadEnv.ToPoint3d(a.Origin);
            var z = (AcadEnv.ToPoint3d(a.ZAxis) - Point3d.Origin);
            if (z.Length < 1e-12) throw new ArgumentException("zAxis cannot be a zero-length vector.");
            z = z.GetNormal();
            // Any X perpendicular to Z will do; pick the one least degenerate for this Z.
            var seed = Math.Abs(z.DotProduct(Vector3d.ZAxis)) > 0.9 ? Vector3d.XAxis : Vector3d.ZAxis;
            var x = seed.CrossProduct(z).GetNormal();
            SetCurrent(doc, o, x, z.CrossProduct(x).GetNormal());
            return Finish(doc, db, tr, a.Name, a.MakeCurrent);
        });

    private static Task<ToolDispatchResult> RotateUcs(JsonObject args, CancellationToken ct) =>
        Run("acad.ucs.rotate_ucs", args, ct, (doc, db, tr) =>
        {
            var a = Read<UcsRotateArgsDto>(args);
            var cs = CurrentMatrix(doc).CoordinateSystem3d;
            var axis = (a.Axis ?? "z").Trim().ToLowerInvariant() switch
            {
                "x" => cs.Xaxis,
                "y" => cs.Yaxis,
                "z" => cs.Zaxis,
                _ => throw new ArgumentException($"axis must be 'x', 'y' or 'z' (got '{a.Axis}')."),
            };
            var rot = Matrix3d.Rotation(a.AngleDeg * Math.PI / 180.0, axis, cs.Origin);
            SetCurrent(doc, cs.Origin, cs.Xaxis.TransformBy(rot), cs.Yaxis.TransformBy(rot));
            return Finish(doc, db, tr, a.Name, a.MakeCurrent);
        });

    private static Task<ToolDispatchResult> CreateFromEntity(JsonObject args, CancellationToken ct) =>
        Run("acad.ucs.create_ucs_from_entity", args, ct, (doc, db, tr) =>
        {
            var a = Read<UcsFromEntityArgsDto>(args);
            var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle), OpenMode.ForRead);

            // Every planar entity carries its own plane; that plane IS the UCS we want.
            Plane plane;
            try { plane = ent.GetPlane(); }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Entity {a.Handle} ({ent.GetRXClass().Name}) does not define a plane, so no UCS can be " +
                    "derived from it. Use create_ucs_3point instead.", ex);
            }
            var cs = plane.GetCoordinateSystem();
            SetCurrent(doc, cs.Origin, cs.Xaxis, cs.Yaxis);
            return Finish(doc, db, tr, a.Name, a.MakeCurrent);
        });

    // ─────────── world / named ───────────

    private static Task<ToolDispatchResult> SetWorld(JsonObject args, CancellationToken ct) =>
        Run("acad.ucs.set_ucs_world", args, ct, (doc, db, tr) =>
        {
            doc.Editor.CurrentUserCoordinateSystem = Matrix3d.Identity;
            return Wrap(new { ucs = CurrentInfo(doc) });
        });

    private static Task<ToolDispatchResult> SaveUcs(JsonObject args, CancellationToken ct) =>
        Run("acad.ucs.save_ucs", args, ct, (doc, db, tr) =>
        {
            var a = Read<UcsSaveArgsDto>(args);
            SaveNamed(db, tr, doc, a.Name, a.Overwrite);
            return Wrap(new { ucs = InfoFromMatrix(CurrentMatrix(doc), a.Name, isCurrent: true) });
        });

    private static Task<ToolDispatchResult> RestoreUcs(JsonObject args, CancellationToken ct) =>
        Run("acad.ucs.restore_ucs", args, ct, (doc, db, tr) =>
        {
            var a = Read<UcsNameArgsDto>(args);
            var m = MatrixOfNamed(db, tr, a.Name);
            doc.Editor.CurrentUserCoordinateSystem = m;
            return Wrap(new { ucs = InfoFromMatrix(m, a.Name, isCurrent: true) });
        });

    private static Task<ToolDispatchResult> DeleteUcs(JsonObject args, CancellationToken ct) =>
        Run("acad.ucs.delete_ucs", args, ct, (doc, db, tr) =>
        {
            var a = Read<UcsNameArgsDto>(args);
            var table = (UcsTable)tr.GetObject(db.UcsTableId, OpenMode.ForRead);
            if (!table.Has(a.Name)) throw new ArgumentException($"No UCS named '{a.Name}'.");
            var rec = (UcsTableRecord)tr.GetObject(table[a.Name], OpenMode.ForWrite);
            rec.Erase();
            return Wrap(new { affected = 1, name = a.Name });
        });

    private static Task<ToolDispatchResult> RenameUcs(JsonObject args, CancellationToken ct) =>
        Run("acad.ucs.rename_ucs", args, ct, (doc, db, tr) =>
        {
            var a = Read<UcsRenameArgsDto>(args);
            AcadEnv.ValidateSymbolName(a.NewName, "Ucs");
            var table = (UcsTable)tr.GetObject(db.UcsTableId, OpenMode.ForRead);
            if (!table.Has(a.OldName)) throw new ArgumentException($"No UCS named '{a.OldName}'.");
            if (table.Has(a.NewName)) throw new ArgumentException($"A UCS named '{a.NewName}' already exists.");
            var rec = (UcsTableRecord)tr.GetObject(table[a.OldName], OpenMode.ForWrite);
            rec.Name = a.NewName;
            return Wrap(new { affected = 1, name = a.NewName });
        });

    // ─────────── inspection ───────────

    private static Task<ToolDispatchResult> GetCurrent(JsonObject args, CancellationToken ct) =>
        Run("acad.ucs.get_current_ucs", args, ct, (doc, db, tr) => Wrap(new { ucs = CurrentInfo(doc) }));

    private static Task<ToolDispatchResult> ListUcs(JsonObject args, CancellationToken ct) =>
        Run("acad.ucs.list_ucs", args, ct, (doc, db, tr) =>
        {
            var table = (UcsTable)tr.GetObject(db.UcsTableId, OpenMode.ForRead);
            var named = new List<object>();
            foreach (ObjectId id in table)
            {
                var rec = (UcsTableRecord)tr.GetObject(id, OpenMode.ForRead);
                var z = rec.XAxis.CrossProduct(rec.YAxis);
                named.Add(new
                {
                    name = rec.Name,
                    origin = Pt(rec.Origin),
                    xAxis = Vec(rec.XAxis),
                    yAxis = Vec(rec.YAxis),
                    zAxis = Vec(z.Length < 1e-12 ? Vector3d.ZAxis : z.GetNormal()),
                    isCurrent = false,
                    isWorld = false,
                });
            }
            return Wrap(new { named, current = CurrentInfo(doc), count = named.Count });
        });

    private static Task<ToolDispatchResult> TransformPoint(JsonObject args, CancellationToken ct) =>
        Run("acad.ucs.transform_point", args, ct, (doc, db, tr) =>
        {
            var a = Read<UcsTransformArgsDto>(args);
            var from = ResolveSystem(doc, db, tr, a.From);
            var to = ResolveSystem(doc, db, tr, a.To);

            // Both matrices map their own system -> WCS. So: source -> WCS, then WCS -> target
            // by inverting the target's. Composing them the other way round yields a point that
            // looks plausible and is wrong, which is the failure rule 43 is written against.
            var p = AcadEnv.ToPoint3d(a.Point);
            var world = p.TransformBy(from);
            var outP = world.TransformBy(to.Inverse());

            return Wrap(new
            {
                input = Pt(p),
                output = Pt(outP),
                from = a.From,
                to = a.To,
            });
        });
}
