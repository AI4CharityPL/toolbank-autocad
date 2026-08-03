// AutoCAD plugin handlers for the acad-dimensions category.
// Registered under "acad.dimensions.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern),
//        27 (text-and-table traps - dimension subclasses).

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

internal static class DimensionsPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.dimensions.linear",                Linear);
        host.Register("acad.dimensions.aligned",               Aligned);
        host.Register("acad.dimensions.angular_3p",            Angular3p);
        host.Register("acad.dimensions.angular_2l",            Angular2l);
        host.Register("acad.dimensions.radial",                Radial);
        host.Register("acad.dimensions.diametric",             Diametric);
        host.Register("acad.dimensions.arc_length",            ArcLengthDim);
        host.Register("acad.dimensions.ordinate",              Ordinate);
        host.Register("acad.dimensions.baseline_chain",        BaselineChain);
        host.Register("acad.dimensions.continued_chain",       ContinuedChain);
        host.Register("acad.dimensions.list_dimstyles",        ListDimStyles);
        host.Register("acad.dimensions.set_entity_dimstyle",   SetEntityDimStyle);
        host.Register("acad.dimensions.ensure_architectural_dimstyle", EnsureArchitecturalDimStyle);
        host.Register("acad.dimensions.cumulative_chain",      CumulativeChain);
        host.Register("acad.dimensions.apply_arch_tick_style", ApplyArchTickStyle);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct, Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    // ─────────── linear / aligned ───────────

    private static Task<ToolDispatchResult> Linear(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.linear", args, ct, (doc, db, tr) =>
        {
            var a = Read<LinearDimArgsDto>(args);
            // trap #5 (rule 27): use RotatedDimension for linear.
            var dim = new RotatedDimension(
                a.RotationDeg * Math.PI / 180.0,
                AcadEnv.ToPoint3d(a.P1),
                AcadEnv.ToPoint3d(a.P2),
                AcadEnv.ToPoint3d(a.DimLinePoint),
                a.TextOverride ?? "",
                AcadEnv.ResolveDimStyleOrCurrent(db, tr, a.DimStyle));
            return Wrap(new { entity = AcadEnv.Persist(db, tr, dim, a.Layer) });
        });

    private static Task<ToolDispatchResult> Aligned(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.aligned", args, ct, (doc, db, tr) =>
        {
            var a = Read<AlignedDimArgsDto>(args);
            var dim = new AlignedDimension(
                AcadEnv.ToPoint3d(a.P1),
                AcadEnv.ToPoint3d(a.P2),
                AcadEnv.ToPoint3d(a.DimLinePoint),
                a.TextOverride ?? "",
                AcadEnv.ResolveDimStyleOrCurrent(db, tr, a.DimStyle));
            return Wrap(new { entity = AcadEnv.Persist(db, tr, dim, a.Layer) });
        });

    // ─────────── angular ───────────

    private static Task<ToolDispatchResult> Angular3p(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.angular_3p", args, ct, (doc, db, tr) =>
        {
            var a = Read<AngularDim3pArgsDto>(args);
            var dim = new Point3AngularDimension(
                AcadEnv.ToPoint3d(a.Center),
                AcadEnv.ToPoint3d(a.First),
                AcadEnv.ToPoint3d(a.Second),
                AcadEnv.ToPoint3d(a.ArcPoint),
                "",
                AcadEnv.ResolveDimStyleOrCurrent(db, tr, a.DimStyle));
            return Wrap(new { entity = AcadEnv.Persist(db, tr, dim, a.Layer) });
        });

    private static Task<ToolDispatchResult> Angular2l(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.angular_2l", args, ct, (doc, db, tr) =>
        {
            var a = Read<AngularDim2lArgsDto>(args);
            var l1 = ResolveLine(db, tr, a.Line1Handle);
            var l2 = ResolveLine(db, tr, a.Line2Handle);
            // LineAngularDimension2(line1Start, line1End, line2Start, line2End, arcPoint, text, dimStyleId)
            var dim = new LineAngularDimension2(
                l1.StartPoint, l1.EndPoint, l2.StartPoint, l2.EndPoint,
                AcadEnv.ToPoint3d(a.ArcPoint),
                "",
                AcadEnv.ResolveDimStyleOrCurrent(db, tr, a.DimStyle));
            return Wrap(new { entity = AcadEnv.Persist(db, tr, dim, a.Layer) });
        });

    // ─────────── radial / diametric / arc length ───────────

    private static Task<ToolDispatchResult> Radial(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.radial", args, ct, (doc, db, tr) =>
        {
            var a = Read<RadialDimArgsDto>(args);
            var ent = ResolveEntity(db, tr, a.CurveHandle);
            var dim = new RadialDimension(
                CenterOfCurve(ent),
                AcadEnv.ToPoint3d(a.ChordPoint),
                a.LeaderLength,
                "",
                AcadEnv.ResolveDimStyleOrCurrent(db, tr, a.DimStyle));
            return Wrap(new { entity = AcadEnv.Persist(db, tr, dim, a.Layer) });
        });

    private static Task<ToolDispatchResult> Diametric(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.diametric", args, ct, (doc, db, tr) =>
        {
            var a = Read<DiametricDimArgsDto>(args);
            var ent = ResolveEntity(db, tr, a.CurveHandle);
            if (ent is not Curve c)
                throw new ArgumentException($"diametric dimension expects a Curve entity, got {ent.GetRXClass().Name}.");

            // DiametricDimension(chordPoint, farChordPoint, ...) measures the distance BETWEEN
            // its two points, and both must lie on the circle, diametrically opposite.
            //
            // This used to pass (GetClosestPointTo(farPoint), farPoint). When the caller does
            // the natural thing and gives a point already on the circle - as the description
            // asks for - the closest point on the circle to it is itself, so both arguments
            // were the same point and the dimension measured zero. Verified live: a circle of
            // radius 3000 was annotated "0" instead of 6000.
            var centre = CenterOfCurve(ent);
            var onCurve = c.GetClosestPointTo(AcadEnv.ToPoint3d(a.FarPoint), false);

            // Antipode through the centre. Works whether the caller's point was on the circle
            // or merely near it, since it is projected first.
            var opposite = centre + (centre - onCurve);

            var dim = new DiametricDimension(
                onCurve, opposite, a.LeaderLength, "",
                AcadEnv.ResolveDimStyleOrCurrent(db, tr, a.DimStyle));
            return Wrap(new { entity = AcadEnv.Persist(db, tr, dim, a.Layer) });
        });

    private static Task<ToolDispatchResult> ArcLengthDim(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.arc_length", args, ct, (doc, db, tr) =>
        {
            var a = Read<ArcLengthDimArgsDto>(args);
            var ent = ResolveEntity(db, tr, a.ArcHandle);
            if (ent is not Arc arc)
                throw new ArgumentException($"arc-length dimension expects an Arc entity, got {ent.GetRXClass().Name}.");
            var dim = new ArcDimension(
                arc.Center, arc.StartPoint, arc.EndPoint,
                AcadEnv.ToPoint3d(a.ArcPoint),
                "",
                AcadEnv.ResolveDimStyleOrCurrent(db, tr, a.DimStyle));
            return Wrap(new { entity = AcadEnv.Persist(db, tr, dim, a.Layer) });
        });

    private static Task<ToolDispatchResult> Ordinate(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.ordinate", args, ct, (doc, db, tr) =>
        {
            var a = Read<OrdinateDimArgsDto>(args);
            var dim = new OrdinateDimension(
                a.UseXAxis,
                AcadEnv.ToPoint3d(a.DefiningPoint),
                AcadEnv.ToPoint3d(a.LeaderEnd),
                "",
                AcadEnv.ResolveDimStyleOrCurrent(db, tr, a.DimStyle));
            return Wrap(new { entity = AcadEnv.Persist(db, tr, dim, a.Layer) });
        });

    // ─────────── baseline / continued ───────────

    private static Task<ToolDispatchResult> BaselineChain(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.baseline_chain", args, ct, (doc, db, tr) =>
        {
            var a = Read<BaselineDimArgsDto>(args);
            // trap #7 (rule 27): build N RotatedDimensions sharing baseline (P1=baselinePoint, P2=Pi).
            // Stagger dimLineOffset by current dimstyle DIMDLI per chain step.
            double rotRad = a.RotationDeg * Math.PI / 180.0;
            var dimStyleId = AcadEnv.ResolveDimStyleOrCurrent(db, tr, a.DimStyle);
            var dimStyle = (DimStyleTableRecord)tr.GetObject(dimStyleId, OpenMode.ForRead);
            double dli = dimStyle.Dimdli * dimStyle.Dimscale;

            var basePt = AcadEnv.ToPoint3d(a.BaselinePoint);
            var dimLine0 = AcadEnv.ToPoint3d(a.DimLinePoint);
            var perp = new Vector3d(-Math.Sin(rotRad), Math.Cos(rotRad), 0).GetNormal();

            var handles = new List<EntityHandle>();
            for (int i = 0; i < a.SubsequentPoints.Count; i++)
            {
                var p2 = AcadEnv.ToPoint3d(a.SubsequentPoints[i]);
                var dimLineP = dimLine0 + (perp * dli * i);
                var dim = new RotatedDimension(rotRad, basePt, p2, dimLineP, "", dimStyleId);
                handles.Add(AcadEnv.Persist(db, tr, dim, a.Layer));
            }
            return Wrap(new { entities = handles });
        });

    private static Task<ToolDispatchResult> ContinuedChain(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.continued_chain", args, ct, (doc, db, tr) =>
        {
            var a = Read<ContinuedDimArgsDto>(args);
            double rotRad = a.RotationDeg * Math.PI / 180.0;
            var dimStyleId = AcadEnv.ResolveDimStyleOrCurrent(db, tr, a.DimStyle);
            var dimLineP = AcadEnv.ToPoint3d(a.DimLinePoint);
            var prev = AcadEnv.ToPoint3d(a.StartPoint);
            var handles = new List<EntityHandle>();
            for (int i = 0; i < a.SubsequentPoints.Count; i++)
            {
                var cur = AcadEnv.ToPoint3d(a.SubsequentPoints[i]);
                var dim = new RotatedDimension(rotRad, prev, cur, dimLineP, "", dimStyleId);
                handles.Add(AcadEnv.Persist(db, tr, dim, a.Layer));
                prev = cur;
            }
            return Wrap(new { entities = handles });
        });

    // ─────────── styles ───────────

    private static Task<ToolDispatchResult> ListDimStyles(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.list_dimstyles", args, ct, (doc, db, tr) =>
        {
            var dst = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
            var styles = new List<string>();
            foreach (ObjectId id in dst)
            {
                var rec = (DimStyleTableRecord)tr.GetObject(id, OpenMode.ForRead);
                styles.Add(rec.Name);
            }
            string current;
            try
            {
                var cur = (DimStyleTableRecord)tr.GetObject(db.Dimstyle, OpenMode.ForRead);
                current = cur.Name;
            }
            catch { current = "Standard"; }
            return Wrap(new { styles, current });
        });

    private static Task<ToolDispatchResult> SetEntityDimStyle(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.set_entity_dimstyle", args, ct, (doc, db, tr) =>
        {
            var a = Read<SetDimStyleArgsDto>(args);
            var dst = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
            if (!dst.Has(a.DimStyle))
                throw new ArgumentException($"Dim style '{a.DimStyle}' does not exist.");
            ObjectId styleId = dst[a.DimStyle];
            int n = 0;
            foreach (var h in a.Handles)
            {
                var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForWrite);
                if (ent is Dimension d) { d.DimensionStyle = styleId; n++; }
            }
            return Wrap(new { affected = n });
        });

    // ─────────── D6 additions ───────────

    private static Task<ToolDispatchResult> EnsureArchitecturalDimStyle(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.ensure_architectural_dimstyle", args, ct, (doc, db, tr) =>
        {
            var a = Read<EnsureArchitecturalDimStyleArgsDto>(args);
            var dst = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForWrite);

            DimStyleTableRecord rec;
            bool created = false, updated = false;
            if (dst.Has(a.StyleName))
            {
                rec = (DimStyleTableRecord)tr.GetObject(dst[a.StyleName], OpenMode.ForWrite);
                updated = true;
            }
            else
            {
                rec = new DimStyleTableRecord { Name = a.StyleName };
                dst.Add(rec);
                tr.AddNewlyCreatedDBObject(rec, true);
                created = true;
            }

            // Architectural-tick configuration. Text height / arrow size are expressed in plot mm
            // and multiplied by DIMSCALE (plot scale denominator) on render.
            rec.Dimscale = a.Scale;
            rec.Dimtxt   = a.TextHeightMm;
            rec.Dimasz   = a.ArrowSizeMm;
            rec.Dimtih   = false;       // text horizontal for inside dims: false = aligned with dim line
            rec.Dimtoh   = false;
            rec.Dimtad   = 1;           // text above dim line
            rec.Dimgap   = 0.625;       // gap between text and dim line (plot mm)
            rec.Dimexe   = 1.25;        // extension line extension past dim line
            rec.Dimexo   = 0.625;       // offset from origin
            rec.Dimdli   = 7.0;         // baseline spacing (plot mm)
            rec.Dimlfac  = 1.0;         // unit scale
            rec.Dimrnd   = a.RoundToMm; // round to mm
            rec.Dimdec   = a.DecimalPlaces;
            rec.Dimzin   = a.SuppressZeros ? 8 : 0;  // 8 = suppress trailing zeros
            rec.Dimtxsty = db.Textstyle;

            // ArchTick arrowheads: we use the built-in "_ArchTick" block name.
            // AutoCAD recognises it when DIMBLK/DIMBLK1/DIMBLK2 are set to the shared ObjectId.
            try
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                if (bt.Has("_ArchTick"))
                {
                    var archTickId = bt["_ArchTick"];
                    rec.Dimblk  = archTickId;
                    rec.Dimblk1 = archTickId;
                    rec.Dimblk2 = archTickId;
                    rec.Dimsah  = false;
                }
            }
            catch { /* block may not exist in older templates; leave default */ }

            bool madeCurrent = false;
            if (a.MakeCurrent)
            {
                db.Dimstyle = rec.ObjectId;
                madeCurrent = true;
            }

            return Wrap(new EnsureArchDimStyleResultDto(a.StyleName, created, updated && !created, madeCurrent));
        });

    private static Task<ToolDispatchResult> CumulativeChain(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.cumulative_chain", args, ct, (doc, db, tr) =>
        {
            var a = Read<CumulativeDimArgsDto>(args);
            double rotRad = a.RotationDeg * Math.PI / 180.0;
            var dimStyleId = AcadEnv.ResolveDimStyleOrCurrent(db, tr, a.DimStyle);
            var basePt = AcadEnv.ToPoint3d(a.BaselinePoint);
            var dimLineP = AcadEnv.ToPoint3d(a.DimLinePoint);

            var handles = new List<EntityHandle>();
            for (int i = 0; i < a.SubsequentPoints.Count; i++)
            {
                var p2 = AcadEnv.ToPoint3d(a.SubsequentPoints[i]);
                var dim = new RotatedDimension(rotRad, basePt, p2, dimLineP, "", dimStyleId);
                handles.Add(AcadEnv.Persist(db, tr, dim, a.Layer));
            }
            return Wrap(new { entities = handles });
        });

    private static Task<ToolDispatchResult> ApplyArchTickStyle(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.apply_arch_tick_style", args, ct, (doc, db, tr) =>
        {
            var a = Read<ApplyArchTickStyleArgsDto>(args);
            var dst = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForWrite);
            bool styleEnsured = false;

            if (!dst.Has(a.DimStyle))
            {
                if (!a.EnsureStyle)
                    throw new ArgumentException($"Dim style '{a.DimStyle}' does not exist (ensureStyle=false).");
                var rec = new DimStyleTableRecord { Name = a.DimStyle, Dimscale = 100.0, Dimtxt = 2.5, Dimasz = 2.5, Dimtad = 1, Dimrnd = 1.0, Dimdec = 0, Dimzin = 8 };
                dst.Add(rec);
                tr.AddNewlyCreatedDBObject(rec, true);
                styleEnsured = true;
            }
            var styleId = dst[a.DimStyle];

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            int scanned = 0, updated = 0;
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                if (ent is not Dimension dim) continue;
                if (!string.Equals(dim.Layer, a.Layer, StringComparison.OrdinalIgnoreCase)) continue;
                scanned++;
                if (dim.DimensionStyle == styleId) continue;
                tr.GetObject(id, OpenMode.ForWrite);
                dim.DimensionStyle = styleId;
                updated++;
            }
            return Wrap(new { scanned, updated, styleEnsured });
        });

    // ─────────── helpers ───────────

    private static Line ResolveLine(Database db, Transaction tr, string handle)
    {
        var ent = ResolveEntity(db, tr, handle);
        if (ent is Line l) return l;
        throw new ArgumentException($"handle '{handle}' is not a Line (got {ent.GetRXClass().Name}).");
    }

    private static Entity ResolveEntity(Database db, Transaction tr, string handle)
    {
        var id = AcadEnv.ResolveHandle(db, handle);
        return (Entity)tr.GetObject(id, OpenMode.ForRead);
    }

    private static Point3d CenterOfCurve(Entity ent) => ent switch
    {
        Circle c => c.Center,
        Arc a    => a.Center,
        Ellipse e => e.Center,
        _ => throw new ArgumentException($"radial dimension expects Circle/Arc/Ellipse, got {ent.GetRXClass().Name}.")
    };
}
