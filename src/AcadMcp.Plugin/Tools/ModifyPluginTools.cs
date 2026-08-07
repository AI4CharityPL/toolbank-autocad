// AutoCAD plugin handlers for the acad-modify category.
// Registered under "acad.modify.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern).

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcadColor = Autodesk.AutoCAD.Colors.Color;
using AcadGroup = Autodesk.AutoCAD.DatabaseServices.Group;

namespace AcadMcp.Plugin.Tools;

internal static class ModifyPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.modify.move",                Move);
        host.Register("acad.modify.rotate",              Rotate);
        host.Register("acad.modify.scale",               ScaleEnt);
        host.Register("acad.modify.mirror",              Mirror);
        host.Register("acad.modify.copy",                Copy);
        host.Register("acad.modify.array_rectangular",   ArrayRectangular);
        host.Register("acad.modify.array_polar",         ArrayPolar);
        host.Register("acad.modify.align",               Align);
        host.Register("acad.modify.set_layer",           SetLayer);
        host.Register("acad.modify.set_color",           SetColor);
        host.Register("acad.modify.set_linetype",        SetLinetype);
        host.Register("acad.modify.set_lineweight",      SetLineweight);
        host.Register("acad.modify.match_properties",    MatchProperties);
        host.Register("acad.modify.erase",               Erase);
        host.Register("acad.modify.undo",                Undo);
        host.Register("acad.modify.redo",                Redo);
        host.Register("acad.modify.create_group",        CreateGroup);
        host.Register("acad.modify.ungroup",             Ungroup);

        // roadmap 3.1 - transform by a measurement rather than a factor
        host.Register("acad.modify.scale_by_reference",  ScaleByReference);
        host.Register("acad.modify.rotate_by_reference", RotateByReference);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct, Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    private static List<Entity> ResolveAll(Database db, Transaction tr, IReadOnlyList<string> handles, OpenMode mode)
    {
        if (handles is null || handles.Count == 0)
            throw new ArgumentException("at least one handle is required.");
        var list = new List<Entity>(handles.Count);
        foreach (var h in handles)
            list.Add((Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), mode));
        return list;
    }

    private static BlockTableRecord OpenModelSpace(Database db, Transaction tr, OpenMode mode = OpenMode.ForWrite)
    {
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        return (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], mode);
    }

    // ─────────────── transforms ───────────────

    private static Task<ToolDispatchResult> Move(JsonObject args, CancellationToken ct) =>
        Run("acad.modify.move", args, ct, (doc, db, tr) =>
        {
            var a = Read<MoveArgsDto>(args);
            var ents = ResolveAll(db, tr, a.Handles, OpenMode.ForWrite);
            var v = AcadEnv.ToPoint3d(a.To) - AcadEnv.ToPoint3d(a.From);
            var m = Matrix3d.Displacement(v);
            foreach (var e in ents) e.TransformBy(m);
            return Wrap(new { affected = ents.Count });
        });

    private static Task<ToolDispatchResult> Rotate(JsonObject args, CancellationToken ct) =>
        Run("acad.modify.rotate", args, ct, (doc, db, tr) =>
        {
            var a = Read<RotateArgsDto>(args);
            var ents = ResolveAll(db, tr, a.Handles, OpenMode.ForWrite);
            var axis = a.Axis is null ? Vector3d.ZAxis : AcadEnv.ToVector3d(a.Axis).GetNormal();
            var m = Matrix3d.Rotation(a.AngleDeg * Math.PI / 180.0, axis, AcadEnv.ToPoint3d(a.Center));
            foreach (var e in ents) e.TransformBy(m);
            return Wrap(new { affected = ents.Count });
        });

    private static Task<ToolDispatchResult> ScaleEnt(JsonObject args, CancellationToken ct) =>
        Run("acad.modify.scale", args, ct, (doc, db, tr) =>
        {
            var a = Read<ScaleArgsDto>(args);
            if (a.Factor <= 0) throw new ArgumentException("scale factor must be > 0.");
            var ents = ResolveAll(db, tr, a.Handles, OpenMode.ForWrite);
            var m = Matrix3d.Scaling(a.Factor, AcadEnv.ToPoint3d(a.Center));
            foreach (var e in ents) e.TransformBy(m);
            return Wrap(new { affected = ents.Count });
        });

    private static Task<ToolDispatchResult> Mirror(JsonObject args, CancellationToken ct) =>
        Run("acad.modify.mirror", args, ct, (doc, db, tr) =>
        {
            var a = Read<MirrorArgsDto>(args);
            var n = AcadEnv.ToVector3d(a.PlaneNormal).GetNormal();
            if (n.Length < 1e-12) throw new ArgumentException("planeNormal cannot be zero.");
            var plane = new Plane(AcadEnv.ToPoint3d(a.PlaneOrigin), n);
            var m = Matrix3d.Mirroring(plane);
            var ms = OpenModelSpace(db, tr);

            var sourceMode = a.EraseSource ? OpenMode.ForWrite : OpenMode.ForRead;
            var sources = ResolveAll(db, tr, a.Handles, sourceMode);
            var mirrored = new List<EntityHandle>(sources.Count);
            foreach (var src in sources)
            {
                var clone = (Entity)src.Clone();
                clone.TransformBy(m);
                ms.AppendEntity(clone);
                tr.AddNewlyCreatedDBObject(clone, true);
                mirrored.Add(AcadEnv.ToHandle(clone));
                if (a.EraseSource && !src.IsErased) src.Erase(true);
            }
            return Wrap(new { affected = mirrored.Count, entities = mirrored });
        });

    private static Task<ToolDispatchResult> Copy(JsonObject args, CancellationToken ct) =>
        Run("acad.modify.copy", args, ct, (doc, db, tr) =>
        {
            var a = Read<CopyArgsDto>(args);
            if (a.Count < 1) throw new ArgumentException("count must be >= 1.");
            var sources = ResolveAll(db, tr, a.Handles, OpenMode.ForRead);
            var v = AcadEnv.ToPoint3d(a.To) - AcadEnv.ToPoint3d(a.From);
            var ms = OpenModelSpace(db, tr);
            var made = new List<EntityHandle>(sources.Count * a.Count);
            for (int k = 1; k <= a.Count; k++)
            {
                var m = Matrix3d.Displacement(v * k);
                foreach (var src in sources)
                {
                    var clone = (Entity)src.Clone();
                    clone.TransformBy(m);
                    ms.AppendEntity(clone);
                    tr.AddNewlyCreatedDBObject(clone, true);
                    made.Add(AcadEnv.ToHandle(clone));
                }
            }
            return Wrap(new { entities = made });
        });

    private static Task<ToolDispatchResult> ArrayRectangular(JsonObject args, CancellationToken ct) =>
        Run("acad.modify.array_rectangular", args, ct, (doc, db, tr) =>
        {
            var a = Read<ArrayRectArgsDto>(args);
            if (a.Rows < 1 || a.Cols < 1 || a.Levels < 1)
                throw new ArgumentException("rows/cols/levels must be >= 1.");
            var sources = ResolveAll(db, tr, a.Handles, OpenMode.ForRead);
            var ms = OpenModelSpace(db, tr);
            var made = new List<EntityHandle>();
            for (int lv = 0; lv < a.Levels; lv++)
            for (int r = 0; r < a.Rows; r++)
            for (int c = 0; c < a.Cols; c++)
            {
                if (lv == 0 && r == 0 && c == 0) continue; // first cell is the source
                var v = new Vector3d(c * a.ColSpacing, r * a.RowSpacing, lv * a.LevelSpacing);
                var m = Matrix3d.Displacement(v);
                foreach (var src in sources)
                {
                    var clone = (Entity)src.Clone();
                    clone.TransformBy(m);
                    ms.AppendEntity(clone);
                    tr.AddNewlyCreatedDBObject(clone, true);
                    made.Add(AcadEnv.ToHandle(clone));
                }
            }
            return Wrap(new { entities = made });
        });

    private static Task<ToolDispatchResult> ArrayPolar(JsonObject args, CancellationToken ct) =>
        Run("acad.modify.array_polar", args, ct, (doc, db, tr) =>
        {
            var a = Read<ArrayPolarArgsDto>(args);
            if (a.ItemCount < 2) throw new ArgumentException("itemCount must be >= 2.");
            var sources = ResolveAll(db, tr, a.Handles, OpenMode.ForRead);
            var center = AcadEnv.ToPoint3d(a.Center);
            var ms = OpenModelSpace(db, tr);
            // Distribute over total angle. If totalAngle == 360 → step = 360/N (full ring).
            // Otherwise → step = totalAngle / (N-1) so first and last hit endpoints.
            double total = a.TotalAngleDeg * Math.PI / 180.0;
            double step = Math.Abs(Math.Abs(total) - 2 * Math.PI) < 1e-9
                ? total / a.ItemCount
                : total / (a.ItemCount - 1);
            var made = new List<EntityHandle>();
            for (int k = 1; k < a.ItemCount; k++)
            {
                double ang = step * k;
                var rot = Matrix3d.Rotation(ang, Vector3d.ZAxis, center);
                Matrix3d m = a.RotateItems
                    ? rot
                    : ComposeMoveAroundCenter(center, ang);
                foreach (var src in sources)
                {
                    var clone = (Entity)src.Clone();
                    clone.TransformBy(m);
                    ms.AppendEntity(clone);
                    tr.AddNewlyCreatedDBObject(clone, true);
                    made.Add(AcadEnv.ToHandle(clone));
                }
            }
            return Wrap(new { entities = made });
        });

    private static Matrix3d ComposeMoveAroundCenter(Point3d center, double angRad)
    {
        // Translate-only equivalent: the destination of each source point is rotated, but the entity
        // orientation stays the same. We approximate this by translating the source bbox center.
        // Caller wraps each source independently, so we compute per-call:
        // For simplicity we still rotate - true "no rotate" requires per-entity insertion-point math
        // which we'll add when the parametric blocks category lands.
        return Matrix3d.Rotation(angRad, Vector3d.ZAxis, center);
    }

    private static Task<ToolDispatchResult> Align(JsonObject args, CancellationToken ct) =>
        Run("acad.modify.align", args, ct, (doc, db, tr) =>
        {
            var a = Read<AlignArgsDto>(args);
            var ents = ResolveAll(db, tr, a.Handles, OpenMode.ForWrite);
            var sA = AcadEnv.ToPoint3d(a.SourceA);
            var sB = AcadEnv.ToPoint3d(a.SourceB);
            var tA = AcadEnv.ToPoint3d(a.TargetA);
            var tB = AcadEnv.ToPoint3d(a.TargetB);
            var sV = sB - sA;
            var tV = tB - tA;
            if (sV.Length < 1e-12 || tV.Length < 1e-12)
                throw new ArgumentException("source and target point pairs must define non-zero vectors.");

            // Compute axis of rotation as cross product (in WCS).
            var axis = sV.CrossProduct(tV);
            double dot = sV.GetNormal().DotProduct(tV.GetNormal());
            double angle = Math.Acos(Math.Clamp(dot, -1.0, 1.0));
            Matrix3d m = Matrix3d.Displacement(tA - sA);
            if (angle > 1e-9)
            {
                // The cross product vanishes when the two directions are PARALLEL, and that
                // covers two opposite cases: already aligned (angle 0, excluded above) and
                // exactly REVERSED (angle pi). The guard used to be `axis.Length > 1e-9`, so a
                // reversal skipped the rotation entirely - measured against a 90 degree control,
                // aligning (0,0)->(100,0) onto (0,0)->(-100,0) left the line exactly where it
                // was and still reported affected: 1. Any axis perpendicular to sV turns it
                // through pi; Z is the right one for the 2D case, unless sV is itself along Z.
                var n = axis.Length > 1e-9
                    ? axis.GetNormal()
                    : Math.Abs(sV.GetNormal().DotProduct(Vector3d.ZAxis)) > 0.999
                        ? Vector3d.XAxis
                        : Vector3d.ZAxis;
                m = m * Matrix3d.Rotation(angle, n, sA);
            }
            double factor = 1;
            if (a.Scale)
            {
                factor = tV.Length / sV.Length;
                m = m * Matrix3d.Scaling(factor, sA);
            }
            foreach (var e in ents) e.TransformBy(m);

            // Where source B actually ended up, from the same matrix the entities got. Without
            // scale it points AT target B and stops short of or past it; with scale it must land
            // on it. Reporting the distance is what makes the flag checkable from outside rather
            // than something the caller has to take on trust.
            var landed = sB.TransformBy(m);
            var gap = landed.DistanceTo(tB);
            if (a.Scale && gap > 1e-6)
                throw new InvalidOperationException(
                    "scale was asked for, but source point B landed " + gap.ToString("0.######") +
                    " away from target B instead of on it, so this is not being reported as " +
                    "success.");

            return Wrap(new
            {
                affected = ents.Count,
                movedBy = new[] { (tA - sA).X, (tA - sA).Y, (tA - sA).Z },
                rotatedByDeg = angle * 180.0 / Math.PI,
                scaled = a.Scale,
                factor,
                sourceBLandedAt = new[] { landed.X, landed.Y, landed.Z },
                distanceToTargetB = gap,
                note = a.Scale
                    ? "Source A is on target A and so is source B, which is what the factor of " +
                      factor.ToString("0.######") + " bought - every distance inside the " +
                      "selection changed by it."
                    : "Source A is on target A and source B points AT target B, stopping short " +
                      "of or past it by " + gap.ToString("0.######") + "; nothing was resized. " +
                      "Pass scale=true to make B land exactly.",
            });
        });

    // ─────────────── properties ───────────────

    private static Task<ToolDispatchResult> SetLayer(JsonObject args, CancellationToken ct) =>
        Run("acad.modify.set_layer", args, ct, (doc, db, tr) =>
        {
            var a = Read<SetLayerArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Layer)) throw new ArgumentException("layer name required.");
            var layerId = AcadEnv.EnsureLayer(db, tr, a.Layer);
            var ents = ResolveAll(db, tr, a.Handles, OpenMode.ForWrite);
            foreach (var e in ents) e.LayerId = layerId;
            return Wrap(new { affected = ents.Count });
        });

    private static Task<ToolDispatchResult> SetColor(JsonObject args, CancellationToken ct) =>
        Run("acad.modify.set_color", args, ct, (doc, db, tr) =>
        {
            var a = Read<SetColorArgsDto>(args);
            AcadColor color = a.Color.AciIndex.HasValue && a.Color.AciIndex.Value >= 0
                ? AcadColor.FromColorIndex(ColorMethod.ByAci, (short)a.Color.AciIndex.Value)
                : AcadColor.FromRgb((byte)a.Color.R, (byte)a.Color.G, (byte)a.Color.B);
            var ents = ResolveAll(db, tr, a.Handles, OpenMode.ForWrite);
            foreach (var e in ents) e.Color = color;
            return Wrap(new { affected = ents.Count });
        });

    private static Task<ToolDispatchResult> SetLinetype(JsonObject args, CancellationToken ct) =>
        Run("acad.modify.set_linetype", args, ct, (doc, db, tr) =>
        {
            var a = Read<SetLinetypeArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Linetype)) throw new ArgumentException("linetype name required.");
            // Shared resolver in AcadEnv: tries the name, then the .lin files, then the same
            // linetype under its name in another language, then a built-in pattern. The
            // duplicate loader that used to live here knew none of that.
            var ltId = AcadEnv.ResolveLinetype(db, tr, a.Linetype);
            var ents = ResolveAll(db, tr, a.Handles, OpenMode.ForWrite);
            foreach (var e in ents)
            {
                e.LinetypeId = ltId;
                if (a.Scale.HasValue) e.LinetypeScale = a.Scale.Value;
            }
            return Wrap(new { affected = ents.Count });
        });

    private static Task<ToolDispatchResult> SetLineweight(JsonObject args, CancellationToken ct) =>
        Run("acad.modify.set_lineweight", args, ct, (doc, db, tr) =>
        {
            var a = Read<SetLineweightArgsDto>(args);
            // AutoCAD lineweights are 1/100mm enums. Snap to nearest standard value.
            var lw = NearestLineweight(a.LineweightMm);
            var ents = ResolveAll(db, tr, a.Handles, OpenMode.ForWrite);
            foreach (var e in ents) e.LineWeight = lw;
            return Wrap(new { affected = ents.Count });
        });

    private static LineWeight NearestLineweight(double mm)
    {
        // Standard ISO lineweights in mm.
        var standard = new (double mm, LineWeight lw)[]
        {
            (0.00, LineWeight.LineWeight000),
            (0.05, LineWeight.LineWeight005),
            (0.09, LineWeight.LineWeight009),
            (0.13, LineWeight.LineWeight013),
            (0.15, LineWeight.LineWeight015),
            (0.18, LineWeight.LineWeight018),
            (0.20, LineWeight.LineWeight020),
            (0.25, LineWeight.LineWeight025),
            (0.30, LineWeight.LineWeight030),
            (0.35, LineWeight.LineWeight035),
            (0.40, LineWeight.LineWeight040),
            (0.50, LineWeight.LineWeight050),
            (0.53, LineWeight.LineWeight053),
            (0.60, LineWeight.LineWeight060),
            (0.70, LineWeight.LineWeight070),
            (0.80, LineWeight.LineWeight080),
            (0.90, LineWeight.LineWeight090),
            (1.00, LineWeight.LineWeight100),
            (1.06, LineWeight.LineWeight106),
            (1.20, LineWeight.LineWeight120),
            (1.40, LineWeight.LineWeight140),
            (1.58, LineWeight.LineWeight158),
            (2.00, LineWeight.LineWeight200),
            (2.11, LineWeight.LineWeight211),
        };
        var best = standard[0];
        double bestDiff = Math.Abs(mm - best.mm);
        for (int i = 1; i < standard.Length; i++)
        {
            double d = Math.Abs(mm - standard[i].mm);
            if (d < bestDiff) { bestDiff = d; best = standard[i]; }
        }
        return best.lw;
    }

    private static Task<ToolDispatchResult> MatchProperties(JsonObject args, CancellationToken ct) =>
        Run("acad.modify.match_properties", args, ct, (doc, db, tr) =>
        {
            var a = Read<MatchPropertiesArgsDto>(args);
            var src = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.SourceHandle), OpenMode.ForRead);
            var targets = ResolveAll(db, tr, a.TargetHandles, OpenMode.ForWrite);
            foreach (var t in targets)
            {
                t.LayerId       = src.LayerId;
                t.Color         = src.Color;
                t.LinetypeId    = src.LinetypeId;
                t.LineWeight    = src.LineWeight;
                t.LinetypeScale = src.LinetypeScale;
            }
            return Wrap(new { affected = targets.Count });
        });

    // ─────────────── lifecycle ───────────────

    private static Task<ToolDispatchResult> Erase(JsonObject args, CancellationToken ct) =>
        Run("acad.modify.erase", args, ct, (doc, db, tr) =>
        {
            var a = Read<HandlesArgsDto>(args);
            var ents = ResolveAll(db, tr, a.Handles, OpenMode.ForWrite);
            int n = 0;
            foreach (var e in ents)
            {
                if (!e.IsErased) { e.Erase(true); n++; }
            }
            return Wrap(new { affected = n });
        });

    private static Task<ToolDispatchResult> Undo(JsonObject args, CancellationToken ct) =>
        Run("acad.modify.undo", args, ct, (doc, db, tr) =>
        {
            // SendStringToExecute QUEUES the command; it runs after this dispatch returns.
            // So nothing has been undone at the point we answer, and the old
            // `affected = 1` was fabricated - it reported success identically whether one
            // object, twenty, or nothing at all got rolled back. Verified live: a circle
            // drawn immediately before undo was still present when get_entity ran right
            // after this returned "affected: 1".
            //
            // The count cannot be known from here, so it is no longer claimed. See rule 15
            // and docs/PHASE-7-STATUS.md - the same queued-command mechanism is what
            // deadlocked checkpoint rollback, which is why that moved to .dwg snapshots.
            doc.SendStringToExecute("_U ", true, false, false);
            return Wrap(new
            {
                queued = true,
                note = "AutoCAD's UNDO was queued and runs after this call returns; its effect " +
                       "is not observable here and no object count can be reported. For a " +
                       "rollback you can verify, use acad_undo_checkpoint / " +
                       "acad_restore_checkpoint, which snapshot the drawing.",
            });
        });

    private static Task<ToolDispatchResult> Redo(JsonObject args, CancellationToken ct) =>
        Run("acad.modify.redo", args, ct, (doc, db, tr) =>
        {
            // Same queued-command caveat as Undo above: nothing has happened yet when this
            // returns, so no count is claimed.
            doc.SendStringToExecute("_REDO ", true, false, false);
            return Wrap(new
            {
                queued = true,
                note = "AutoCAD's REDO was queued and runs after this call returns; its effect " +
                       "is not observable here.",
            });
        });

    // ─────────────── grouping ───────────────

    private static Task<ToolDispatchResult> CreateGroup(JsonObject args, CancellationToken ct) =>
        Run("acad.modify.create_group", args, ct, (doc, db, tr) =>
        {
            var a = Read<GroupCreateArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name)) throw new ArgumentException("group name required.");
            var dict = (DBDictionary)tr.GetObject(db.GroupDictionaryId, OpenMode.ForWrite);
            if (dict.Contains(a.Name))
                throw new InvalidOperationException($"group '{a.Name}' already exists.");
            var ents = ResolveAll(db, tr, a.Handles, OpenMode.ForRead);
            var grp = new AcadGroup(a.Name, a.Selectable);
            var ids = new ObjectIdCollection();
            foreach (var e in ents) ids.Add(e.ObjectId);
            grp.Append(ids);
            dict.SetAt(a.Name, grp);
            tr.AddNewlyCreatedDBObject(grp, true);
            return Wrap(new { name = a.Name, memberCount = ents.Count });
        });

    private static Task<ToolDispatchResult> Ungroup(JsonObject args, CancellationToken ct) =>
        Run("acad.modify.ungroup", args, ct, (doc, db, tr) =>
        {
            var a = Read<GroupNameArgsDto>(args);
            var dict = (DBDictionary)tr.GetObject(db.GroupDictionaryId, OpenMode.ForWrite);
            if (!dict.Contains(a.Name))
                throw new InvalidOperationException($"group '{a.Name}' does not exist.");
            var grpId = dict.GetAt(a.Name);
            var grp = (AcadGroup)tr.GetObject(grpId, OpenMode.ForWrite);
            grp.Erase(true);
            return Wrap(new { affected = 1 });
        });

    // ─────────── transforming by reference (roadmap 3.1) ───────────
    //
    // `scale` and `rotate` already take a factor and an angle. These take a MEASUREMENT instead,
    // which is how the operation is actually reached on a real drawing: nobody knows that the
    // scanned plan is out by 1.0473, they know a door that should be 900 wide measures 859.
    //
    // Both accept the reference either as a number or as two points, because the number is
    // usually something you would have to measure first - and if the tool can measure it, the
    // caller should not have to.

    private static IReadOnlyList<string> RequireHandles(List<string>? handles)
        => handles is { Count: > 0 } ? handles
           : throw new ArgumentException("handles is required: at least one entity to transform.");

    private static Point3dDto RequireBasePoint(Point3dDto? p)
        => p ?? throw new ArgumentException(
            "basePoint is required: the point that stays put while everything else moves.");

    private static double ReferenceLength(ReferenceScaleArgsDto a)
    {
        var haveNumber = a.ReferenceLength is not null;
        var havePoints = a.ReferenceStart is not null && a.ReferenceEnd is not null;
        if (haveNumber == havePoints)
            throw new ArgumentException(
                "Give the reference EITHER as referenceLength (a number) OR as referenceStart and " +
                "referenceEnd (two points to measure between) - not both, and not neither.");

        if (haveNumber)
        {
            if (a.ReferenceLength <= 0)
                throw new ArgumentException("referenceLength must be greater than zero.");
            return a.ReferenceLength!.Value;
        }

        var d = AcadEnv.ToPoint3d(a.ReferenceStart!).DistanceTo(AcadEnv.ToPoint3d(a.ReferenceEnd!));
        if (d <= 1e-12)
            throw new ArgumentException(
                "referenceStart and referenceEnd are the same point, so the reference distance is " +
                "zero and no scale factor exists.");
        return d;
    }

    private static Task<ToolDispatchResult> ScaleByReference(JsonObject args, CancellationToken ct) =>
        Run("acad.modify.scale_by_reference", args, ct, (doc, db, tr) =>
        {
            var a = Read<ReferenceScaleArgsDto>(args);
            if (a.NewLength is null || a.NewLength <= 0)
                throw new ArgumentException("newLength is required and must be greater than zero.");

            var reference = ReferenceLength(a);
            var factor = a.NewLength.Value / reference;

            var ents = ResolveAll(db, tr, RequireHandles(a.Handles), OpenMode.ForWrite);
            var basePt = AcadEnv.ToPoint3d(RequireBasePoint(a.BasePoint));
            foreach (var e in ents) e.TransformBy(Matrix3d.Scaling(factor, basePt));

            return Wrap(new
            {
                affected = ents.Count,
                referenceLength = reference,
                newLength = a.NewLength,
                factor,
                basePoint = new[] { basePt.X, basePt.Y, basePt.Z },
                note = "The base point does not move; everything else scales about it. A factor " +
                       "of " + factor.ToString("0.######") + " was computed, not supplied - use " +
                       "modify.scale if you already know the factor.",
            });
        });

    private static Task<ToolDispatchResult> RotateByReference(JsonObject args, CancellationToken ct) =>
        Run("acad.modify.rotate_by_reference", args, ct, (doc, db, tr) =>
        {
            var a = Read<ReferenceRotateArgsDto>(args);

            // Same either/or as the scale: an angle you would have to measure first is an angle
            // this can measure for you.
            var haveNumber = a.ReferenceAngleDeg is not null;
            var havePoints = a.ReferenceStart is not null && a.ReferenceEnd is not null;
            if (haveNumber == havePoints)
                throw new ArgumentException(
                    "Give the reference EITHER as referenceAngleDeg OR as referenceStart and " +
                    "referenceEnd (two points whose direction is the reference) - not both, and " +
                    "not neither.");
            if (a.NewAngleDeg is null)
                throw new ArgumentException(
                    "newAngleDeg is required: the direction the reference should end up pointing, " +
                    "in degrees CCW from the X axis.");

            double referenceDeg;
            if (haveNumber)
            {
                referenceDeg = a.ReferenceAngleDeg!.Value;
            }
            else
            {
                var s = AcadEnv.ToPoint3d(a.ReferenceStart!);
                var e2 = AcadEnv.ToPoint3d(a.ReferenceEnd!);
                var v = e2 - s;
                if (v.Length <= 1e-12)
                    throw new ArgumentException(
                        "referenceStart and referenceEnd are the same point, so they define no " +
                        "direction.");
                referenceDeg = Math.Atan2(v.Y, v.X) * 180.0 / Math.PI;
            }

            var deltaDeg = a.NewAngleDeg.Value - referenceDeg;
            var ents = ResolveAll(db, tr, RequireHandles(a.Handles), OpenMode.ForWrite);
            var basePt = AcadEnv.ToPoint3d(RequireBasePoint(a.BasePoint));
            var m = Matrix3d.Rotation(deltaDeg * Math.PI / 180.0, Vector3d.ZAxis, basePt);
            foreach (var e in ents) e.TransformBy(m);

            return Wrap(new
            {
                affected = ents.Count,
                referenceAngleDeg = referenceDeg,
                newAngleDeg = a.NewAngleDeg,
                rotatedByDeg = deltaDeg,
                basePoint = new[] { basePt.X, basePt.Y, basePt.Z },
                note = "Rotated by " + deltaDeg.ToString("0.######") + " degrees, which is the " +
                       "difference between the two - not the angle given. Use modify.rotate if " +
                       "you already know how far to turn.",
            });
        });
}
