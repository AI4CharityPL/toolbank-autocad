// AutoCAD plugin handlers for acad-plumbing. Nine tools over a parametric
// sanitary-fixture block factory. All fixtures follow WT-2019 + PN-EN 17210
// minimum footprints.
//
// Catalogue blocks (all origin at geometric centre):
//   PLMB-WC-FS        370 × 650    floor-standing WC
//   PLMB-WC-WH        370 × 540    wall-hung WC
//   PLMB-WC-BID       370 × 550    bidet-combo
//   PLMB-WC-ACC       800 × 800    accessible WC (grab-bar markers)
//   PLMB-BSN-STD      600 × 450    basin
//   PLMB-BSN-DBL     1200 × 450    double basin
//   PLMB-BSN-ACC-<W>-<D>           accessible basin (knee clearance)
//   PLMB-SHW-SQ-<W>-<D>            shower tray (square / rect)
//   PLMB-SHW-WI-<W>-<D>            walk-in shower (no raised tray + curtain)
//   PLMB-BT-<VAR>-<W>-<D>          bathtub (standard / mini / corner)
//   PLMB-UR-STD       380 × 340    urinal
//   PLMB-UR-ACC       380 × 450    accessible urinal
//
// Rules: 10, 11, 12, 19, 28, 63.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using AcadMcp.Shared.Catalogs;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace AcadMcp.Plugin.Tools;

internal static class PlumbingPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.plumbing.list_plumbing_catalog",  ListCatalog);
        host.Register("acad.plumbing.insert_plumbing",        InsertPlumbing);
        host.Register("acad.plumbing.insert_wc",              InsertWc);
        host.Register("acad.plumbing.insert_basin",           InsertBasin);
        host.Register("acad.plumbing.insert_shower",          InsertShower);
        host.Register("acad.plumbing.insert_bathtub",         InsertBathtub);
        host.Register("acad.plumbing.insert_urinal",          InsertUrinal);
        host.Register("acad.plumbing.populate_bathroom",      PopulateBathroom);
        host.Register("acad.plumbing.list_plumbing_in_model", ListPlumbingInModel);
    }

    // ─────────── helpers ───────────

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> RunW(string tk, JsonObject args, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(tk, ct, work);

    private static Task<ToolDispatchResult> RunR(string tk, JsonObject args, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunReadAsync(tk, ct, work);

    private static string NameForSized(string family, double wMm, double dMm)
        => $"{family}-{(int)Math.Round(wMm)}-{(int)Math.Round(dMm)}";

    private static string DefaultLayerFor(string blockName)
    {
        if (blockName.StartsWith("PLMB-WC",  StringComparison.OrdinalIgnoreCase)) return "A-PLMB-WC";
        if (blockName.StartsWith("PLMB-BSN", StringComparison.OrdinalIgnoreCase)) return "A-PLMB-BSN";
        if (blockName.StartsWith("PLMB-SHW", StringComparison.OrdinalIgnoreCase)) return "A-PLMB-SHW";
        if (blockName.StartsWith("PLMB-BT",  StringComparison.OrdinalIgnoreCase)) return "A-PLMB-BT";
        if (blockName.StartsWith("PLMB-UR",  StringComparison.OrdinalIgnoreCase)) return "A-PLMB-UR";
        return "A-PLMB";
    }

    // ─────────── catalog metadata ───────────

    // Catalogue data and name resolution live in AcadMcp.Shared.Catalogs.PlumbingCatalog,
    // outside AutoCAD's reach so CI can test them. Geometry stays here.

    private static Task<ToolDispatchResult> ListCatalog(JsonObject args, CancellationToken ct) =>
        RunR("acad.plumbing.list_plumbing_catalog", args, ct, (doc, db, tr) =>
        {
            var a = Read<PlumbListCatalogDto>(args);
            var list = PlumbingCatalog.All(a.CategoryFilter, a.DomainFilter, a.AccessibleOnly);

            return Wrap(new { entries = list, count = list.Count });
        });

    // ─────────── block factory dispatch ───────────

    private static (ObjectId BtrId, bool Created, double WMm, double DMm, bool Accessible) EnsureBlock(
        Database db, Transaction tr, string name, double? sizedW, double? sizedD)
    {
        AcadEnv.ValidateSymbolName(name, "Block");
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        if (bt.Has(name))
        {
            var accExisting = LookupAccessibleByName(name);
            return (bt[name], false, sizedW ?? 0, sizedD ?? 0, accExisting);
        }

        bt.UpgradeOpen();
        var btr = new BlockTableRecord { Name = name };
        var btrId = bt.Add(btr);
        tr.AddNewlyCreatedDBObject(btr, true);

        var (w, d, acc) = BuildBlockGeometry(tr, btr, name, sizedW, sizedD);
        AddStandardAttributes(tr, btr, w, d, acc);
        return (btrId, true, w, d, acc);
    }

    private static bool LookupAccessibleByName(string name)
    {
        // An existing block: we did not draw it this call, but the caller still needs to know
        // whether it satisfies the barrier-free clearances, so ask the catalogue. A name that
        // is not ours at all is reported as not accessible rather than throwing - the block
        // exists either way and refusing here would be worse than the honest false.
        try
        {
            return PlumbingCatalog.Resolve(name).Accessible;
        }
        catch (CatalogNameException)
        {
            return false;
        }
    }

    private static (double W, double D, bool Accessible) BuildBlockGeometry(
        Transaction tr, BlockTableRecord btr, string name, double? sizedW, double? sizedD)
    {
        // See FurniturePluginTools for the reasoning: one resolution, then pure dispatch.
        var r = PlumbingCatalog.Resolve(name, sizedW, sizedD);

        if (r.Match == CatalogMatch.Fixed)
        {
            BuildFixedBlock(tr, btr, r.Entry);
            return (r.WidthMm, r.DepthMm, r.Accessible);
        }

        BuildSizedBlock(tr, btr, r.Family, r.WidthMm, r.DepthMm);
        return (r.WidthMm, r.DepthMm, r.Accessible);
    }

    private static void BuildFixedBlock(Transaction tr, BlockTableRecord btr, PlumbingCatalogEntry e)
    {
        switch (e.Name.ToUpperInvariant())
        {
            case "PLMB-WC-FS":   DrawWcFloorStanding(tr, btr, e.WidthMm, e.DepthMm); break;
            case "PLMB-WC-WH":   DrawWcWallHung(tr, btr, e.WidthMm, e.DepthMm); break;
            case "PLMB-WC-BID":  DrawWcBidetCombo(tr, btr, e.WidthMm, e.DepthMm); break;
            case "PLMB-WC-ACC":  DrawWcAccessible(tr, btr, e.WidthMm, e.DepthMm); break;
            case "PLMB-BSN-STD": DrawBasin(tr, btr, e.WidthMm, e.DepthMm, accessible: false); break;
            case "PLMB-BSN-DBL": DrawBasinDouble(tr, btr, e.WidthMm, e.DepthMm); break;
            case "PLMB-UR-STD":  DrawUrinal(tr, btr, e.WidthMm, e.DepthMm, accessible: false); break;
            case "PLMB-UR-ACC":  DrawUrinal(tr, btr, e.WidthMm, e.DepthMm, accessible: true); break;
            default:
                throw new InvalidOperationException($"No factory registered for fixed plumbing block '{e.Name}'.");
        }
    }

    private static void BuildSizedBlock(Transaction tr, BlockTableRecord btr, string family, double w, double d)
    {
        switch (family.ToUpperInvariant())
        {
            case "PLMB-BSN-ACC":     DrawBasin(tr, btr, w, d, accessible: true); break;
            case "PLMB-SHW-SQ":      DrawShower(tr, btr, w, d, walkIn: false); break;
            case "PLMB-SHW-WI":      DrawShower(tr, btr, w, d, walkIn: true); break;
            case "PLMB-BT-STANDARD": DrawBathtub(tr, btr, w, d, cornerTub: false); break;
            case "PLMB-BT-MINI":     DrawBathtub(tr, btr, w, d, cornerTub: false); break;
            case "PLMB-BT-CORNER":   DrawBathtub(tr, btr, w, d, cornerTub: true); break;
            default:
                throw new ArgumentException($"Unknown plumbing sized-family '{family}'.");
        }
    }

    // ─────────── primitives ───────────

    private static Polyline Rectangle(double w, double d)
    {
        var pl = new Polyline();
        double hw = w / 2.0, hd = d / 2.0;
        pl.AddVertexAt(0, new Point2d(-hw, -hd), 0, 0, 0);
        pl.AddVertexAt(1, new Point2d( hw, -hd), 0, 0, 0);
        pl.AddVertexAt(2, new Point2d( hw,  hd), 0, 0, 0);
        pl.AddVertexAt(3, new Point2d(-hw,  hd), 0, 0, 0);
        pl.Closed = true;
        return pl;
    }

    private static Line Seg(double x1, double y1, double x2, double y2) =>
        new(new Point3d(x1, y1, 0), new Point3d(x2, y2, 0));

    private static void Append(Transaction tr, BlockTableRecord btr, params Entity[] ents)
    {
        foreach (var e in ents)
        {
            btr.AppendEntity(e);
            tr.AddNewlyCreatedDBObject(e, true);
        }
    }

    private static void AddStandardAttributes(Transaction tr, BlockTableRecord btr, double wMm, double dMm, bool accessible)
    {
        double h = Math.Min(wMm, dMm) * 0.12;
        if (h < 60) h = 60;
        if (h > 200) h = 200;

        void Attr(string tag, string prompt, string def, double y, bool invisible)
        {
            var ad = new AttributeDefinition
            {
                Tag = tag,
                Prompt = prompt,
                TextString = def,
                Height = h * 0.8,
                Position = new Point3d(0, y, 0),
                HorizontalMode = TextHorizontalMode.TextCenter,
                VerticalMode = TextVerticalMode.TextVerticalMid,
                AlignmentPoint = new Point3d(0, y, 0),
                Invisible = invisible,
            };
            btr.AppendEntity(ad);
            tr.AddNewlyCreatedDBObject(ad, true);
        }

        double topY = dMm / 2.0;
        Attr("INV_ID",     "Inventory ID", "—", topY + h * 1.2, invisible: false);
        Attr("TYPE",       "Type",         btr.Name, 0, invisible: true);
        Attr("ACCESSIBLE", "Accessible",   accessible ? "TRUE" : "FALSE", 0, invisible: true);
        Attr("NOTE",       "Note",         "", 0, invisible: true);
    }

    // ─────────── WC factories ───────────

    private static void DrawWcFloorStanding(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        // Tank at rear (top), bowl at front (bottom). Origin centred.
        // Tank rectangle (rear 150mm)
        double tankDepth = 180;
        var tank = Rectangle(w, tankDepth);
        tank.TransformBy(Matrix3d.Displacement(new Vector3d(0, d / 2.0 - tankDepth / 2.0, 0)));
        Append(tr, btr, tank);
        // Bowl: oval approximated by 4-vertex polyline + arc at front
        var bowl = Rectangle(w * 0.85, d - tankDepth - 20);
        bowl.TransformBy(Matrix3d.Displacement(new Vector3d(0, -(tankDepth + 20) / 2.0, 0)));
        Append(tr, btr, bowl);
        // flush arc inside bowl
        Append(tr, btr, new Circle(new Point3d(0, -(tankDepth / 2.0) - 80, 0), Vector3d.ZAxis, w * 0.25));
    }

    private static void DrawWcWallHung(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        // No tank (concealed in wall). Only bowl.
        var bowl = Rectangle(w * 0.9, d);
        Append(tr, btr, bowl);
        // flush arc
        Append(tr, btr, new Circle(new Point3d(0, d / 2.0 - 80, 0), Vector3d.ZAxis, w * 0.25));
        // wall-indicator line at rear
        Append(tr, btr, Seg(-w / 2.0, d / 2.0, w / 2.0, d / 2.0));
    }

    private static void DrawWcBidetCombo(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        DrawWcFloorStanding(tr, btr, w, d);
        // bidet spray indicator (small circle inside bowl)
        Append(tr, btr, new Circle(new Point3d(0, -d * 0.15, 0), Vector3d.ZAxis, 30));
    }

    private static void DrawWcAccessible(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        // 800x800 footprint = WC + grab bars + 1500mm accessible approach square (indicated via dashed)
        // WC at rear (top)
        double wcW = 400, wcD = 650;
        var wc = Rectangle(wcW, wcD);
        wc.TransformBy(Matrix3d.Displacement(new Vector3d(0, d / 2.0 - wcD / 2.0, 0)));
        Append(tr, btr, wc);
        // tank at rear of the WC
        var tank = Rectangle(wcW, 160);
        tank.TransformBy(Matrix3d.Displacement(new Vector3d(0, d / 2.0 - 80, 0)));
        Append(tr, btr, tank);
        // grab bars (left + right, L-shape) drawn as line segments
        double barLenSide = 700;
        double barLenFront = 600;
        // LEFT side bar (wall-mounted, running from wall to front then forward)
        Append(tr, btr,
            Seg(-wcW / 2.0 - 20, d / 2.0, -wcW / 2.0 - 20, d / 2.0 - barLenSide),
            Seg(-wcW / 2.0 - 20, d / 2.0 - barLenSide, -wcW / 2.0 - 20 + barLenFront, d / 2.0 - barLenSide));
        // LEFT side bar (drop-down / folding)
        Append(tr, btr,
            Seg(wcW / 2.0 + 20, d / 2.0, wcW / 2.0 + 20, d / 2.0 - barLenSide),
            Seg(wcW / 2.0 + 20, d / 2.0 - barLenSide, wcW / 2.0 + 20 - barLenFront, d / 2.0 - barLenSide));
        // outer footprint box
        Append(tr, btr, Rectangle(w, d));
    }

    // ─────────── Basin factories ───────────

    private static void DrawBasin(Transaction tr, BlockTableRecord btr, double w, double d, bool accessible)
    {
        Append(tr, btr, Rectangle(w, d));
        // bowl indicator: inset rectangle
        var bowl = Rectangle(w * 0.85, d * 0.7);
        Append(tr, btr, bowl);
        // faucet circle at rear centre
        Append(tr, btr, new Circle(new Point3d(0, d / 2.0 - 40, 0), Vector3d.ZAxis, 20));
        // drain circle at centre
        Append(tr, btr, new Circle(Point3d.Origin, Vector3d.ZAxis, 15));
        if (accessible)
        {
            // knee-clearance marker: dashed-style double line under the basin
            Append(tr, btr, Seg(-w / 2.0 + 80, -d / 2.0 - 60, w / 2.0 - 80, -d / 2.0 - 60));
            Append(tr, btr, Seg(-w / 2.0 + 80, -d / 2.0 - 100, w / 2.0 - 80, -d / 2.0 - 100));
        }
    }

    private static void DrawBasinDouble(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // divider
        Append(tr, btr, Seg(0, -d / 2.0, 0, d / 2.0));
        // two bowls
        var leftBowl = Rectangle(w * 0.4, d * 0.7);
        leftBowl.TransformBy(Matrix3d.Displacement(new Vector3d(-w / 4.0, 0, 0)));
        Append(tr, btr, leftBowl);
        var rightBowl = Rectangle(w * 0.4, d * 0.7);
        rightBowl.TransformBy(Matrix3d.Displacement(new Vector3d(w / 4.0, 0, 0)));
        Append(tr, btr, rightBowl);
        Append(tr, btr, new Circle(new Point3d(-w / 4.0, d / 2.0 - 40, 0), Vector3d.ZAxis, 20));
        Append(tr, btr, new Circle(new Point3d( w / 4.0, d / 2.0 - 40, 0), Vector3d.ZAxis, 20));
    }

    // ─────────── Shower factories ───────────

    private static void DrawShower(Transaction tr, BlockTableRecord btr, double w, double d, bool walkIn)
    {
        Append(tr, btr, Rectangle(w, d));
        // drain at centre
        Append(tr, btr, new Circle(Point3d.Origin, Vector3d.ZAxis, 50));
        Append(tr, btr, new Circle(Point3d.Origin, Vector3d.ZAxis, 30));
        if (walkIn)
        {
            // curtain indicator: wavy line along front
            double amp = 40, len = w;
            var wave = new Polyline();
            int seg = 8;
            for (int i = 0; i <= seg; i++)
            {
                double x = -len / 2.0 + i * (len / seg);
                double y = -d / 2.0 + (i % 2 == 0 ? amp : -amp);
                wave.AddVertexAt(i, new Point2d(x, y), 0, 0, 0);
            }
            Append(tr, btr, wave);
        }
        else
        {
            // drain slope arrows (four diagonals toward centre)
            Append(tr, btr,
                Seg(-w / 2.0 + 80, -d / 2.0 + 80, -80, -80),
                Seg( w / 2.0 - 80, -d / 2.0 + 80,  80, -80),
                Seg(-w / 2.0 + 80,  d / 2.0 - 80, -80,  80),
                Seg( w / 2.0 - 80,  d / 2.0 - 80,  80,  80));
        }
    }

    // ─────────── Bathtub factories ───────────

    private static void DrawBathtub(Transaction tr, BlockTableRecord btr, double w, double d, bool cornerTub)
    {
        if (cornerTub)
        {
            // quarter-round bathtub: origin at centre of the square footprint; arc is at the far corner
            Append(tr, btr, Rectangle(w, d));
            Append(tr, btr, new Arc(new Point3d(w / 2.0, d / 2.0, 0), Math.Min(w, d) * 0.9, Math.PI, 1.5 * Math.PI));
            // drain at centre
            Append(tr, btr, new Circle(Point3d.Origin, Vector3d.ZAxis, 30));
            return;
        }
        // standard/mini rectangular bathtub
        Append(tr, btr, Rectangle(w, d));
        // inner rim indicator
        Append(tr, btr, Rectangle(w - 160, d - 100));
        // drain at one end (foot end)
        Append(tr, btr, new Circle(new Point3d(-w / 2.0 + 200, 0, 0), Vector3d.ZAxis, 30));
        // faucet at head end
        Append(tr, btr, new Circle(new Point3d( w / 2.0 - 100, 0, 0), Vector3d.ZAxis, 20));
    }

    // ─────────── Urinal factory ───────────

    private static void DrawUrinal(Transaction tr, BlockTableRecord btr, double w, double d, bool accessible)
    {
        Append(tr, btr, Rectangle(w, d));
        // bowl inset
        var bowl = Rectangle(w * 0.8, d * 0.7);
        bowl.TransformBy(Matrix3d.Displacement(new Vector3d(0, -d * 0.05, 0)));
        Append(tr, btr, bowl);
        // drain
        Append(tr, btr, new Circle(new Point3d(0, -d * 0.3, 0), Vector3d.ZAxis, 12));
        // wall line at rear
        Append(tr, btr, Seg(-w / 2.0, d / 2.0, w / 2.0, d / 2.0));
        if (accessible)
        {
            // accessibility tick (triangle on the right, like wheelchair symbol hint)
            Append(tr, btr, Seg(w / 2.0 + 20, d / 2.0 - 50, w / 2.0 + 60, d / 2.0 - 50));
            Append(tr, btr, Seg(w / 2.0 + 20, d / 2.0 - 50, w / 2.0 + 40, d / 2.0 - 10));
        }
    }

    // ─────────── BlockReference insertion ───────────

    private static (EntityHandle H, string Name, bool Created, double W, double D, bool Accessible) InsertBlockCore(
        Database db, Transaction tr, string name, Point2dDto pos, double rotDeg,
        double sx, double sy, string? layer, Dictionary<string, string>? attrs,
        double? sizedW = null, double? sizedD = null)
    {
        var (btrId, created, w, d, acc) = EnsureBlock(db, tr, name, sizedW, sizedD);
        var br = new BlockReference(AcadEnv.ToPoint3d(pos), btrId)
        {
            ScaleFactors = new Scale3d(sx, sy, 1.0),
            Rotation = rotDeg * Math.PI / 180.0,
        };
        var effectiveLayer = string.IsNullOrWhiteSpace(layer) ? DefaultLayerFor(name) : layer;
        var handle = AcadEnv.Persist(db, tr, br, effectiveLayer);

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
                    if (attrs is not null && attrs.TryGetValue(def.Tag, out var v))
                        ar.TextString = v ?? "";
                    br.AttributeCollection.AppendAttribute(ar);
                    tr.AddNewlyCreatedDBObject(ar, true);
                }
            }
        }

        return (handle, name, created, w, d, acc);
    }

    private static Dictionary<string, string> Attrs(string? invId, string? type, bool? accessible = null)
    {
        var a = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(invId)) a["INV_ID"] = invId!;
        if (!string.IsNullOrWhiteSpace(type))  a["TYPE"]   = type!;
        if (accessible is not null)            a["ACCESSIBLE"] = accessible.Value ? "TRUE" : "FALSE";
        return a;
    }

    // ─────────── handlers ───────────

    private static Task<ToolDispatchResult> InsertPlumbing(JsonObject args, CancellationToken ct) =>
        RunW("acad.plumbing.insert_plumbing", args, ct, (doc, db, tr) =>
        {
            var a = Read<PlumbInsertGenericDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name))
                throw new ArgumentException("insert_plumbing: 'name' is required.");
            var r = InsertBlockCore(db, tr, a.Name, a.Position, a.RotationDeg, a.ScaleX, a.ScaleY, a.Layer, a.Attributes);
            return Wrap(new { entity = r.H, blockName = r.Name, created = r.Created, widthMm = r.W, depthMm = r.D, accessible = r.Accessible });
        });

    private static Task<ToolDispatchResult> InsertWc(JsonObject args, CancellationToken ct) =>
        RunW("acad.plumbing.insert_wc", args, ct, (doc, db, tr) =>
        {
            var a = Read<PlumbInsertWcDto>(args);
            var name = a.Accessible
                ? "PLMB-WC-ACC"
                : (a.Type ?? "floor-standing").ToLowerInvariant() switch
                {
                    "wall-hung"   => "PLMB-WC-WH",
                    "bidet-combo" => "PLMB-WC-BID",
                    _             => "PLMB-WC-FS",
                };
            var attrs = Attrs(a.InvId, a.Type, a.Accessible);
            var r = InsertBlockCore(db, tr, name, a.Position, a.RotationDeg, 1.0, 1.0, a.Layer, attrs);
            return Wrap(new { entity = r.H, blockName = r.Name, created = r.Created, widthMm = r.W, depthMm = r.D, accessible = r.Accessible });
        });

    private static Task<ToolDispatchResult> InsertBasin(JsonObject args, CancellationToken ct) =>
        RunW("acad.plumbing.insert_basin", args, ct, (doc, db, tr) =>
        {
            var a = Read<PlumbInsertBasinDto>(args);
            string name;
            double? sw = null, sd = null;
            if (a.Accessible)
            {
                double wUsed = a.WidthMm > 0 ? a.WidthMm : 700.0;
                double dUsed = 550.0;
                name = NameForSized("PLMB-BSN-ACC", wUsed, dUsed);
                sw = wUsed; sd = dUsed;
            }
            else if (string.Equals(a.Type, "double", StringComparison.OrdinalIgnoreCase))
                name = "PLMB-BSN-DBL";
            else
                name = "PLMB-BSN-STD";

            var attrs = Attrs(a.InvId, a.Type, a.Accessible);
            var r = InsertBlockCore(db, tr, name, a.Position, a.RotationDeg, 1.0, 1.0, a.Layer, attrs, sw, sd);
            return Wrap(new { entity = r.H, blockName = r.Name, created = r.Created, widthMm = r.W, depthMm = r.D, accessible = r.Accessible });
        });

    private static Task<ToolDispatchResult> InsertShower(JsonObject args, CancellationToken ct) =>
        RunW("acad.plumbing.insert_shower", args, ct, (doc, db, tr) =>
        {
            var a = Read<PlumbInsertShowerDto>(args);
            double w = a.WidthMm, d = a.DepthMm;
            if (string.Equals(a.Shape, "square", StringComparison.OrdinalIgnoreCase) && Math.Abs(w - d) > 1)
                d = w;
            var family = a.WalkIn ? "PLMB-SHW-WI" : "PLMB-SHW-SQ";
            var name = NameForSized(family, w, d);
            var attrs = Attrs(a.InvId, a.WalkIn ? "walk-in" : a.Shape, a.WalkIn);
            var r = InsertBlockCore(db, tr, name, a.Position, a.RotationDeg, 1.0, 1.0, a.Layer, attrs, w, d);
            return Wrap(new { entity = r.H, blockName = r.Name, created = r.Created, widthMm = r.W, depthMm = r.D, accessible = r.Accessible });
        });

    private static Task<ToolDispatchResult> InsertBathtub(JsonObject args, CancellationToken ct) =>
        RunW("acad.plumbing.insert_bathtub", args, ct, (doc, db, tr) =>
        {
            var a = Read<PlumbInsertBathtubDto>(args);
            var family = (a.Type ?? "standard").ToLowerInvariant() switch
            {
                "mini"   => "PLMB-BT-MINI",
                "corner" => "PLMB-BT-CORNER",
                _        => "PLMB-BT-STANDARD",
            };
            var name = NameForSized(family, a.WidthMm, a.DepthMm);
            var attrs = Attrs(a.InvId, a.Type);
            var r = InsertBlockCore(db, tr, name, a.Position, a.RotationDeg, 1.0, 1.0, a.Layer, attrs, a.WidthMm, a.DepthMm);
            return Wrap(new { entity = r.H, blockName = r.Name, created = r.Created, widthMm = r.W, depthMm = r.D, accessible = r.Accessible });
        });

    private static Task<ToolDispatchResult> InsertUrinal(JsonObject args, CancellationToken ct) =>
        RunW("acad.plumbing.insert_urinal", args, ct, (doc, db, tr) =>
        {
            var a = Read<PlumbInsertUrinalDto>(args);
            var name = a.Accessible ? "PLMB-UR-ACC" : "PLMB-UR-STD";
            var attrs = Attrs(a.InvId, a.Accessible ? "accessible" : "standard", a.Accessible);
            var r = InsertBlockCore(db, tr, name, a.Position, a.RotationDeg, 1.0, 1.0, a.Layer, attrs);
            return Wrap(new { entity = r.H, blockName = r.Name, created = r.Created, widthMm = r.W, depthMm = r.D, accessible = r.Accessible });
        });

    // ─────────── populate_bathroom ───────────

    private static Task<ToolDispatchResult> PopulateBathroom(JsonObject args, CancellationToken ct) =>
        RunW("acad.plumbing.populate_bathroom", args, ct, (doc, db, tr) =>
        {
            var a = Read<PlumbPopulateDto>(args);
            var (min, max) = ResolveRoomBbox(db, tr, a);
            var plan = BuildBathroomPlan(a.Preset, a.Accessible, min, max, a.Orientation);

            var handles = new List<string>();
            var items = new List<string>();
            var warnings = new List<string>(plan.Warnings);

            foreach (var p in plan.Items)
            {
                try
                {
                    var attrs = Attrs(null, p.Type, p.Accessible);
                    var r = InsertBlockCore(db, tr, p.BlockName, new Point2dDto(p.X, p.Y),
                        p.RotationDeg, 1.0, 1.0, a.Layer, attrs, p.SizedW, p.SizedD);
                    handles.Add(r.H.Handle);
                    items.Add(p.BlockName);
                }
                catch (Exception ex)
                {
                    warnings.Add($"{p.BlockName} @ ({p.X:F0},{p.Y:F0}): {ex.Message}");
                }
            }

            return Wrap(new
            {
                preset = a.Preset,
                accessible = a.Accessible,
                inserted = handles.Count,
                handles = handles,
                items = items,
                warnings = warnings
            });
        });

    private static (Point2d Min, Point2d Max) ResolveRoomBbox(Database db, Transaction tr, PlumbPopulateDto a)
    {
        if (!string.IsNullOrWhiteSpace(a.RoomBoundaryHandle))
        {
            var id = AcadEnv.ResolveHandle(db, a.RoomBoundaryHandle!);
            var ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
            var e = ent.GeometricExtents;
            return (new Point2d(e.MinPoint.X, e.MinPoint.Y), new Point2d(e.MaxPoint.X, e.MaxPoint.Y));
        }
        if (a.BboxMin is not null && a.BboxMax is not null)
            return (new Point2d(a.BboxMin.X, a.BboxMin.Y), new Point2d(a.BboxMax.X, a.BboxMax.Y));
        throw new ArgumentException("populate_bathroom requires roomBoundaryHandle or (bboxMin + bboxMax).");
    }

    private sealed record BathItem(string BlockName, double X, double Y, double RotationDeg, string Type, bool Accessible, double? SizedW = null, double? SizedD = null);
    private sealed record BathPlan(IReadOnlyList<BathItem> Items, IReadOnlyList<string> Warnings);

    private static BathPlan BuildBathroomPlan(string preset, bool accessible, Point2d min, Point2d max, string orientation)
    {
        var items = new List<BathItem>();
        var warnings = new List<string>();
        double w = max.X - min.X, h = max.Y - min.Y;
        double cx = (min.X + max.X) / 2.0, cy = (min.Y + max.Y) / 2.0;

        double rot = orientation.ToLowerInvariant() switch
        {
            "east"  => 90, "south" => 180, "west"  => 270, _ => 0,
        };

        switch ((preset ?? "wc-public").ToLowerInvariant())
        {
            case "wc-public":
                if (w < 1200 || h < 1400) warnings.Add($"Room {w:F0}x{h:F0}mm tight for WC+basin public preset (min 1200x1400).");
                items.Add(new(accessible ? "PLMB-WC-ACC" : "PLMB-WC-FS", min.X + 450, min.Y + 400, 0, "wc", accessible));
                items.Add(new(accessible ? NameForSized("PLMB-BSN-ACC", 700, 550) : "PLMB-BSN-STD", max.X - 400, min.Y + 300, 270, "basin", accessible, 700, 550));
                break;

            case "wc-accessible":
                if (w < 1500 || h < 1800) warnings.Add($"Accessible WC needs min 1500x1800 (PN-EN 17210 T.1); got {w:F0}x{h:F0}.");
                items.Add(new("PLMB-WC-ACC", cx, cy + h * 0.25, 0, "wc", true));
                items.Add(new(NameForSized("PLMB-BSN-ACC", 700, 550), max.X - 400, cy - h * 0.2, 270, "basin", true, 700, 550));
                break;

            case "bathroom-residential":
                if (w < 1600 || h < 2200) warnings.Add($"Residential bathroom needs min 1600x2200 (WT-2019 §82); got {w:F0}x{h:F0}.");
                items.Add(new("PLMB-WC-FS", min.X + 400, min.Y + 400, 0, "wc", false));
                items.Add(new("PLMB-BSN-STD", min.X + 400, cy + h * 0.15, 0, "basin", false));
                // bathtub if room >= 1800, else shower 900x900
                if (w >= 1800)
                    items.Add(new(NameForSized("PLMB-BT-STANDARD", 1700, 700), max.X - 400, cy, 270, "standard", false, 1700, 700));
                else
                    items.Add(new(NameForSized("PLMB-SHW-SQ", 900, 900), max.X - 500, max.Y - 500, 0, "square", false, 900, 900));
                break;

            case "bathroom-hospital-patient":
                if (w < 2400 || h < 2400) warnings.Add($"Hospital patient bathroom needs min 2400x2400; got {w:F0}x{h:F0}.");
                items.Add(new("PLMB-WC-WH", min.X + 350, cy + h * 0.2, 0, "wall-hung", false));
                items.Add(new(NameForSized("PLMB-BSN-ACC", 700, 550), min.X + 400, cy - h * 0.2, 0, "accessible", true, 700, 550));
                items.Add(new(NameForSized("PLMB-SHW-WI", 1200, 900), max.X - 700, cy, 270, "walk-in", true, 1200, 900));
                break;

            case "shower-room":
                items.Add(new(NameForSized("PLMB-SHW-SQ", 900, 900), min.X + 500, cy, 0, "square", false, 900, 900));
                items.Add(new("PLMB-BSN-STD", max.X - 400, cy, 270, "basin", false));
                break;

            case "wc-block-staff":
                if (w < 3200) warnings.Add($"Staff WC-block prefers w >= 3200; got {w:F0}.");
                items.Add(new("PLMB-WC-FS", min.X + 450, min.Y + 400, 0, "wc", false));
                items.Add(new("PLMB-WC-FS", cx, min.Y + 400, 0, "wc", false));
                items.Add(new("PLMB-BSN-STD", min.X + 450, max.Y - 300, 180, "basin", false));
                items.Add(new("PLMB-BSN-STD", cx, max.Y - 300, 180, "basin", false));
                items.Add(new("PLMB-UR-STD", max.X - 300, cy, 270, "urinal", false));
                break;

            default:
                warnings.Add($"Unknown preset '{preset}' — no items placed. Valid: wc-public / wc-accessible / bathroom-residential / bathroom-hospital-patient / shower-room / wc-block-staff.");
                break;
        }

        if (rot != 0)
        {
            var rotated = new List<BathItem>(items.Count);
            double rad = rot * Math.PI / 180.0, cos = Math.Cos(rad), sin = Math.Sin(rad);
            foreach (var it in items)
            {
                double dx = it.X - cx, dy = it.Y - cy;
                double nx = cx + dx * cos - dy * sin, ny = cy + dx * sin + dy * cos;
                rotated.Add(it with { X = nx, Y = ny, RotationDeg = it.RotationDeg + rot });
            }
            items = rotated;
        }

        return new BathPlan(items, warnings);
    }

    // ─────────── list_plumbing_in_model ───────────

    private static Task<ToolDispatchResult> ListPlumbingInModel(JsonObject args, CancellationToken ct) =>
        RunR("acad.plumbing.list_plumbing_in_model", args, ct, (doc, db, tr) =>
        {
            var a = Read<PlumbListInModelDto>(args);
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var refs = new List<object>();
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                if (ent is not BlockReference br) continue;

                string bn;
                try
                {
                    var btr = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                    bn = btr.Name;
                }
                catch { continue; }
                if (!bn.StartsWith("PLMB-", StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrWhiteSpace(a.BlockFilter) && !bn.Equals(a.BlockFilter, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrWhiteSpace(a.LayerFilter) && !br.Layer.Equals(a.LayerFilter, StringComparison.OrdinalIgnoreCase)) continue;

                string? invId = null, type = null;
                bool accessible = false;
                foreach (ObjectId aid in br.AttributeCollection)
                {
                    var ar = (AttributeReference)tr.GetObject(aid, OpenMode.ForRead);
                    switch (ar.Tag.ToUpperInvariant())
                    {
                        case "INV_ID":    invId = ar.TextString; break;
                        case "TYPE":      type  = ar.TextString; break;
                        case "ACCESSIBLE": accessible = string.Equals(ar.TextString, "TRUE", StringComparison.OrdinalIgnoreCase); break;
                    }
                }

                refs.Add(new
                {
                    handle = br.Handle.ToString(),
                    blockName = bn,
                    layer = br.Layer,
                    position = new Point2dDto(br.Position.X, br.Position.Y),
                    rotationDeg = br.Rotation * 180.0 / Math.PI,
                    invId = invId,
                    type = type,
                    accessible = accessible,
                });
            }
            return Wrap(new { references = refs, count = refs.Count });
        });
}
