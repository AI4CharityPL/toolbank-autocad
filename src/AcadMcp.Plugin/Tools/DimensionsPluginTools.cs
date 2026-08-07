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

        // roadmap 3.2 - editing a dimension after it is placed
        host.Register("acad.dimensions.jogged_radius",         JoggedRadius);
        host.Register("acad.dimensions.oblique",               Oblique);
        host.Register("acad.dimensions.edit_dimension_text",   EditDimensionText);
        host.Register("acad.dimensions.tolerance",             DimTolerance);
        host.Register("acad.dimensions.update",                DimUpdate);
        host.Register("acad.dimensions.space",                 DimSpace);
        host.Register("acad.dimensions.arc_symbol",            ArcSymbol);
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

    // ─────────── roadmap 3.2: editing a dimension after it is placed ───────────
    //
    // The category could PLACE eleven kinds of dimension and change none of them afterwards.
    // All three tools below share one claim that is easy to get wrong and impossible to see in
    // a return code: they change how a dimension LOOKS and must not change what it MEASURES.
    // Measurement is therefore read before and after in every one of them, and a change in it
    // is reported as a failure rather than as a successful edit.

    private static Dimension RequireDimension(Database db, Transaction tr, string? handle, OpenMode mode)
    {
        if (string.IsNullOrWhiteSpace(handle))
            throw new ArgumentException("handle is required: the dimension to edit.");
        var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, handle!), mode);
        if (ent is Dimension d) return d;
        throw new ArgumentException(
            "Entity " + handle + " is a " + ent.GetType().Name + ", not a Dimension. These tools " +
            "edit dimensions that already exist; place one first with dimensions.linear, " +
            "aligned, radial and the rest.");
    }

    private static double RadiusOfCurve(Entity ent) => ent switch
    {
        Circle c => c.Radius,
        Arc a    => a.Radius,
        _ => throw new ArgumentException(
            "A jogged radius dimension expects a Circle or an Arc, not a " +
            ent.GetRXClass().Name + ". An ellipse has no single radius to jog.")
    };

    private static Task<ToolDispatchResult> JoggedRadius(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.jogged_radius", args, ct, (doc, db, tr) =>
        {
            var a = Read<JoggedRadiusArgsDto>(args);
            var ent = ResolveEntity(db, tr, a.CurveHandle);
            var centre = CenterOfCurve(ent);
            var radius = RadiusOfCurve(ent);
            var chord = AcadEnv.ToPoint3d(a.ChordPoint);

            // The whole point of DIMJOGGED: the real centre is off the sheet, so the dimension is
            // drawn from a FALSE centre near the arc and the line is jogged to say so. Default it
            // to a point on the same ray, one radius back from the chord point, which is where a
            // draughtsman would put it.
            var dir = (chord - centre).GetNormal();
            var overrideCentre = a.OverrideCenter is not null
                ? AcadEnv.ToPoint3d(a.OverrideCenter)
                : chord - dir * radius * 0.5;

            // The jog point must be OFF the centre-to-chord line. Measured, because the first
            // version defaulted it onto that line and every number still came out right: the
            // entity was a RadialDimensionLarge, the measurement was the true radius, the false
            // centre differed from the real one - and it drew as a plain straight leader with no
            // jog at all. Its lateral extent was 3.542, the width of the text and nothing more,
            // against 63.1 for the same dimension with the jog point moved 60 aside.
            var perp = new Vector3d(-dir.Y, dir.X, 0);
            var jog = a.JogPoint is not null
                ? AcadEnv.ToPoint3d(a.JogPoint)
                : overrideCentre + (chord - overrideCentre) * 0.5 + perp * radius * 0.15;

            // How far the jog sits off the leader. This is the number that separates a jogged
            // dimension from a straight one, so a caller who supplies their own collinear point
            // is told rather than handed a dimension that quietly is not jogged.
            var lateral = Math.Abs((jog - centre).DotProduct(perp));
            if (lateral < radius * 1e-3)
                throw new ArgumentException(
                    "jogPoint (" + jog.X + ", " + jog.Y + ") lies on the line from the centre to chordPoint, " +
                    "only " + lateral + " off it, so there is nothing for the jog to bend around " +
                    "and this would draw as a plain straight leader under a jogged class - " +
                    "indistinguishable on the sheet from dimensions.radial. Move it sideways, or " +
                    "omit jogPoint and a default " + (radius * 0.15) + " off the line is used.");

            var jogAngleDeg = a.JogAngleDeg ?? 45.0;
            if (jogAngleDeg <= 0 || jogAngleDeg >= 180)
                throw new ArgumentException(
                    "jogAngleDeg must be between 0 and 180 exclusive; AutoCAD's own default is 45. " +
                    "A jog of 0 or 180 degrees would be a straight line, which is a plain radial " +
                    "dimension - use dimensions.radial for that.");

            var dim = new RadialDimensionLarge(
                centre, chord, overrideCentre, jog,
                jogAngleDeg * Math.PI / 180.0,
                "",
                AcadEnv.ResolveDimStyleOrCurrent(db, tr, a.DimStyle));

            var handle = AcadEnv.Persist(db, tr, dim, a.Layer);

            // The jog is a DRAWING device. If it changed the measured radius the dimension would
            // be lying, so this is checked rather than assumed.
            var measured = dim.Measurement;
            if (Math.Abs(measured - radius) > 1e-6)
                throw new InvalidOperationException(
                    "The jogged dimension measures " + measured + " where the curve's radius is " +
                    radius + ". A jog changes how the dimension is DRAWN, never what it reports, " +
                    "so this is not being returned as success.");

            return Wrap(new
            {
                entity = handle,
                type = nameof(RadialDimensionLarge),
                radius,
                measurement = measured,
                jogAngleDeg,
                center = new[] { centre.X, centre.Y, centre.Z },
                overrideCenter = new[] { overrideCentre.X, overrideCentre.Y, overrideCentre.Z },
                jogPoint = new[] { jog.X, jog.Y, jog.Z },
                jogOffset = lateral,
                note = "A jogged radius exists for arcs whose true centre is off the sheet: the " +
                       "dimension is drawn from overrideCenter, a FALSE centre near the arc, and " +
                       "the bend in the leader says so. jogOffset is how far the jog sits off " +
                       "the centre-to-chord line - at zero the leader would be straight and this " +
                       "would be a plain radial dimension wearing a jogged class, so it is " +
                       "reported rather than assumed. The measurement is still the real radius, " +
                       measured + ". Use dimensions.radial when the centre is on the sheet.",
            });
        });

    private static Task<ToolDispatchResult> Oblique(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.oblique", args, ct, (doc, db, tr) =>
        {
            var a = Read<ObliqueArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0)
                throw new ArgumentException(
                    "handles is required: which dimensions to slant. DIMEDIT Oblique takes a " +
                    "selection because obliquing one dimension of a crowded chain and not its " +
                    "neighbours is what makes the chain readable.");
            if (a.ObliqueDeg is null)
                throw new ArgumentException(
                    "obliqueDeg is required: the angle the EXTENSION lines lean to, in degrees " +
                    "CCW from the X axis. Pass 0 to straighten them again.");

            var deg = a.ObliqueDeg.Value;
            var changed = new List<object>();

            foreach (var h in a.Handles)
            {
                var d = RequireDimension(db, tr, h, OpenMode.ForWrite);
                var before = d.Measurement;

                // Oblique is NOT on the Dimension base class - only the two linear kinds carry
                // it, because it is the extension lines that lean and a radial dimension has
                // none. Checked by the compiler, not assumed: Dimension.Oblique does not exist.
                switch (d)
                {
                    case RotatedDimension rd: rd.Oblique = deg * Math.PI / 180.0; break;
                    case AlignedDimension ad: ad.Oblique = deg * Math.PI / 180.0; break;
                    default:
                        throw new ArgumentException(
                            "Entity " + h + " is a " + d.GetType().Name + ". Only linear and " +
                            "aligned dimensions can be obliqued - they are the ones with " +
                            "extension lines to lean. A radial, diametric or angular dimension " +
                            "has none.");
                }

                // Obliquing is a change of APPEARANCE. A changed measurement would mean the
                // dimension now reports a different distance than the one it was placed on.
                var after = d.Measurement;
                if (Math.Abs(after - before) > 1e-9)
                    throw new InvalidOperationException(
                        "Dimension " + h + " measured " + before + " and now measures " + after +
                        ". Obliquing leans the extension lines and must not change the measured " +
                        "distance, so this is not being reported as success.");

                changed.Add(new { handle = h, type = d.GetType().Name, measurement = after });
            }

            return Wrap(new
            {
                affected = changed.Count,
                obliqueDeg = deg,
                dimensions = changed,
                note = "The EXTENSION lines now lean to " + deg + " degrees; the dimension line " +
                       "and the measured distance are untouched. This is what separates a " +
                       "crowded chain into readable steps. Pass obliqueDeg 0 to undo it.",
            });
        });

    private static Task<ToolDispatchResult> EditDimensionText(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.edit_dimension_text", args, ct, (doc, db, tr) =>
        {
            var a = Read<EditDimTextArgsDto>(args);
            var d = RequireDimension(db, tr, a.Handle, OpenMode.ForWrite);

            if (a.Text is null && a.TextPosition is null && a.TextRotationDeg is null &&
                a.ResetPosition is not true)
                throw new ArgumentException(
                    "Nothing to change. Give text, textPosition, textRotationDeg or " +
                    "resetPosition: true.");

            var measurement = d.Measurement;
            var textBefore = d.DimensionText;
            var posBefore = d.TextPosition;

            if (a.Text is not null)
            {
                // AutoCAD's own convention, and it is not obvious from the outside:
                //   ""   -> use the measurement (this is the DEFAULT state, not "blank")
                //   "<>" -> the measurement, embedded in surrounding text
                //   " "  -> a single space SUPPRESSES the text entirely
                // A caller passing "" to mean "clear the text" gets the measurement back
                // instead, which looks like the tool ignored them. Say so rather than let them
                // find out on the sheet.
                d.DimensionText = a.Text;
            }

            if (a.ResetPosition == true)
            {
                if (a.TextPosition is not null)
                    throw new ArgumentException(
                        "resetPosition and textPosition contradict each other: one puts the text " +
                        "back where the style wants it, the other puts it somewhere specific.");
                d.UsingDefaultTextPosition = true;
            }
            else if (a.TextPosition is not null)
            {
                d.TextPosition = AcadEnv.ToPoint3d(a.TextPosition);
                d.UsingDefaultTextPosition = false;
            }

            if (a.TextRotationDeg is not null)
                d.TextRotation = a.TextRotationDeg.Value * Math.PI / 180.0;

            // Rebuild the dimension's block so the change is on screen and not only in the
            // record. Without this a moved text can read back correctly and still be drawn in
            // its old place until the drawing is regenerated.
            d.RecomputeDimensionBlock(true);

            // The decisive check for this tool. An override changes what the dimension SAYS; it
            // must not change what it MEASURED, or the number on the sheet stops being a fact
            // about the drawing.
            var after = d.Measurement;
            if (Math.Abs(after - measurement) > 1e-9)
                throw new InvalidOperationException(
                    "The measurement changed from " + measurement + " to " + after + ". Editing " +
                    "the text overrides what is DISPLAYED and must never touch the measured " +
                    "value, so this is not being reported as success.");

            var textNow = d.DimensionText;
            var suppressed = textNow == " ";
            var usesMeasurement = string.IsNullOrEmpty(textNow) || textNow.Contains("<>");

            return Wrap(new
            {
                handle = a.Handle,
                type = d.GetType().Name,
                measurement = after,
                textBefore,
                text = textNow,
                displaysMeasurement = usesMeasurement,
                textSuppressed = suppressed,
                positionBefore = new[] { posBefore.X, posBefore.Y, posBefore.Z },
                textPosition = new[] { d.TextPosition.X, d.TextPosition.Y, d.TextPosition.Z },
                usingDefaultTextPosition = d.UsingDefaultTextPosition,
                note = "The measurement is unchanged at " + after + " - an override changes what " +
                       "the dimension SAYS, never what it measured. AutoCAD's conventions for " +
                       "text: \"\" means show the measurement (the default state, not blank), " +
                       "\"<>\" embeds the measurement inside your own text, and a single space " +
                       "suppresses the text altogether.",
            });
        });

    // ─────────── roadmap 3.2, second tranche ───────────

    /// <summary>The direction a linear dimension measures along, and the normal to it.</summary>
    /// <remarks>
    /// A rotated dimension carries its direction as an ANGLE; an aligned one takes it from its
    /// two extension line points. Read the wrong one and the perpendicular is wrong by the
    /// dimension's rotation, so every offset computed from it lands somewhere plausible.
    /// </remarks>
    private static (Vector3d dir, Vector3d perp) LinearAxes(Dimension d)
    {
        Vector3d dir;
        switch (d)
        {
            case RotatedDimension rd:
                dir = new Vector3d(Math.Cos(rd.Rotation), Math.Sin(rd.Rotation), 0);
                break;
            case AlignedDimension ald:
            {
                var v = ald.XLine2Point - ald.XLine1Point;
                if (v.Length < 1e-12)
                    throw new ArgumentException(
                        "Aligned dimension " + d.Handle + " has both extension lines at the same " +
                        "point, so it defines no direction to space along.");
                dir = v.GetNormal();
                break;
            }
            default:
                throw new ArgumentException(
                    "Entity " + d.Handle + " is a " + d.GetType().Name + ". Spacing applies to " +
                    "linear and aligned dimensions, the ones that sit on parallel dimension " +
                    "lines. A radial or angular dimension has no such line.");
        }
        return (dir, new Vector3d(-dir.Y, dir.X, 0));
    }

    private static Point3d DimLineOf(Dimension d) => d switch
    {
        RotatedDimension rd => rd.DimLinePoint,
        AlignedDimension ad => ad.DimLinePoint,
        _ => throw new ArgumentException(
            "Entity " + d.Handle + " is a " + d.GetType().Name + " and has no dimension line " +
            "point to move."),
    };

    private static void SetDimLine(Dimension d, Point3d p)
    {
        switch (d)
        {
            case RotatedDimension rd: rd.DimLinePoint = p; break;
            case AlignedDimension ad: ad.DimLinePoint = p; break;
            default: throw new ArgumentException("Entity " + d.Handle + " has no dimension line.");
        }
    }

    private static Task<ToolDispatchResult> DimTolerance(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.tolerance", args, ct, (doc, db, tr) =>
        {
            var a = Read<DimToleranceArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0)
                throw new ArgumentException("handles is required: which dimensions to change.");

            var mode = (a.Mode ?? "symmetrical").Trim().ToLowerInvariant();
            if (mode is not ("none" or "symmetrical" or "deviation" or "limits"))
                throw new ArgumentException(
                    "mode must be none, symmetrical, deviation or limits. symmetrical prints one " +
                    "plus-minus value and needs upper only; deviation prints separate upper and " +
                    "lower; limits replaces the measurement with the two extreme sizes; none " +
                    "turns the tolerance off.");

            if (mode is "symmetrical" or "deviation" or "limits" && a.Upper is null)
                throw new ArgumentException(
                    "upper is required for mode " + mode + ": how far ABOVE the measured size is " +
                    "still acceptable.");
            if (mode is "deviation" or "limits" && a.Lower is null)
                throw new ArgumentException(
                    "lower is required for mode " + mode + ". A symmetrical tolerance does not " +
                    "take one - the same value is used both ways.");

            var changed = new List<object>();
            foreach (var h in a.Handles)
            {
                var d = RequireDimension(db, tr, h, OpenMode.ForWrite);
                var before = d.Measurement;

                switch (mode)
                {
                    case "none":
                        d.Dimtol = false;
                        d.Dimlim = false;
                        break;
                    case "symmetrical":
                        d.Dimtol = true;
                        d.Dimlim = false;
                        // AutoCAD prints a single plus-minus only when the two agree, so the
                        // lower is set from the upper rather than left at whatever it held.
                        d.Dimtp = a.Upper!.Value;
                        d.Dimtm = a.Upper!.Value;
                        break;
                    case "deviation":
                        d.Dimtol = true;
                        d.Dimlim = false;
                        d.Dimtp = a.Upper!.Value;
                        d.Dimtm = a.Lower!.Value;
                        break;
                    case "limits":
                        d.Dimlim = true;
                        d.Dimtol = false;
                        d.Dimtp = a.Upper!.Value;
                        d.Dimtm = a.Lower!.Value;
                        break;
                }
                if (a.Decimals is not null)
                {
                    if (a.Decimals < 0 || a.Decimals > 8)
                        throw new ArgumentException("decimals must be between 0 and 8.");
                    d.Dimtdec = a.Decimals.Value;
                }

                d.RecomputeDimensionBlock(true);

                // A tolerance says how much the MEASURED size may vary. It must not change the
                // measured size itself.
                var after = d.Measurement;
                if (Math.Abs(after - before) > 1e-9)
                    throw new InvalidOperationException(
                        "Dimension " + h + " measured " + before + " and now measures " + after +
                        ". A tolerance annotates the measurement and must not alter it.");

                changed.Add(new
                {
                    handle = h,
                    type = d.GetType().Name,
                    measurement = after,
                    dimtol = d.Dimtol,
                    dimlim = d.Dimlim,
                    upper = d.Dimtp,
                    lower = d.Dimtm,
                    decimals = d.Dimtdec,
                });
            }

            return Wrap(new
            {
                affected = changed.Count,
                mode,
                dimensions = changed,
                note = "These are per-entity OVERRIDES of the dimension style, not edits to the " +
                       "style - every other dimension using that style is untouched. " +
                       "dimensions.update puts an overridden dimension back under its style.",
            });
        });

    private static Task<ToolDispatchResult> DimUpdate(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.update", args, ct, (doc, db, tr) =>
        {
            var a = Read<DimUpdateArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0)
                throw new ArgumentException("handles is required: which dimensions to update.");

            var styleId = AcadEnv.ResolveDimStyleOrCurrent(db, tr, a.DimStyle);
            var styleRec = (DimStyleTableRecord)tr.GetObject(styleId, OpenMode.ForRead);

            var changed = new List<object>();
            foreach (var h in a.Handles)
            {
                var d = RequireDimension(db, tr, h, OpenMode.ForWrite);
                var measured = d.Measurement;
                var styleBefore = d.DimensionStyleName;
                var tolBefore = d.Dimtol || d.Dimlim;

                // SetDimstyleData re-applies the style's own values to the entity. That is the
                // whole difference from set_entity_dimstyle, which assigns DimensionStyle and
                // leaves per-entity overrides standing - so the override state is REPORTED
                // before and after rather than claimed in a description.
                d.DimensionStyle = styleId;
                d.SetDimstyleData(styleRec);
                d.RecomputeDimensionBlock(true);

                var after = d.Measurement;
                if (Math.Abs(after - measured) > 1e-9)
                    throw new InvalidOperationException(
                        "Dimension " + h + " measured " + measured + " and now measures " + after +
                        ". Applying a style changes presentation, never the measured value.");

                changed.Add(new
                {
                    handle = h,
                    type = d.GetType().Name,
                    measurement = after,
                    styleBefore,
                    style = d.DimensionStyleName,
                    toleranceOverrideBefore = tolBefore,
                    toleranceOverrideAfter = d.Dimtol || d.Dimlim,
                });
            }

            return Wrap(new
            {
                affected = changed.Count,
                style = styleRec.Name,
                dimensions = changed,
                note = "Re-applies the style's own values to each dimension. " +
                       "toleranceOverrideBefore and toleranceOverrideAfter are reported for " +
                       "every one, because that pair is the only thing separating this from " +
                       "set_entity_dimstyle, which assigns the style and leaves overrides alone.",
            });
        });

    private static Task<ToolDispatchResult> DimSpace(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.space", args, ct, (doc, db, tr) =>
        {
            var a = Read<DimSpaceArgsDto>(args);
            if (a.Handles is null || a.Handles.Count < 2)
                throw new ArgumentException(
                    "handles needs at least 2 dimensions: one to space, and the base it is " +
                    "spaced from.");
            if (a.Spacing is null)
                throw new ArgumentException(
                    "spacing is required: the gap between neighbouring dimension lines. Pass 0 " +
                    "to ALIGN them all onto the base's line instead, which is what DIMSPACE " +
                    "does with a spacing of zero.");
            if (a.Spacing < 0)
                throw new ArgumentException(
                    "spacing cannot be negative. Which side each dimension lands on is decided " +
                    "by where it already sits, not by a sign.");

            var baseHandle = a.BaseHandle ?? a.Handles[0];
            if (!a.Handles.Contains(baseHandle, StringComparer.OrdinalIgnoreCase))
                throw new ArgumentException(
                    "baseHandle " + baseHandle + " is not in handles. The base is the dimension " +
                    "that stays put, so it has to be one of the ones being spaced.");

            var baseDim = RequireDimension(db, tr, baseHandle, OpenMode.ForRead);
            var (dir, perp) = LinearAxes(baseDim);
            var basePoint = DimLineOf(baseDim);

            // Every dimension must be PARALLEL to the base. Spacing a chain that is not would
            // move each one along a direction meaningless for it, and still return a tidy list
            // of new positions.
            var others = new List<(string h, Dimension d, double offset)>();
            foreach (var h in a.Handles)
            {
                if (string.Equals(h, baseHandle, StringComparison.OrdinalIgnoreCase)) continue;
                var d = RequireDimension(db, tr, h, OpenMode.ForWrite);
                var (dd, _) = LinearAxes(d);
                if (Math.Abs(Math.Abs(dd.DotProduct(dir)) - 1.0) > 1e-6)
                    throw new ArgumentException(
                        "Dimension " + h + " is not parallel to the base " + baseHandle +
                        "; their directions differ by " +
                        (Math.Acos(Math.Min(1, Math.Abs(dd.DotProduct(dir)))) * 180 / Math.PI) +
                        " degrees. Spacing only means something for dimensions sharing a " +
                        "direction, so group them and space each direction separately.");
                others.Add((h, d, (DimLineOf(d) - basePoint).DotProduct(perp)));
            }

            // Each side of the base is numbered outwards from it, so a dimension already above
            // the base stays above it. Sorting by offset within a side keeps the chain in the
            // order it was drawn instead of reshuffling it.
            var above = others.Where(o => o.offset >= 0).OrderBy(o => o.offset).ToList();
            var below = others.Where(o => o.offset < 0).OrderByDescending(o => o.offset).ToList();

            var moved = new List<object>();
            void Place(List<(string h, Dimension d, double offset)> side, int sign)
            {
                for (int i = 0; i < side.Count; i++)
                {
                    var (h, d, was) = side[i];
                    var target = a.Spacing.Value == 0 ? 0 : sign * (i + 1) * a.Spacing.Value;
                    var before = d.Measurement;
                    SetDimLine(d, basePoint + perp * target);
                    d.RecomputeDimensionBlock(true);

                    var now = (DimLineOf(d) - basePoint).DotProduct(perp);
                    if (Math.Abs(now - target) > 1e-6)
                        throw new InvalidOperationException(
                            "Dimension " + h + " sits " + now + " from the base where " + target +
                            " was intended, so the move did not take.");
                    if (Math.Abs(d.Measurement - before) > 1e-9)
                        throw new InvalidOperationException(
                            "Dimension " + h + " changed what it measures while being moved, " +
                            "from " + before + " to " + d.Measurement + ".");

                    moved.Add(new
                    {
                        handle = h,
                        type = d.GetType().Name,
                        offsetBefore = was,
                        offset = now,
                        measurement = d.Measurement,
                    });
                }
            }
            Place(above, +1);
            Place(below, -1);

            return Wrap(new
            {
                affected = moved.Count,
                baseHandle,
                spacing = a.Spacing,
                aligned = a.Spacing.Value == 0,
                basePoint = new[] { basePoint.X, basePoint.Y, basePoint.Z },
                dimensions = moved,
                note = a.Spacing.Value == 0
                    ? "spacing 0 ALIGNS every dimension onto the base's dimension line rather " +
                      "than stacking them, so each offset is now 0."
                    : "Each dimension sits a multiple of " + a.Spacing + " from the base, " +
                      "numbered outwards on whichever side it was already on. The base did not " +
                      "move and no measurement changed.",
            });
        });

    private static Task<ToolDispatchResult> ArcSymbol(JsonObject args, CancellationToken ct) =>
        Run("acad.dimensions.arc_symbol", args, ct, (doc, db, tr) =>
        {
            var a = Read<ArcSymbolArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0)
                throw new ArgumentException("handles is required: which arc length dimensions.");

            // DIMARCSYM is a plain int on the entity, not an enum - the compiler said so by
            // rejecting a string for it. Naming the three positions keeps 0, 1 and 2 out of the
            // caller's hands, where they mean nothing.
            var pos = (a.Position ?? "preceding").Trim().ToLowerInvariant();
            int value = pos switch
            {
                "preceding" => 0,
                "above" => 1,
                "none" => 2,
                _ => throw new ArgumentException(
                    "position must be preceding (the symbol before the text, AutoCAD's default), " +
                    "above (over the text), or none (no symbol at all)."),
            };

            var changed = new List<object>();
            foreach (var h in a.Handles)
            {
                var d = RequireDimension(db, tr, h, OpenMode.ForWrite);
                if (d is not ArcDimension arc)
                    throw new ArgumentException(
                        "Entity " + h + " is a " + d.GetType().Name + ", not an ArcDimension. " +
                        "The arc symbol belongs to an arc LENGTH dimension - place one with " +
                        "dimensions.arc_length. A radial dimension on the same arc is a " +
                        "different thing and has no such symbol.");

                var before = arc.Measurement;
                var symBefore = arc.ArcSymbolType;
                arc.ArcSymbolType = value;
                arc.RecomputeDimensionBlock(true);

                if (arc.ArcSymbolType != value)
                    throw new InvalidOperationException(
                        "Arc symbol on " + h + " reads back as " + arc.ArcSymbolType +
                        " rather than " + value + ", so the change did not take.");
                if (Math.Abs(arc.Measurement - before) > 1e-9)
                    throw new InvalidOperationException(
                        "Dimension " + h + " changed what it measures, from " + before + " to " +
                        arc.Measurement + ". A symbol is decoration and must not touch the arc " +
                        "length.");

                changed.Add(new
                {
                    handle = h,
                    arcSymbolBefore = symBefore,
                    arcSymbol = arc.ArcSymbolType,
                    measurement = arc.Measurement,
                });
            }

            return Wrap(new
            {
                affected = changed.Count,
                position = pos,
                arcSymbolType = value,
                dimensions = changed,
                note = "0 puts the arc symbol before the text, 1 above it, 2 removes it. The " +
                       "measured arc length is untouched either way.",
            });
        });
}
