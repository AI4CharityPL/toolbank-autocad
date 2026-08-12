// AutoCAD plugin handlers for acad-furniture. Ten tools over a parametric
// block-factory registry.
//
// Architecture:
//   * s_catalog           — static metadata listing for list_furniture_catalog
//   * s_fixedFactories    — factories keyed by fully-qualified block name
//                           (FURN-BED-STD, FURN-CHAIR-OFF, etc.)
//   * s_sizedFactories    — factories keyed by family (FURN-DESK-OFF, FURN-CBT-STR,
//                           FURN-TBL-RECT, …) that receive (width, depth) at call
//                           time and produce a BTR named "<family>-<W>-<D>".
//
// Block origin is at the geometric centre of the footprint so rotations spin
// around the centre and placement is predictable. Every block carries three
// attribute definitions: INV_ID, TYPE, ROOM (invisible / editable); plus an
// optional NOTE depending on block. See rule 64-furniture-density-per-room.md
// and rule 28 (block traps) for naming / symbol-safety requirements.
//
// Rules: 10, 11, 12, 19, 28, 64.

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
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

namespace AcadMcp.Plugin.Tools;

internal static class FurniturePluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.furniture.list_furniture_catalog",  ListCatalog);
        host.Register("acad.furniture.insert_furniture",        InsertFurniture);
        host.Register("acad.furniture.insert_bed",              InsertBed);
        host.Register("acad.furniture.insert_chair",            InsertChair);
        host.Register("acad.furniture.insert_desk",             InsertDesk);
        host.Register("acad.furniture.insert_cabinet",          InsertCabinet);
        host.Register("acad.furniture.insert_sofa",             InsertSofa);
        host.Register("acad.furniture.insert_table",            InsertTable);
        host.Register("acad.furniture.populate_room",           PopulateRoom);
        host.Register("acad.furniture.list_furniture_in_model", ListFurnitureInModel);
    }

    // ─────────── shared helpers ───────────

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
        if (blockName.StartsWith("FURN-BED",   StringComparison.OrdinalIgnoreCase)) return "A-FURN-BED";
        if (blockName.StartsWith("FURN-CHAIR", StringComparison.OrdinalIgnoreCase)) return "A-FURN-CHR";
        if (blockName.StartsWith("FURN-DESK",  StringComparison.OrdinalIgnoreCase)) return "A-FURN-DSK";
        if (blockName.StartsWith("FURN-CBT",   StringComparison.OrdinalIgnoreCase)) return "A-FURN-CBT";
        if (blockName.StartsWith("FURN-SOFA",  StringComparison.OrdinalIgnoreCase)) return "A-FURN-SFA";
        if (blockName.StartsWith("FURN-TBL",   StringComparison.OrdinalIgnoreCase)) return "A-FURN-TBL";
        return "A-FURN";
    }

    // ─────────── catalog metadata ───────────

    // The catalogue itself lives in AcadMcp.Shared.Catalogs.FurnitureCatalog, which has no
    // AutoCAD dependency and is therefore reachable from tests CI can run. What stays here
    // is the half that genuinely needs AutoCAD: turning a resolution into geometry.
    // CatalogContractTests holds the two halves to their contract.

    // ─────────── catalog listing ───────────

    private static Task<ToolDispatchResult> ListCatalog(JsonObject args, CancellationToken ct) =>
        RunR("acad.furniture.list_furniture_catalog", args, ct, (doc, db, tr) =>
        {
            var a = Read<FurnitureListCatalogArgsDto>(args);
            var list = FurnitureCatalog.All(a.CategoryFilter, a.DomainFilter);

            return Wrap(new { entries = list, count = list.Count });
        });

    // ─────────── block factory dispatch ───────────

    /// <summary>
    /// Ensure a BlockTableRecord with the given name exists in the database.
    /// Returns (blockTableRecordId, created flag, actual width, actual depth in mm).
    /// </summary>
    private static (ObjectId BtrId, bool Created, double WMm, double DMm) EnsureBlock(
        Database db, Transaction tr, string name, double? sizedW, double? sizedD)
    {
        AcadEnv.ValidateSymbolName(name, "Block");
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        if (bt.Has(name))
        {
            return (bt[name], false, sizedW ?? 0, sizedD ?? 0);
        }

        bt.UpgradeOpen();
        var btr = new BlockTableRecord { Name = name };
        var btrId = bt.Add(btr);
        tr.AddNewlyCreatedDBObject(btr, true);

        var dims = BuildBlockGeometry(db, tr, btr, name, sizedW, sizedD);
        AddStandardAttributes(db, tr, btr, dims.W, dims.D);
        return (btrId, true, dims.W, dims.D);
    }

    private static (double W, double D) BuildBlockGeometry(
        Database db, Transaction tr, BlockTableRecord btr, string name, double? sizedW, double? sizedD)
    {
        // One decision, made in one place, testable without AutoCAD. Everything below it is
        // dispatch. All three name forms - fixed entry, bare family, family with a -W-D
        // suffix - are resolved by the shared catalogue, so a name the listing publishes
        // cannot be rejected here without CatalogContractTests going red.
        var r = FurnitureCatalog.Resolve(name, sizedW, sizedD);

        if (r.Match == CatalogMatch.Fixed)
        {
            BuildFixedBlock(tr, btr, r.Entry);
            return (r.WidthMm, r.DepthMm);
        }

        BuildSizedBlock(tr, btr, r.Family, r.WidthMm, r.DepthMm);
        return (r.WidthMm, r.DepthMm);
    }

    private static void BuildFixedBlock(Transaction tr, BlockTableRecord btr, FurnitureCatalogEntry e)
    {
        switch (e.Name.ToUpperInvariant())
        {
            // beds
            case "FURN-BED-STD":    DrawBedStandard(tr, btr, e.WidthMm, e.DepthMm); break;
            case "FURN-BED-ICU":    DrawBedIcu(tr, btr, e.WidthMm, e.DepthMm); break;
            case "FURN-BED-BARIAT": DrawBedBariatric(tr, btr, e.WidthMm, e.DepthMm); break;
            case "FURN-BED-PED":    DrawBedPediatric(tr, btr, e.WidthMm, e.DepthMm); break;
            case "FURN-BED-OR":     DrawBedOr(tr, btr, e.WidthMm, e.DepthMm); break;
            case "FURN-BED-LBR":    DrawBedLabour(tr, btr, e.WidthMm, e.DepthMm); break;
            // chairs
            case "FURN-CHAIR-OFF":  DrawChairOffice(tr, btr); break;
            case "FURN-CHAIR-ARM":  DrawChairArmchair(tr, btr, e.WidthMm, e.DepthMm); break;
            case "FURN-CHAIR-STL":  DrawChairStool(tr, btr, Math.Max(e.WidthMm, e.DepthMm) / 2.0); break;
            case "FURN-CHAIR-EXAM": DrawChairExam(tr, btr, Math.Max(e.WidthMm, e.DepthMm) / 2.0); break;
            case "FURN-CHAIR-WHL":  DrawChairWheelchair(tr, btr, e.WidthMm, e.DepthMm); break;
            // sofas
            case "FURN-SOFA-2":     DrawSofa(tr, btr, e.WidthMm, e.DepthMm, seats: 2, clinical: false); break;
            case "FURN-SOFA-3":     DrawSofa(tr, btr, e.WidthMm, e.DepthMm, seats: 3, clinical: false); break;
            case "FURN-SOFA-CLN-2": DrawSofa(tr, btr, e.WidthMm, e.DepthMm, seats: 2, clinical: true); break;
            case "FURN-SOFA-CLN-3": DrawSofa(tr, btr, e.WidthMm, e.DepthMm, seats: 3, clinical: true); break;
            // medical imaging / OR equipment
            case "FURN-EQP-CT":     DrawEquipmentCt(tr, btr, e.WidthMm, e.DepthMm); break;
            case "FURN-EQP-MRI":    DrawEquipmentMri(tr, btr, e.WidthMm, e.DepthMm); break;
            case "FURN-EQP-CARM":   DrawEquipmentCarm(tr, btr, e.WidthMm, e.DepthMm); break;
            case "FURN-EQP-LIGHT":  DrawEquipmentOrLight(tr, btr, e.WidthMm, e.DepthMm); break;
            case "FURN-EQP-CRASH":  DrawEquipmentCrashCart(tr, btr, e.WidthMm, e.DepthMm); break;
            case "FURN-EQP-VENT":   DrawEquipmentVentilator(tr, btr, e.WidthMm, e.DepthMm); break;
            case "FURN-EQP-MON":    DrawEquipmentMonitor(tr, btr, e.WidthMm, e.DepthMm); break;
            // kitchen appliances
            case "FURN-KIT-HOB":    DrawKitchenHob(tr, btr, e.WidthMm, e.DepthMm); break;
            case "FURN-KIT-FRIDGE": DrawKitchenFridge(tr, btr, e.WidthMm, e.DepthMm); break;
            case "FURN-KIT-SINK":   DrawKitchenSink(tr, btr, e.WidthMm, e.DepthMm); break;
            default:
                throw new InvalidOperationException($"No factory registered for fixed block '{e.Name}'.");
        }
    }

    private static void BuildSizedBlock(Transaction tr, BlockTableRecord btr, string family, double wMm, double dMm)
    {
        switch (family.ToUpperInvariant())
        {
            case "FURN-DESK-OFF":  DrawDeskOffice(tr, btr, wMm, dMm); break;
            case "FURN-DESK-RCP":  DrawDeskReception(tr, btr, wMm, dMm); break;
            case "FURN-DESK-NST":  DrawDeskNurseStation(tr, btr, wMm, dMm); break;
            case "FURN-CBT-STR":   DrawCabinetStorage(tr, btr, wMm, dMm); break;
            case "FURN-CBT-MED":   DrawCabinetMedical(tr, btr, wMm, dMm); break;
            case "FURN-CBT-FIL":   DrawCabinetFile(tr, btr, wMm, dMm); break;
            case "FURN-CBT-WDR":   DrawCabinetWardrobe(tr, btr, wMm, dMm); break;
            case "FURN-TBL-RECT":  DrawTableRect(tr, btr, wMm, dMm); break;
            case "FURN-TBL-ROUND": DrawTableRound(tr, btr, Math.Min(wMm, dMm) / 2.0); break;
            case "FURN-TBL-SQ":    DrawTableRect(tr, btr, wMm, wMm); break;
            case "FURN-TBL-EXAM":  DrawTableExam(tr, btr, wMm, dMm); break;
            case "FURN-KIT-COUNTER": DrawKitchenCounter(tr, btr, wMm, dMm); break;
            case "FURN-BED-RES":   DrawBedResidential(tr, btr, wMm, dMm); break;
            case "FURN-CBT-NST":   DrawNightstand(tr, btr, wMm, dMm); break;
            default:
                throw new ArgumentException($"Unknown sized-family '{family}'.");
        }
    }

    // ─────────── primitive helpers (centred at origin) ───────────

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

    private static void AddStandardAttributes(Database db, Transaction tr, BlockTableRecord btr, double wMm, double dMm)
    {
        var labelHeight = Math.Min(wMm, dMm) * 0.12;
        if (labelHeight < 80) labelHeight = 80;
        if (labelHeight > 250) labelHeight = 250;

        void Attr(string tag, string prompt, string def, double y, bool invisible)
        {
            var ad = new AttributeDefinition
            {
                Tag = tag,
                Prompt = prompt,
                TextString = def,
                Height = labelHeight * 0.8,
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
        Attr("INV_ID", "Inventory ID",       "—", topY + labelHeight * 1.2, invisible: false);
        Attr("TYPE",   "Type / variant",      btr.Name, 0, invisible: true);
        Attr("ROOM",   "Room code",           "",  0, invisible: true);
        Attr("NOTE",   "Note",                "",  0, invisible: true);
    }

    // ─────────── BED factories ───────────

    private static void DrawBedStandard(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // pillow strip at head (top)
        double pillowH = d * 0.18;
        Append(tr, btr, Rectangle(w * 0.9, pillowH));
        // translate pillow strip to head end (y = +d/2 - pillowH/2)
        // simpler: draw pillow explicitly
        // (the strip above is centred; leave as visual cue, add a second head-line)
        Append(tr, btr, Seg(-w / 2.0, d / 2.0 - pillowH, w / 2.0, d / 2.0 - pillowH));
        // mattress mid-seam
        Append(tr, btr, Seg(-w / 2.0 + 50, 0, w / 2.0 - 50, 0));
    }

    private static void DrawBedIcu(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // head monitor box (above head end)
        double monW = w * 0.7, monH = 250;
        var mon = Rectangle(monW, monH);
        mon.TransformBy(Matrix3d.Displacement(new Vector3d(0, d / 2.0 + monH / 2.0 + 50, 0)));
        Append(tr, btr, mon);
        // side rails (two long lines offset inward)
        double railIn = 80;
        Append(tr, btr, Seg(-w / 2.0 + railIn, -d / 2.0 + 200, -w / 2.0 + railIn, d / 2.0 - 200));
        Append(tr, btr, Seg( w / 2.0 - railIn, -d / 2.0 + 200,  w / 2.0 - railIn, d / 2.0 - 200));
        // pillow line
        Append(tr, btr, Seg(-w / 2.0, d / 2.0 - d * 0.18, w / 2.0, d / 2.0 - d * 0.18));
    }

    private static void DrawBedBariatric(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        var outer = Rectangle(w, d);
        Append(tr, btr, outer);
        // doubled frame line (reinforced)
        var inner = Rectangle(w - 120, d - 120);
        Append(tr, btr, inner);
        Append(tr, btr, Seg(-w / 2.0, d / 2.0 - d * 0.18, w / 2.0, d / 2.0 - d * 0.18));
    }

    private static void DrawBedPediatric(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // ped rail (saw-tooth hint: just a doubled line at sides)
        Append(tr, btr, Seg(-w / 2.0 + 60, -d / 2.0 + 150, -w / 2.0 + 60, d / 2.0 - 150));
        Append(tr, btr, Seg( w / 2.0 - 60, -d / 2.0 + 150,  w / 2.0 - 60, d / 2.0 - 150));
        Append(tr, btr, Seg(-w / 2.0, d / 2.0 - d * 0.2, w / 2.0, d / 2.0 - d * 0.2));
    }

    private static void DrawBedOr(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        // narrow operating table, trendelenburg split at foot end
        Append(tr, btr, Rectangle(w, d));
        // trendelenburg hinge at y = -d*0.25 (dashed-ish approximation with one line)
        Append(tr, btr, Seg(-w / 2.0, -d * 0.25, w / 2.0, -d * 0.25));
        Append(tr, btr, Seg(-w / 2.0,  d * 0.25, w / 2.0,  d * 0.25));
        // head pad marker
        Append(tr, btr, Seg(-w / 2.0, d / 2.0 - 150, w / 2.0, d / 2.0 - 150));
    }

    private static void DrawBedLabour(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // stirrups: two short arcs at foot end
        double arcR = 200;
        double stirY = -d / 2.0 + 50;
        double stirXL = -w / 2.0 - 250;
        double stirXR =  w / 2.0 + 250;
        Append(tr, btr, new Arc(new Point3d(stirXL, stirY, 0), arcR, 0, Math.PI));
        Append(tr, btr, new Arc(new Point3d(stirXR, stirY, 0), arcR, 0, Math.PI));
        Append(tr, btr, Seg(-w / 2.0, d / 2.0 - d * 0.15, w / 2.0, d / 2.0 - d * 0.15));
    }

    // ─────────── CHAIR factories ───────────

    private static void DrawChairOffice(Transaction tr, BlockTableRecord btr)
    {
        // swivel chair: circular seat 500 ø + 5 star legs
        double r = 250;
        Append(tr, btr, new Circle(Point3d.Origin, Vector3d.ZAxis, r));
        Append(tr, btr, new Circle(Point3d.Origin, Vector3d.ZAxis, 40)); // central pole
        for (int i = 0; i < 5; i++)
        {
            double a = i * (2.0 * Math.PI / 5.0) + Math.PI / 2.0;
            Append(tr, btr, Seg(0, 0, Math.Cos(a) * r * 0.9, Math.Sin(a) * r * 0.9));
        }
        // backrest indicator: arc at top-rear
        var backArc = new Arc(new Point3d(0, -r * 0.2, 0), r * 0.8, Math.PI * 0.25, Math.PI * 0.75);
        Append(tr, btr, backArc);
    }

    private static void DrawChairArmchair(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        // armchair with arms + back
        Append(tr, btr, Rectangle(w, d));
        // backrest line at rear (top)
        Append(tr, btr, Seg(-w / 2.0, d / 2.0 - d * 0.25, w / 2.0, d / 2.0 - d * 0.25));
        // arm indicators (left/right vertical lines at rear third)
        Append(tr, btr, Seg(-w / 2.0 + d * 0.2, d / 2.0 - d * 0.25, -w / 2.0 + d * 0.2, 0));
        Append(tr, btr, Seg( w / 2.0 - d * 0.2, d / 2.0 - d * 0.25,  w / 2.0 - d * 0.2, 0));
    }

    private static void DrawChairStool(Transaction tr, BlockTableRecord btr, double r)
    {
        Append(tr, btr, new Circle(Point3d.Origin, Vector3d.ZAxis, r));
    }

    private static void DrawChairExam(Transaction tr, BlockTableRecord btr, double r)
    {
        Append(tr, btr, new Circle(Point3d.Origin, Vector3d.ZAxis, r));
        Append(tr, btr, new Circle(Point3d.Origin, Vector3d.ZAxis, r * 0.25)); // hydraulic post
        // casters: 5 small ticks
        for (int i = 0; i < 5; i++)
        {
            double a = i * (2.0 * Math.PI / 5.0) + Math.PI / 2.0;
            var p = new Point3d(Math.Cos(a) * r * 0.9, Math.Sin(a) * r * 0.9, 0);
            Append(tr, btr, new Circle(p, Vector3d.ZAxis, 30));
        }
    }

    private static void DrawChairWheelchair(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // two large wheels (main) at centre sides
        double wheelR = d * 0.28;
        Append(tr, btr, new Circle(new Point3d(-w / 2.0 + 20, 0, 0), Vector3d.ZAxis, wheelR));
        Append(tr, btr, new Circle(new Point3d( w / 2.0 - 20, 0, 0), Vector3d.ZAxis, wheelR));
        // castors front
        Append(tr, btr, new Circle(new Point3d(-w / 2.0 + 60, -d / 2.0 + 90, 0), Vector3d.ZAxis, 50));
        Append(tr, btr, new Circle(new Point3d( w / 2.0 - 60, -d / 2.0 + 90, 0), Vector3d.ZAxis, 50));
        // backrest line (rear)
        Append(tr, btr, Seg(-w / 2.0, d / 2.0 - 80, w / 2.0, d / 2.0 - 80));
    }

    // ─────────── SOFA factories ───────────

    private static void DrawSofa(Transaction tr, BlockTableRecord btr, double w, double d, int seats, bool clinical)
    {
        Append(tr, btr, Rectangle(w, d));
        // backrest line (rear)
        double backT = clinical ? d * 0.18 : d * 0.22;
        Append(tr, btr, Seg(-w / 2.0, d / 2.0 - backT, w / 2.0, d / 2.0 - backT));
        // arms (unless clinical)
        if (!clinical)
        {
            Append(tr, btr, Seg(-w / 2.0 + d * 0.15, d / 2.0 - backT, -w / 2.0 + d * 0.15, -d / 2.0));
            Append(tr, btr, Seg( w / 2.0 - d * 0.15, d / 2.0 - backT,  w / 2.0 - d * 0.15, -d / 2.0));
        }
        // seat cushion dividers
        double innerW = clinical ? w : (w - 2 * d * 0.15);
        double xStart = -innerW / 2.0;
        for (int i = 1; i < seats; i++)
        {
            double x = xStart + i * (innerW / seats);
            Append(tr, btr, Seg(x, d / 2.0 - backT, x, -d / 2.0 + d * 0.1));
        }
    }

    // ─────────── DESK factories ───────────

    private static void DrawDeskOffice(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // two drawer lines on right side (1/3 width)
        double drawerW = w / 3.0;
        double xL = w / 2.0 - drawerW;
        Append(tr, btr, Seg(xL, -d / 2.0, xL, d / 2.0));
        Append(tr, btr, Seg(xL, 0, w / 2.0, 0));
    }

    private static void DrawDeskReception(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        // L-counter: main top + front overhang
        Append(tr, btr, Rectangle(w, d));
        // overhang (front) = 400mm extension at y = -d/2 - 200
        double oh = 400;
        var ohPl = new Polyline();
        ohPl.AddVertexAt(0, new Point2d(-w / 2.0 + 200, -d / 2.0), 0, 0, 0);
        ohPl.AddVertexAt(1, new Point2d(-w / 2.0 + 200, -d / 2.0 - oh), 0, 0, 0);
        ohPl.AddVertexAt(2, new Point2d( w / 2.0 - 200, -d / 2.0 - oh), 0, 0, 0);
        ohPl.AddVertexAt(3, new Point2d( w / 2.0 - 200, -d / 2.0), 0, 0, 0);
        Append(tr, btr, ohPl);
    }

    private static void DrawDeskNurseStation(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // raised edge: inner line
        Append(tr, btr, Rectangle(w - 200, d - 200));
    }

    // ─────────── CABINET factories ───────────

    private static void DrawCabinetStorage(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // door swing arc: hinge at bottom-left corner, swing 90° to bottom-right
        double hingeX = -w / 2.0, hingeY = -d / 2.0;
        Append(tr, btr, new Arc(new Point3d(hingeX, hingeY, 0), w, 0, Math.PI / 2.0));
        Append(tr, btr, Seg(hingeX, hingeY, hingeX + w, hingeY));
    }

    private static void DrawCabinetMedical(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // glass-door indicator: diagonals
        Append(tr, btr, Seg(-w / 2.0, -d / 2.0, w / 2.0, d / 2.0));
        Append(tr, btr, Seg(-w / 2.0,  d / 2.0, w / 2.0, -d / 2.0));
    }

    private static void DrawCabinetFile(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // three drawer horizontal lines (inside)
        for (int i = 1; i <= 2; i++)
        {
            double y = -d / 2.0 + i * (d / 3.0);
            Append(tr, btr, Seg(-w / 2.0 + 50, y, w / 2.0 - 50, y));
        }
    }

    private static void DrawCabinetWardrobe(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // hanger rail: single line parallel to front
        Append(tr, btr, Seg(-w / 2.0 + 100, d / 2.0 - 150, w / 2.0 - 100, d / 2.0 - 150));
    }

    // ─────────── TABLE factories ───────────

    private static void DrawTableRect(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
    }

    private static void DrawTableRound(Transaction tr, BlockTableRecord btr, double r)
    {
        Append(tr, btr, new Circle(Point3d.Origin, Vector3d.ZAxis, r));
    }

    private static void DrawTableExam(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // pillow strip at head (top)
        Append(tr, btr, Seg(-w / 2.0, d / 2.0 - d * 0.22, w / 2.0, d / 2.0 - d * 0.22));
        // paper-roll slot at head edge
        Append(tr, btr, Seg(-w / 2.0 + 150, d / 2.0, w / 2.0 - 150, d / 2.0));
    }

    // ─────────── EQUIPMENT factories (2026-08-12, Hospital2026 PRIME) ───────────

    private static void DrawEquipmentCt(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        // gantry housing footprint
        Append(tr, btr, Rectangle(w, d));
        // bore ring, offset toward one end (the end the table enters from)
        double ringR = Math.Min(w, d) * 0.32;
        double ringY = d / 2.0 - ringR - d * 0.08;
        Append(tr, btr, new Circle(new Point3d(0, ringY, 0), Vector3d.ZAxis, ringR));
        Append(tr, btr, new Circle(new Point3d(0, ringY, 0), Vector3d.ZAxis, ringR * 0.55));
        // patient table centreline running through the bore, full depth
        Append(tr, btr, Seg(0, d / 2.0, 0, -d / 2.0));
    }

    private static void DrawEquipmentMri(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        // cryostat housing footprint - double outline (heavier unit than CT)
        Append(tr, btr, Rectangle(w, d));
        Append(tr, btr, Rectangle(w - 150, d - 150));
        // bore, deeper tunnel than CT (longer ring pair) - two concentric circles offset toward one end
        double ringR = Math.Min(w, d) * 0.30;
        double ringY = d / 2.0 - ringR - d * 0.10;
        Append(tr, btr, new Circle(new Point3d(0, ringY, 0), Vector3d.ZAxis, ringR));
        Append(tr, btr, new Circle(new Point3d(0, ringY, 0), Vector3d.ZAxis, ringR * 0.45));
        // patient table centreline
        Append(tr, btr, Seg(0, d / 2.0, 0, -d / 2.0));
    }

    private static void DrawEquipmentCarm(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        // base/pedestal footprint
        Append(tr, btr, Rectangle(w * 0.55, d * 0.55));
        // the C-shaped gantry arc, swept ~270 degrees around the table position
        double armR = Math.Min(w, d) * 0.42;
        Append(tr, btr, new Arc(Point3d.Origin, armR, -Math.PI * 0.75, Math.PI * 0.75));
        Append(tr, btr, new Arc(Point3d.Origin, armR * 0.85, -Math.PI * 0.75, Math.PI * 0.75));
        // patient table through the centre of the C
        Append(tr, btr, Seg(-w / 2.0, 0, w / 2.0, 0));
    }

    private static void DrawEquipmentOrLight(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        // ceiling-mounted light head: concentric rings (lamp face) + boom arm tick
        double r = Math.Min(w, d) / 2.0;
        Append(tr, btr, new Circle(Point3d.Origin, Vector3d.ZAxis, r));
        Append(tr, btr, new Circle(Point3d.Origin, Vector3d.ZAxis, r * 0.6));
        Append(tr, btr, new Circle(Point3d.Origin, Vector3d.ZAxis, r * 0.2));
        Append(tr, btr, Seg(0, 0, r, r)); // boom-arm indicator
    }

    private static void DrawEquipmentCrashCart(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // shelf lines (drawer stack)
        Append(tr, btr, Seg(-w / 2.0, -d / 6.0, w / 2.0, -d / 6.0));
        Append(tr, btr, Seg(-w / 2.0, d / 6.0, w / 2.0, d / 6.0));
        // medical cross marker, centred
        double armL = Math.Min(w, d) * 0.22;
        Append(tr, btr, Seg(-armL, 0, armL, 0));
        Append(tr, btr, Seg(0, -armL, 0, armL));
    }

    private static void DrawEquipmentVentilator(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // breathing-circuit loop indicator (small circle, offset toward the patient-facing edge)
        double r = Math.Min(w, d) * 0.22;
        Append(tr, btr, new Circle(new Point3d(0, d / 2.0 - r - 40, 0), Vector3d.ZAxis, r));
        // mobile stand base cross
        Append(tr, btr, Seg(-w / 2.0 + 40, -d / 2.0 + 40, w / 2.0 - 40, -d / 2.0 + 40));
    }

    private static void DrawEquipmentMonitor(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // waveform trace across the screen (simple zig-zag polyline)
        var trace = new Polyline();
        double hw = w / 2.0 - 40, y0 = d * 0.05;
        trace.AddVertexAt(0, new Point2d(-hw, y0), 0, 0, 0);
        trace.AddVertexAt(1, new Point2d(-hw * 0.4, y0 + d * 0.2), 0, 0, 0);
        trace.AddVertexAt(2, new Point2d(0, y0 - d * 0.2), 0, 0, 0);
        trace.AddVertexAt(3, new Point2d(hw * 0.4, y0 + d * 0.15), 0, 0, 0);
        trace.AddVertexAt(4, new Point2d(hw, y0), 0, 0, 0);
        Append(tr, btr, trace);
        // mobile stand base
        Append(tr, btr, Seg(-w / 2.0 + 40, -d / 2.0 + 40, w / 2.0 - 40, -d / 2.0 + 40));
    }

    // ─────────── KITCHEN factories (2026-08-12, knowledge-base proof-of-concept) ───────────

    private static void DrawKitchenHob(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // 4-burner indicator, one circle per quadrant
        double r = Math.Min(w, d) * 0.16;
        foreach (var (sx, sy) in new[] { (-1.0, -1.0), (1.0, -1.0), (-1.0, 1.0), (1.0, 1.0) })
            Append(tr, btr, new Circle(new Point3d(sx * w * 0.22, sy * d * 0.22, 0), Vector3d.ZAxis, r));
    }

    private static void DrawKitchenFridge(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // door split line (fridge/freezer) two-thirds up
        Append(tr, btr, Seg(-w / 2.0, d * 0.17, w / 2.0, d * 0.17));
        // door swing arc, hinge at one front corner
        Append(tr, btr, new Arc(new Point3d(-w / 2.0, -d / 2.0, 0), w * 0.6, 0, Math.PI / 2.0));
    }

    private static void DrawKitchenSink(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // basin outline, inset rectangle
        Append(tr, btr, Rectangle(w * 0.7, d * 0.55));
        // tap marker
        Append(tr, btr, Seg(0, d / 2.0 - 40, 0, d / 2.0 - 120));
    }

    private static void DrawKitchenCounter(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // cabinet-front division lines every ~600mm along the run
        int bays = Math.Max(1, (int)Math.Round(w / 600.0));
        for (int i = 1; i < bays; i++)
        {
            double x = -w / 2.0 + i * (w / bays);
            Append(tr, btr, Seg(x, -d / 2.0, x, d / 2.0));
        }
    }

    // ─────────── RESIDENTIAL BED / NIGHTSTAND factories ───────────

    private static void DrawBedResidential(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // pillow strip at head (top)
        double pillowH = d * 0.16;
        Append(tr, btr, Seg(-w / 2.0, d / 2.0 - pillowH, w / 2.0, d / 2.0 - pillowH));
        // duvet mid-seam
        Append(tr, btr, Seg(-w / 2.0 + 50, 0, w / 2.0 - 50, 0));
        // headboard line
        Append(tr, btr, Seg(-w / 2.0, d / 2.0, w / 2.0, d / 2.0));
    }

    private static void DrawNightstand(Transaction tr, BlockTableRecord btr, double w, double d)
    {
        Append(tr, btr, Rectangle(w, d));
        // single drawer line
        Append(tr, btr, Seg(-w / 2.0 + 40, 0, w / 2.0 - 40, 0));
    }

    // ─────────── BlockReference insertion + attributes ───────────

    private static (EntityHandle H, string BlockName, bool Created, double W, double D) InsertBlockCore(
        Database db, Transaction tr, string blockName, Point2dDto pos, double rotDeg,
        double sx, double sy, string? layer, Dictionary<string, string>? attrs,
        double? sizedW = null, double? sizedD = null)
    {
        var (btrId, created, w, d) = EnsureBlock(db, tr, blockName, sizedW, sizedD);
        var br = new BlockReference(AcadEnv.ToPoint3d(pos), btrId)
        {
            ScaleFactors = new Scale3d(sx, sy, 1.0),
            Rotation = rotDeg * Math.PI / 180.0,
        };
        var effectiveLayer = string.IsNullOrWhiteSpace(layer) ? DefaultLayerFor(blockName) : layer;
        var handle = AcadEnv.Persist(db, tr, br, effectiveLayer);

        // Materialise attribute definitions into references (rule 28 trap #5).
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

        return (handle, blockName, created, w, d);
    }

    // ─────────── handlers ───────────

    private static Task<ToolDispatchResult> InsertFurniture(JsonObject args, CancellationToken ct) =>
        RunW("acad.furniture.insert_furniture", args, ct, (doc, db, tr) =>
        {
            var a = Read<FurnitureInsertGenericDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name))
                throw new ArgumentException("insert_furniture: 'name' is required.");
            var r = InsertBlockCore(db, tr, a.Name, a.Position, a.RotationDeg, a.ScaleX, a.ScaleY, a.Layer, a.Attributes);
            return Wrap(new { entity = r.H, blockName = r.BlockName, created = r.Created, widthMm = r.W, depthMm = r.D });
        });

    private static Task<ToolDispatchResult> InsertBed(JsonObject args, CancellationToken ct) =>
        RunW("acad.furniture.insert_bed", args, ct, (doc, db, tr) =>
        {
            var a = Read<FurnitureInsertBedDto>(args);
            var name = ResolveBedName(a.Type);
            var attrs = BuildAttrs(a.InvId, a.Type, a.Room);
            var r = InsertBlockCore(db, tr, name, a.Position, a.RotationDeg, 1.0, 1.0, a.Layer, attrs);
            return Wrap(new { entity = r.H, blockName = r.BlockName, created = r.Created, widthMm = r.W, depthMm = r.D });
        });

    private static Task<ToolDispatchResult> InsertChair(JsonObject args, CancellationToken ct) =>
        RunW("acad.furniture.insert_chair", args, ct, (doc, db, tr) =>
        {
            var a = Read<FurnitureInsertChairDto>(args);
            var name = ResolveChairName(a.Type);
            var attrs = BuildAttrs(a.InvId, a.Type, null);
            var r = InsertBlockCore(db, tr, name, a.Position, a.RotationDeg, 1.0, 1.0, a.Layer, attrs);
            return Wrap(new { entity = r.H, blockName = r.BlockName, created = r.Created, widthMm = r.W, depthMm = r.D });
        });

    private static Task<ToolDispatchResult> InsertDesk(JsonObject args, CancellationToken ct) =>
        RunW("acad.furniture.insert_desk", args, ct, (doc, db, tr) =>
        {
            var a = Read<FurnitureInsertDeskDto>(args);
            var family = ResolveDeskFamily(a.Type);
            var name = NameForSized(family, a.WidthMm, a.DepthMm);
            var attrs = BuildAttrs(a.InvId, a.Type, null);
            var r = InsertBlockCore(db, tr, name, a.Position, a.RotationDeg, 1.0, 1.0, a.Layer, attrs, a.WidthMm, a.DepthMm);
            return Wrap(new { entity = r.H, blockName = r.BlockName, created = r.Created, widthMm = r.W, depthMm = r.D });
        });

    private static Task<ToolDispatchResult> InsertCabinet(JsonObject args, CancellationToken ct) =>
        RunW("acad.furniture.insert_cabinet", args, ct, (doc, db, tr) =>
        {
            var a = Read<FurnitureInsertCabinetDto>(args);
            var family = ResolveCabinetFamily(a.Type);
            var name = NameForSized(family, a.WidthMm, a.DepthMm);
            var attrs = BuildAttrs(a.InvId, a.Type, null);
            var r = InsertBlockCore(db, tr, name, a.Position, a.RotationDeg, 1.0, 1.0, a.Layer, attrs, a.WidthMm, a.DepthMm);
            return Wrap(new { entity = r.H, blockName = r.BlockName, created = r.Created, widthMm = r.W, depthMm = r.D });
        });

    private static Task<ToolDispatchResult> InsertSofa(JsonObject args, CancellationToken ct) =>
        RunW("acad.furniture.insert_sofa", args, ct, (doc, db, tr) =>
        {
            var a = Read<FurnitureInsertSofaDto>(args);
            var name = ResolveSofaName(a.Type, a.Seats);
            var attrs = BuildAttrs(a.InvId, a.Type, null);
            var r = InsertBlockCore(db, tr, name, a.Position, a.RotationDeg, 1.0, 1.0, a.Layer, attrs);
            return Wrap(new { entity = r.H, blockName = r.BlockName, created = r.Created, widthMm = r.W, depthMm = r.D });
        });

    private static Task<ToolDispatchResult> InsertTable(JsonObject args, CancellationToken ct) =>
        RunW("acad.furniture.insert_table", args, ct, (doc, db, tr) =>
        {
            var a = Read<FurnitureInsertTableDto>(args);
            var (family, w, d) = ResolveTableFamily(a.Shape, a.Type, a.WidthMm, a.DepthMm);
            var name = NameForSized(family, w, d);
            var attrs = BuildAttrs(a.InvId, a.Type, null);
            var r = InsertBlockCore(db, tr, name, a.Position, a.RotationDeg, 1.0, 1.0, a.Layer, attrs, w, d);
            return Wrap(new { entity = r.H, blockName = r.BlockName, created = r.Created, widthMm = r.W, depthMm = r.D });
        });

    // ─────────── type → block-name resolvers ───────────

    private static string ResolveBedName(string type) => (type ?? "standard").ToLowerInvariant() switch
    {
        "icu"                    => "FURN-BED-ICU",
        "bariatric" or "bariat"  => "FURN-BED-BARIAT",
        "pediatric" or "ped"     => "FURN-BED-PED",
        "or" or "operating"      => "FURN-BED-OR",
        "labour"  or "labor" or "delivery" or "lbr" => "FURN-BED-LBR",
        _                        => "FURN-BED-STD",
    };

    private static string ResolveChairName(string type) => (type ?? "office").ToLowerInvariant() switch
    {
        "armchair" or "arm"      => "FURN-CHAIR-ARM",
        "stool"                  => "FURN-CHAIR-STL",
        "examination" or "exam"  => "FURN-CHAIR-EXAM",
        "wheelchair" or "whl"    => "FURN-CHAIR-WHL",
        _                        => "FURN-CHAIR-OFF",
    };

    private static string ResolveDeskFamily(string type) => (type ?? "office").ToLowerInvariant() switch
    {
        "reception"              => "FURN-DESK-RCP",
        "nurse-station" or "nurse" => "FURN-DESK-NST",
        _                        => "FURN-DESK-OFF",
    };

    private static string ResolveCabinetFamily(string type) => (type ?? "storage").ToLowerInvariant() switch
    {
        "medical" or "med"       => "FURN-CBT-MED",
        "file"                   => "FURN-CBT-FIL",
        "wardrobe" or "wdr"      => "FURN-CBT-WDR",
        _                        => "FURN-CBT-STR",
    };

    private static string ResolveSofaName(string type, int seats)
    {
        int s = (seats == 2 || seats == 3) ? seats : 3;
        bool clinical = (type ?? "lounge").Equals("clinic", StringComparison.OrdinalIgnoreCase);
        return clinical ? $"FURN-SOFA-CLN-{s}" : $"FURN-SOFA-{s}";
    }

    private static (string Family, double W, double D) ResolveTableFamily(string shape, string? type, double w, double d)
    {
        var s = (shape ?? "rectangle").ToLowerInvariant();
        var t = (type ?? "meeting").ToLowerInvariant();
        if (t == "exam") return ("FURN-TBL-EXAM", w, d);
        return s switch
        {
            "round"  => ("FURN-TBL-ROUND", Math.Min(w, d), Math.Min(w, d)),
            "square" => ("FURN-TBL-SQ", Math.Min(w, d), Math.Min(w, d)),
            _        => ("FURN-TBL-RECT", w, d),
        };
    }

    private static Dictionary<string, string> BuildAttrs(string? invId, string? type, string? room)
    {
        var a = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(invId)) a["INV_ID"] = invId!;
        if (!string.IsNullOrWhiteSpace(type))  a["TYPE"]   = type!;
        if (!string.IsNullOrWhiteSpace(room))  a["ROOM"]   = room!;
        return a;
    }

    // ─────────── populate_room ───────────

    private static Task<ToolDispatchResult> PopulateRoom(JsonObject args, CancellationToken ct) =>
        RunW("acad.furniture.populate_room", args, ct, (doc, db, tr) =>
        {
            var a = Read<FurniturePopulateRoomDto>(args);
            var (min, max) = ResolveRoomBbox(db, tr, a);
            var plan = BuildPopulationPlan(a.Preset, min, max, a.Orientation, a.RoomName);

            var handles = new List<string>();
            var items = new List<string>();
            var warnings = new List<string>();

            foreach (var p in plan.Items)
            {
                try
                {
                    var attrs = BuildAttrs(null, p.Type, a.RoomName);
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

            if (plan.Warnings.Count > 0) warnings.InsertRange(0, plan.Warnings);

            return Wrap(new
            {
                preset = a.Preset,
                inserted = handles.Count,
                handles = handles,
                items = items,
                warnings = warnings
            });
        });

    private static (Point2d Min, Point2d Max) ResolveRoomBbox(Database db, Transaction tr, FurniturePopulateRoomDto a)
    {
        if (!string.IsNullOrWhiteSpace(a.RoomBoundaryHandle))
        {
            var id = AcadEnv.ResolveHandle(db, a.RoomBoundaryHandle!);
            var ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
            var e = ent.GeometricExtents;
            return (new Point2d(e.MinPoint.X, e.MinPoint.Y), new Point2d(e.MaxPoint.X, e.MaxPoint.Y));
        }
        if (a.BboxMin is not null && a.BboxMax is not null)
        {
            return (new Point2d(a.BboxMin.X, a.BboxMin.Y), new Point2d(a.BboxMax.X, a.BboxMax.Y));
        }
        throw new ArgumentException("populate_room requires either roomBoundaryHandle or (bboxMin + bboxMax).");
    }

    private sealed record PopulateItem(string BlockName, double X, double Y, double RotationDeg, string Type, double? SizedW = null, double? SizedD = null);
    private sealed record PopulationPlan(IReadOnlyList<PopulateItem> Items, IReadOnlyList<string> Warnings);

    private static PopulationPlan BuildPopulationPlan(string preset, Point2d min, Point2d max, string orientation, string? roomName)
    {
        var items = new List<PopulateItem>();
        var warnings = new List<string>();
        double w = max.X - min.X, h = max.Y - min.Y;
        double cx = (min.X + max.X) / 2.0, cy = (min.Y + max.Y) / 2.0;

        if (w < 1500 || h < 1500)
            warnings.Add($"Room {w:F0}x{h:F0}mm is smaller than 1500x1500 — preset may overflow.");

        double rot = orientation.ToLowerInvariant() switch
        {
            "east"  => 90,
            "south" => 180,
            "west"  => 270,
            _       => 0,
        };

        switch ((preset ?? "office").ToLowerInvariant())
        {
            case "ward-room":
                // two beds parallel along long axis, facing outward, with nightstands + visitor chair
                {
                    bool horiz = w >= h;
                    if (horiz)
                    {
                        items.Add(new("FURN-BED-STD", min.X + 600, cy - h * 0.23, 0, "standard"));
                        items.Add(new("FURN-BED-STD", min.X + 600, cy + h * 0.23, 0, "standard"));
                        items.Add(new("FURN-CBT-STR-400-400", min.X + 1250, cy - h * 0.23, 0, "storage", 400, 400));
                        items.Add(new("FURN-CBT-STR-400-400", min.X + 1250, cy + h * 0.23, 0, "storage", 400, 400));
                        items.Add(new("FURN-CHAIR-ARM", max.X - 500, cy, 180, "armchair"));
                    }
                    else
                    {
                        items.Add(new("FURN-BED-STD", cx - w * 0.23, min.Y + 1100, 90, "standard"));
                        items.Add(new("FURN-BED-STD", cx + w * 0.23, min.Y + 1100, 90, "standard"));
                        items.Add(new("FURN-CBT-STR-400-400", cx - w * 0.23, min.Y + 1900, 0, "storage", 400, 400));
                        items.Add(new("FURN-CBT-STR-400-400", cx + w * 0.23, min.Y + 1900, 0, "storage", 400, 400));
                        items.Add(new("FURN-CHAIR-ARM", cx, max.Y - 500, 180, "armchair"));
                    }
                    break;
                }
            case "icu-room":
                {
                    items.Add(new("FURN-BED-ICU", cx, cy, 0, "icu"));
                    items.Add(new("FURN-CBT-MED-700-500", cx, cy + h / 2.0 - 400, 0, "medical", 700, 500));
                    items.Add(new("FURN-CHAIR-ARM", cx + w * 0.3, cy - h * 0.2, 180, "armchair"));
                    break;
                }
            case "or-room":
                {
                    items.Add(new("FURN-BED-OR", cx, cy, 0, "or"));
                    items.Add(new("FURN-CBT-MED-800-500", cx - w * 0.3, cy, 0, "medical", 800, 500));
                    items.Add(new("FURN-CBT-MED-800-500", cx + w * 0.3, cy, 0, "medical", 800, 500));
                    // anaesthesia cart above head
                    items.Add(new("FURN-CBT-MED-700-500", cx, cy + h * 0.3, 0, "medical", 700, 500));
                    break;
                }
            case "office":
                {
                    items.Add(new("FURN-DESK-OFF-1600-800", cx, min.Y + 700, 0, "office", 1600, 800));
                    items.Add(new("FURN-CHAIR-OFF", cx, min.Y + 1400, 0, "office"));
                    items.Add(new("FURN-CBT-FIL-800-450", min.X + 500, max.Y - 300, 180, "file", 800, 450));
                    break;
                }
            case "reception":
                {
                    items.Add(new("FURN-DESK-RCP-2400-800", cx, min.Y + 700, 0, "reception", 2400, 800));
                    items.Add(new("FURN-CHAIR-OFF", cx - 600, min.Y + 1400, 0, "office"));
                    items.Add(new("FURN-CHAIR-OFF", cx + 600, min.Y + 1400, 0, "office"));
                    items.Add(new("FURN-SOFA-CLN-3", cx, max.Y - 500, 180, "clinic"));
                    break;
                }
            case "waiting":
                {
                    items.Add(new("FURN-SOFA-CLN-3", cx, min.Y + 500, 0, "clinic"));
                    items.Add(new("FURN-SOFA-CLN-3", cx, max.Y - 500, 180, "clinic"));
                    items.Add(new("FURN-TBL-ROUND-800-800", cx, cy, 0, "coffee", 800, 800));
                    break;
                }
            case "consult":
                {
                    items.Add(new("FURN-DESK-OFF-1400-800", cx - w * 0.2, min.Y + 700, 0, "office", 1400, 800));
                    items.Add(new("FURN-CHAIR-OFF", cx - w * 0.2, min.Y + 1400, 0, "office"));
                    items.Add(new("FURN-CHAIR-ARM", cx - w * 0.2 + 900, min.Y + 1400, 180, "armchair"));
                    items.Add(new("FURN-TBL-EXAM-1900-700", cx + w * 0.25, cy, 0, "exam", 1900, 700));
                    items.Add(new("FURN-CBT-MED-800-450", max.X - 400, max.Y - 300, 180, "medical", 800, 450));
                    break;
                }
            case "bedroom":
                {
                    items.Add(new("FURN-BED-RES", cx, min.Y + 900, 0, "residential"));
                    items.Add(new("FURN-CBT-NST-450-400", min.X + 350, min.Y + 300, 0, "nightstand", 450, 400));
                    items.Add(new("FURN-CBT-NST-450-400", max.X - 350, min.Y + 300, 0, "nightstand", 450, 400));
                    items.Add(new("FURN-CBT-WDR-1200-600", cx, max.Y - 300, 180, "wardrobe", 1200, 600));
                    break;
                }
            case "kitchen":
                {
                    items.Add(new("FURN-KIT-COUNTER-2400-600", cx, min.Y + 300, 0, "counter", 2400, 600));
                    items.Add(new("FURN-KIT-HOB", cx - w * 0.25, min.Y + 300, 0, "hob"));
                    items.Add(new("FURN-KIT-SINK", cx + w * 0.25, min.Y + 300, 0, "sink"));
                    items.Add(new("FURN-KIT-FRIDGE", max.X - 400, max.Y - 400, 0, "fridge"));
                    break;
                }
            case "living-room-res":
                {
                    items.Add(new("FURN-SOFA-3", cx, min.Y + 500, 0, "lounge"));
                    items.Add(new("FURN-TBL-RECT-1200-800", cx, cy, 0, "coffee", 1200, 800));
                    items.Add(new("FURN-CHAIR-ARM", max.X - 500, max.Y - 500, 180, "armchair"));
                    break;
                }
            default:
                warnings.Add($"Unknown preset '{preset}' — no items placed. Valid: ward-room / icu-room / or-room / office / reception / waiting / consult / bedroom / kitchen / living-room-res.");
                break;
        }

        // Rotate entire plan around centre if requested orientation != "north".
        if (rot != 0)
        {
            var rotated = new List<PopulateItem>(items.Count);
            double rad = rot * Math.PI / 180.0;
            double cos = Math.Cos(rad), sin = Math.Sin(rad);
            foreach (var it in items)
            {
                double dx = it.X - cx, dy = it.Y - cy;
                double nx = cx + dx * cos - dy * sin;
                double ny = cy + dx * sin + dy * cos;
                rotated.Add(it with { X = nx, Y = ny, RotationDeg = it.RotationDeg + rot });
            }
            items = rotated;
        }

        return new PopulationPlan(items, warnings);
    }

    // ─────────── list_furniture_in_model ───────────

    private static Task<ToolDispatchResult> ListFurnitureInModel(JsonObject args, CancellationToken ct) =>
        RunR("acad.furniture.list_furniture_in_model", args, ct, (doc, db, tr) =>
        {
            var a = Read<FurnitureListInModelArgsDto>(args);
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
                if (!bn.StartsWith("FURN-", StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrWhiteSpace(a.BlockFilter) && !bn.Equals(a.BlockFilter, StringComparison.OrdinalIgnoreCase)) continue;
                if (!string.IsNullOrWhiteSpace(a.LayerFilter) && !br.Layer.Equals(a.LayerFilter, StringComparison.OrdinalIgnoreCase)) continue;

                string? invId = null, type = null, note = null;
                foreach (ObjectId aid in br.AttributeCollection)
                {
                    var ar = (AttributeReference)tr.GetObject(aid, OpenMode.ForRead);
                    switch (ar.Tag.ToUpperInvariant())
                    {
                        case "INV_ID": invId = ar.TextString; break;
                        case "TYPE":   type  = ar.TextString; break;
                        case "NOTE":   note  = ar.TextString; break;
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
                    note = note,
                });
            }
            return Wrap(new { references = refs, count = refs.Count });
        });
}
