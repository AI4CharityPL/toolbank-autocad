// AutoCAD plugin handlers for acad-openings. Ten tools implementing doors +
// windows with REI / RC / acoustic / lead-shield attributes, auto-numbering
// D-001 / W-001, generic escape-hatch, quick-sketch (points) variants, wall
// surgery (cut_wall_for_opening) and schedule export (CSV / JSON).
//
// Architecture mirrors FurniturePluginTools: sized-family block factories keyed
// by "<FAMILY>-<W>-<H>", block origin at geometric centre of the opening, one
// unified attribute contract.
//
// Attribute contract (every opening BlockReference carries ALL of these,
// empty-string when irrelevant for the kind):
//   NUMBER, TYPE, WIDTH_MM, HEIGHT_MM, REI, RC, FIRE_CLASS, LEAF_DIR,
//   SWING_DIR, SILL_MM, ACOUSTIC_DB, LEAD, ROOM_FROM, ROOM_TO
//
// Rules: 10, 11, 12, 19, 28, 65.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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

internal static class OpeningsPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.openings.list_opening_catalog",  ListCatalog);
        host.Register("acad.openings.insert_door",           InsertDoor);
        host.Register("acad.openings.insert_window",         InsertWindow);
        host.Register("acad.openings.insert_opening_generic", InsertOpeningGeneric);
        host.Register("acad.openings.draw_door_by_points",   DrawDoorByPoints);
        host.Register("acad.openings.draw_window_by_points", DrawWindowByPoints);
        host.Register("acad.openings.cut_wall_for_opening",  CutWallForOpening);
        host.Register("acad.openings.renumber_openings",     RenumberOpenings);
        host.Register("acad.openings.list_openings_in_model", ListOpeningsInModel);
        host.Register("acad.openings.export_schedule",       ExportSchedule);
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

    private static string NameForSized(string family, double wMm, double hMm)
        => $"{family}-{(int)Math.Round(wMm)}-{(int)Math.Round(hMm)}";

    private static string DefaultLayerFor(string blockName)
    {
        var n = blockName.ToUpperInvariant();
        if (n.StartsWith("DOOR-FIRE"))    return "A-DOOR-FIRE";
        if (n.StartsWith("DOOR-LEAD"))    return "A-DOOR-LEAD";
        if (n.StartsWith("DOOR-HOSP"))    return "A-DOOR-HOSP";
        if (n.StartsWith("DOOR-"))        return "A-DOOR";
        if (n.StartsWith("WIN-FIRE") || n.StartsWith("WIN-HOSP")) return "A-GLAZ-FIRE";
        if (n.StartsWith("WIN-"))         return "A-GLAZ";
        return "A-DOOR";
    }

    // ─────────── catalog metadata ───────────

    internal sealed record OpeningFamily(
        string Family, string Kind, double DefaultWMm, double DefaultHMm,
        bool SupportsFire, bool SupportsBurglary, bool SupportsLead,
        string Description);

    private static readonly IReadOnlyList<OpeningFamily> s_families = new List<OpeningFamily>
    {
        // doors
        new("DOOR-SINGLE",   "door",  900, 2100, false, false, false, "Single-leaf hinged door with swing arc"),
        new("DOOR-DOUBLE",   "door", 1600, 2100, false, false, false, "Double-leaf hinged door with two swing arcs"),
        new("DOOR-SLIDING",  "door", 1000, 2100, false, false, false, "Sliding door with track indicator"),
        new("DOOR-FIRE",     "door", 1200, 2100, true,  false, false, "Fire-rated door (REI 30/60/90/120 per PN-EN 1634-1)"),
        new("DOOR-HOSP",     "door", 1800, 2100, true,  false, false, "Hospital double-swing door with bidirectional trajectory"),
        new("DOOR-LEAD",     "door", 1200, 2100, false, false, true,  "Lead-lined door for radiological shielding (RTG / CT rooms)"),
        // windows
        new("WIN-FIXED",     "window", 1200, 1500, false, true,  false, "Fixed non-opening window"),
        new("WIN-CASE",      "window", 1200, 1500, false, true,  false, "Casement (side-hung) window"),
        new("WIN-TILT",      "window", 1500, 1500, false, true,  false, "Tilt & turn window"),
        new("WIN-HOSP",      "window", 1800, 1500, true,  true,  false, "Hospital fire-rated window (E/EI30/EI60)"),
        new("WIN-FIRE",      "window", 1500, 1500, true,  true,  false, "Fire-rated window (EI30/EI60/EI120)"),
    };

    private static Task<ToolDispatchResult> ListCatalog(JsonObject args, CancellationToken ct) =>
        RunR("acad.openings.list_opening_catalog", args, ct, (doc, db, tr) =>
        {
            var a = Read<OpeningsListCatalogArgsDto>(args);
            IEnumerable<OpeningFamily> src = s_families;
            if (!string.IsNullOrWhiteSpace(a.Kind))
                src = src.Where(f => string.Equals(f.Kind, a.Kind, StringComparison.OrdinalIgnoreCase));

            var entries = src.Select(f => new
            {
                family = f.Family,
                kind = f.Kind,
                description = f.Description,
                defaultWidthMm = f.DefaultWMm,
                defaultHeightMm = f.DefaultHMm,
                supportsFire = f.SupportsFire,
                supportsBurglary = f.SupportsBurglary,
                supportsLeadShield = f.SupportsLead,
            }).ToList();
            return Wrap(new { entries, count = entries.Count });
        });

    // ─────────── block factory ───────────

    private static (ObjectId BtrId, bool Created, string Family, double W, double H) EnsureBlock(
        Database db, Transaction tr, string blockName)
    {
        AcadEnv.ValidateSymbolName(blockName, "Block");
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);

        var (family, wMm, hMm) = ParseSizedName(blockName);
        if (bt.Has(blockName))
            return (bt[blockName], false, family, wMm, hMm);

        bt.UpgradeOpen();
        var btr = new BlockTableRecord { Name = blockName };
        var btrId = bt.Add(btr);
        tr.AddNewlyCreatedDBObject(btr, true);

        BuildBlockGeometry(tr, btr, family, wMm, hMm);
        AddStandardAttributes(tr, btr, wMm);

        return (btrId, true, family, wMm, hMm);
    }

    private static (string Family, double W, double H) ParseSizedName(string name)
    {
        var parts = name.Split('-');
        if (parts.Length < 4)
            throw new ArgumentException(
                $"Opening block '{name}' must follow FAMILY-W-H format (e.g. DOOR-SINGLE-900-2100).");
        if (!int.TryParse(parts[^2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var w) ||
            !int.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var h))
            throw new ArgumentException($"Opening block '{name}': last two tokens must be integer width and height in mm.");
        var family = string.Join('-', parts.Take(parts.Length - 2));
        return (family, w, h);
    }

    private static void BuildBlockGeometry(Transaction tr, BlockTableRecord btr, string family, double w, double h)
    {
        switch (family.ToUpperInvariant())
        {
            case "DOOR-SINGLE":   DrawDoorSingle(tr, btr, w); break;
            case "DOOR-DOUBLE":   DrawDoorDouble(tr, btr, w); break;
            case "DOOR-SLIDING":  DrawDoorSliding(tr, btr, w); break;
            case "DOOR-FIRE":     DrawDoorFire(tr, btr, w); break;
            case "DOOR-HOSP":     DrawDoorHosp(tr, btr, w); break;
            case "DOOR-LEAD":     DrawDoorLead(tr, btr, w); break;
            case "WIN-FIXED":     DrawWinFixed(tr, btr, w); break;
            case "WIN-CASE":      DrawWinCasement(tr, btr, w); break;
            case "WIN-TILT":      DrawWinTilt(tr, btr, w); break;
            case "WIN-HOSP":      DrawWinHospital(tr, btr, w); break;
            case "WIN-FIRE":      DrawWinFire(tr, btr, w); break;
            default:
                throw new ArgumentException(
                    $"Unknown opening family '{family}'. Valid: {string.Join(", ", s_families.Select(f => f.Family))}.");
        }
    }

    // ─────────── primitive helpers (centred at origin; +Y = opening "inside" swing) ──

    private const double JAMB_MARK = 40;     // mm jamb tick
    private const double GLASS_OFFSET = 40;  // mm half-glass-thickness for windows
    private const double WALL_AXIS_CLEAR = 6; // cosmetic gap at wall axis

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

    /// <summary>
    /// Jamb tick marks at (-w/2, 0) and (+w/2, 0) indicating opening in the wall.
    /// </summary>
    private static void Jambs(Transaction tr, BlockTableRecord btr, double w)
    {
        Append(tr, btr,
            Seg(-w / 2.0, -JAMB_MARK / 2.0, -w / 2.0, JAMB_MARK / 2.0),
            Seg( w / 2.0, -JAMB_MARK / 2.0,  w / 2.0, JAMB_MARK / 2.0));
    }

    // ─────────── DOOR factories ───────────

    private static void DrawDoorSingle(Transaction tr, BlockTableRecord btr, double w)
    {
        // Hinge at (-w/2, 0); leaf extends into the room (+Y), 90° arc.
        double hingeX = -w / 2.0;
        double leafEndX = hingeX + w;
        Jambs(tr, btr, w);
        // leaf line (open 90° => pointing along +Y)
        Append(tr, btr, Seg(hingeX, 0, hingeX, w));
        // swing arc from leaf-end-open (hinge, hinge+w along +Y) sweeping to closed (hinge+w along +X)
        Append(tr, btr, new Arc(new Point3d(hingeX, 0, 0), w, 0, Math.PI / 2.0));
        // closed-position reference line (lighter visual — skip to keep read clean)
    }

    private static void DrawDoorDouble(Transaction tr, BlockTableRecord btr, double w)
    {
        // Two leaves, each w/2 long, hinged at the jambs, both opening inward (+Y).
        double half = w / 2.0;
        Jambs(tr, btr, w);
        Append(tr, btr, Seg(-half, 0, -half, half));     // left leaf open
        Append(tr, btr, Seg( half, 0,  half, half));     // right leaf open
        Append(tr, btr, new Arc(new Point3d(-half, 0, 0), half, 0, Math.PI / 2.0));
        Append(tr, btr, new Arc(new Point3d( half, 0, 0), half, Math.PI / 2.0, Math.PI));
    }

    private static void DrawDoorSliding(Transaction tr, BlockTableRecord btr, double w)
    {
        double half = w / 2.0;
        Jambs(tr, btr, w);
        // track as a line slightly above the wall axis, full width
        Append(tr, btr, Seg(-half, 80, half, 80));
        // closed leaf line on the axis
        Append(tr, btr, Seg(-half + 30, 0, half - 30, 0));
        // pocket / sliding indicator: dashed double-arrow
        Append(tr, btr,
            Seg(-half * 0.6, 40, half * 0.6, 40),
            Seg(half * 0.5, 20,  half * 0.6, 40),
            Seg(half * 0.5, 60,  half * 0.6, 40));
    }

    private static void DrawDoorFire(Transaction tr, BlockTableRecord btr, double w)
    {
        // Fire door looks like a thicker-framed single leaf with REI marker slot.
        DrawDoorSingle(tr, btr, w);
        // Inner rectangle (slightly inside jambs) acts as frame-thickness indicator.
        double half = w / 2.0;
        Append(tr, btr,
            Seg(-half + 50, -60, -half + 50, 60),
            Seg( half - 50, -60,  half - 50, 60));
        // Fire-marker circle near hinge (small)
        Append(tr, btr, new Circle(new Point3d(-half + 100, -100, 0), Vector3d.ZAxis, 60));
    }

    private static void DrawDoorHosp(Transaction tr, BlockTableRecord btr, double w)
    {
        // Hospital double-swing: 4 arcs (2 leaves × 2 directions).
        double half = w / 2.0;
        Jambs(tr, btr, w);
        // Left leaf: 90° both sides (+Y and -Y)
        Append(tr, btr, Seg(-half, 0, -half, half));
        Append(tr, btr, Seg(-half, 0, -half, -half));
        Append(tr, btr, new Arc(new Point3d(-half, 0, 0), half, 0, Math.PI / 2.0));
        Append(tr, btr, new Arc(new Point3d(-half, 0, 0), half, Math.PI * 1.5, Math.PI * 2.0));
        // Right leaf: same
        Append(tr, btr, Seg( half, 0,  half, half));
        Append(tr, btr, Seg( half, 0,  half, -half));
        Append(tr, btr, new Arc(new Point3d( half, 0, 0), half, Math.PI / 2.0, Math.PI));
        Append(tr, btr, new Arc(new Point3d( half, 0, 0), half, Math.PI, Math.PI * 1.5));
    }

    private static void DrawDoorLead(Transaction tr, BlockTableRecord btr, double w)
    {
        // Single leaf + doubled frame + Pb marker near hinge.
        DrawDoorSingle(tr, btr, w);
        double half = w / 2.0;
        // thicker frame (duplicate jamb tick farther out)
        Append(tr, btr,
            Seg(-half - 40, -JAMB_MARK / 2.0, -half - 40, JAMB_MARK / 2.0),
            Seg( half + 40, -JAMB_MARK / 2.0,  half + 40, JAMB_MARK / 2.0));
        // Pb marker: small square
        double pbX = -half + 150, pbY = -200;
        Append(tr, btr,
            Seg(pbX - 60, pbY - 60, pbX + 60, pbY - 60),
            Seg(pbX + 60, pbY - 60, pbX + 60, pbY + 60),
            Seg(pbX + 60, pbY + 60, pbX - 60, pbY + 60),
            Seg(pbX - 60, pbY + 60, pbX - 60, pbY - 60));
    }

    // ─────────── WINDOW factories ───────────

    private static void DrawWinFixed(Transaction tr, BlockTableRecord btr, double w)
    {
        double half = w / 2.0;
        Jambs(tr, btr, w);
        // outer glass line
        Append(tr, btr, Seg(-half, GLASS_OFFSET, half, GLASS_OFFSET));
        Append(tr, btr, Seg(-half, -GLASS_OFFSET, half, -GLASS_OFFSET));
        // centre line (single pane)
        Append(tr, btr, Seg(-half, 0, half, 0));
    }

    private static void DrawWinCasement(Transaction tr, BlockTableRecord btr, double w)
    {
        DrawWinFixed(tr, btr, w);
        // opening hint: small triangle at centre indicating sash swing
        double half = w / 2.0;
        double sashW = Math.Min(w * 0.4, 600);
        Append(tr, btr,
            Seg(-sashW / 2.0, 0, 0, GLASS_OFFSET + 100),
            Seg( sashW / 2.0, 0, 0, GLASS_OFFSET + 100));
    }

    private static void DrawWinTilt(Transaction tr, BlockTableRecord btr, double w)
    {
        DrawWinFixed(tr, btr, w);
        // arrowhead pointing inward (tilt marker)
        Append(tr, btr,
            Seg(0, GLASS_OFFSET + 200, -120, GLASS_OFFSET + 20),
            Seg(0, GLASS_OFFSET + 200,  120, GLASS_OFFSET + 20));
    }

    private static void DrawWinHospital(Transaction tr, BlockTableRecord btr, double w)
    {
        DrawWinFixed(tr, btr, w);
        // cross-hatch marker (fire-rated indicator) at centre
        Append(tr, btr,
            Seg(-80, -GLASS_OFFSET, 80, GLASS_OFFSET),
            Seg( 80, -GLASS_OFFSET, -80, GLASS_OFFSET));
    }

    private static void DrawWinFire(Transaction tr, BlockTableRecord btr, double w)
    {
        DrawWinFixed(tr, btr, w);
        // double cross marker
        Append(tr, btr,
            Seg(-100, -GLASS_OFFSET, 100, GLASS_OFFSET),
            Seg( 100, -GLASS_OFFSET, -100, GLASS_OFFSET),
            Seg(-50, -GLASS_OFFSET, 50, GLASS_OFFSET),
            Seg( 50, -GLASS_OFFSET, -50, GLASS_OFFSET));
    }

    // ─────────── attribute contract ───────────

    private static readonly string[] s_attrTags =
    {
        "NUMBER", "TYPE", "WIDTH_MM", "HEIGHT_MM",
        "REI", "RC", "FIRE_CLASS", "LEAF_DIR", "SWING_DIR",
        "SILL_MM", "ACOUSTIC_DB", "LEAD", "ROOM_FROM", "ROOM_TO"
    };

    private static void AddStandardAttributes(Transaction tr, BlockTableRecord btr, double wMm)
    {
        double txt = Math.Clamp(wMm * 0.07, 80, 180);

        void Attr(string tag, string prompt, double y, bool invisible)
        {
            var ad = new AttributeDefinition
            {
                Tag = tag,
                Prompt = prompt,
                TextString = "",
                Height = txt,
                Position = new Point3d(0, y, 0),
                HorizontalMode = TextHorizontalMode.TextCenter,
                VerticalMode = TextVerticalMode.TextVerticalMid,
                AlignmentPoint = new Point3d(0, y, 0),
                Invisible = invisible,
            };
            btr.AppendEntity(ad);
            tr.AddNewlyCreatedDBObject(ad, true);
        }

        // Visible: NUMBER (above the opening centreline, readable above wall).
        Attr("NUMBER", "Opening number", wMm * 0.5, invisible: false);
        // Invisible schedule attrs.
        foreach (var tag in s_attrTags.Skip(1))
            Attr(tag, tag, 0, invisible: true);
    }

    // ─────────── BlockReference insertion + attribute materialisation ─────

    private static (EntityHandle Handle, string BlockName, bool Created, double WidthMm, double HeightMm) InsertBlockCore(
        Database db, Transaction tr, string blockName, Point2dDto pos, double rotDeg,
        string? layer, IReadOnlyDictionary<string, string>? attrs)
    {
        var (btrId, created, family, w, h) = EnsureBlock(db, tr, blockName);
        var br = new BlockReference(AcadEnv.ToPoint3d(pos), btrId)
        {
            Rotation = rotDeg * Math.PI / 180.0,
        };
        var effectiveLayer = string.IsNullOrWhiteSpace(layer) ? DefaultLayerFor(blockName) : layer;
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

        return (handle, blockName, created, w, h);
    }

    // ─────────── door / window resolvers ───────────

    private static string ResolveDoorFamily(string type, bool leadShielded) => leadShielded
        ? "DOOR-LEAD"
        : (type ?? "single").ToLowerInvariant() switch
        {
            "double"                => "DOOR-DOUBLE",
            "sliding"               => "DOOR-SLIDING",
            "fire"                  => "DOOR-FIRE",
            "hospital" or "hosp"    => "DOOR-HOSP",
            "lead"                  => "DOOR-LEAD",
            _                       => "DOOR-SINGLE",
        };

    private static string ResolveWindowFamily(string type) => (type ?? "casement").ToLowerInvariant() switch
    {
        "fixed"                 => "WIN-FIXED",
        "tilt" or "tilt-turn"   => "WIN-TILT",
        "hospital" or "hosp"    => "WIN-HOSP",
        "fire"                  => "WIN-FIRE",
        _                       => "WIN-CASE",
    };

    // ─────────── number allocation ───────────

    private static string NextNumber(Database db, Transaction tr, string prefix, int padDigits = 3)
    {
        int max = 0;
        foreach (var br in EnumerateOpeningRefs(db, tr))
        {
            string? num = GetAttr(br, "NUMBER");
            if (string.IsNullOrWhiteSpace(num)) continue;
            if (!num.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase)) continue;
            var tail = num.Substring(prefix.Length + 1);
            if (int.TryParse(tail, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                if (n > max) max = n;
        }
        int next = max + 1;
        return $"{prefix}-{next.ToString(new string('0', Math.Max(1, padDigits)), CultureInfo.InvariantCulture)}";
    }

    private static IEnumerable<BlockReference> EnumerateOpeningRefs(Database db, Transaction tr)
    {
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        foreach (ObjectId id in ms)
        {
            var e = tr.GetObject(id, OpenMode.ForRead);
            if (e is not BlockReference br) continue;
            string name;
            try
            {
                var btr = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                name = btr.Name;
            }
            catch { continue; }
            if (!name.StartsWith("DOOR-", StringComparison.OrdinalIgnoreCase) &&
                !name.StartsWith("WIN-",  StringComparison.OrdinalIgnoreCase)) continue;
            yield return br;
        }
    }

    private static string? GetAttr(BlockReference br, string tag)
    {
        foreach (ObjectId aid in br.AttributeCollection)
        {
            var ar = (AttributeReference)br.Database.TransactionManager.TopTransaction.GetObject(aid, OpenMode.ForRead);
            if (string.Equals(ar.Tag, tag, StringComparison.OrdinalIgnoreCase))
                return ar.TextString;
        }
        return null;
    }

    private static void SetAttr(BlockReference br, string tag, string value)
    {
        foreach (ObjectId aid in br.AttributeCollection)
        {
            var ar = (AttributeReference)br.Database.TransactionManager.TopTransaction.GetObject(aid, OpenMode.ForWrite);
            if (string.Equals(ar.Tag, tag, StringComparison.OrdinalIgnoreCase))
            {
                ar.TextString = value;
                return;
            }
        }
    }

    // ─────────── InsertDoor ───────────

    private static Task<ToolDispatchResult> InsertDoor(JsonObject args, CancellationToken ct) =>
        RunW("acad.openings.insert_door", args, ct, (doc, db, tr) =>
        {
            var a = Read<OpeningsInsertDoorDto>(args);
            var family = ResolveDoorFamily(a.Type, a.LeadShielded);
            var name = NameForSized(family, a.WidthMm, a.HeightMm);

            string? number = a.Number;
            if (string.IsNullOrWhiteSpace(number) && a.AutoNumber)
                number = NextNumber(db, tr, "D");

            var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["NUMBER"]      = number ?? "",
                ["TYPE"]        = a.Type ?? "single",
                ["WIDTH_MM"]    = ((int)Math.Round(a.WidthMm)).ToString(CultureInfo.InvariantCulture),
                ["HEIGHT_MM"]   = ((int)Math.Round(a.HeightMm)).ToString(CultureInfo.InvariantCulture),
                ["REI"]         = a.Rei.ToString(CultureInfo.InvariantCulture),
                ["RC"]          = "0",
                ["FIRE_CLASS"]  = "",
                ["LEAF_DIR"]    = string.IsNullOrWhiteSpace(a.LeafDirection) ? "R" : a.LeafDirection.ToUpperInvariant(),
                ["SWING_DIR"]   = string.IsNullOrWhiteSpace(a.SwingDirection) ? "IN" : a.SwingDirection.ToUpperInvariant(),
                ["SILL_MM"]     = "0",
                ["ACOUSTIC_DB"] = a.AcousticDb.ToString(CultureInfo.InvariantCulture),
                ["LEAD"]        = a.LeadShielded ? "1" : "0",
                ["ROOM_FROM"]   = a.RoomFrom ?? "",
                ["ROOM_TO"]     = a.RoomTo ?? "",
            };
            var r = InsertBlockCore(db, tr, name, a.Position, a.RotationDeg, a.Layer, attrs);
            return Wrap(new
            {
                entity = r.Handle, blockName = r.BlockName, created = r.Created, number,
                widthMm = r.WidthMm, heightMm = r.HeightMm,
            });
        });

    // ─────────── InsertWindow ───────────

    private static Task<ToolDispatchResult> InsertWindow(JsonObject args, CancellationToken ct) =>
        RunW("acad.openings.insert_window", args, ct, (doc, db, tr) =>
        {
            var a = Read<OpeningsInsertWindowDto>(args);
            var family = ResolveWindowFamily(a.Type);
            var name = NameForSized(family, a.WidthMm, a.HeightMm);

            string? number = a.Number;
            if (string.IsNullOrWhiteSpace(number) && a.AutoNumber)
                number = NextNumber(db, tr, "W");

            var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["NUMBER"]      = number ?? "",
                ["TYPE"]        = a.Type ?? "casement",
                ["WIDTH_MM"]    = ((int)Math.Round(a.WidthMm)).ToString(CultureInfo.InvariantCulture),
                ["HEIGHT_MM"]   = ((int)Math.Round(a.HeightMm)).ToString(CultureInfo.InvariantCulture),
                ["REI"]         = "0",
                ["RC"]          = a.Rc.ToString(CultureInfo.InvariantCulture),
                ["FIRE_CLASS"]  = a.FireClass ?? "",
                ["LEAF_DIR"]    = "",
                ["SWING_DIR"]   = "",
                ["SILL_MM"]     = ((int)Math.Round(a.SillHeightMm)).ToString(CultureInfo.InvariantCulture),
                ["ACOUSTIC_DB"] = "0",
                ["LEAD"]        = "0",
                ["ROOM_FROM"]   = a.Room ?? "",
                ["ROOM_TO"]     = "",
            };
            var r = InsertBlockCore(db, tr, name, a.Position, a.RotationDeg, a.Layer, attrs);
            return Wrap(new
            {
                entity = r.Handle, blockName = r.BlockName, created = r.Created, number,
                widthMm = r.WidthMm, heightMm = r.HeightMm,
            });
        });

    // ─────────── InsertOpeningGeneric ───────────

    private static Task<ToolDispatchResult> InsertOpeningGeneric(JsonObject args, CancellationToken ct) =>
        RunW("acad.openings.insert_opening_generic", args, ct, (doc, db, tr) =>
        {
            var a = Read<OpeningsInsertGenericDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name))
                throw new ArgumentException("insert_opening_generic: 'name' is required.");
            var r = InsertBlockCore(db, tr, a.Name, a.Position, a.RotationDeg, a.Layer, a.Attributes);
            string? number = null;
            if (a.Attributes is not null) a.Attributes.TryGetValue("NUMBER", out number);
            return Wrap(new
            {
                entity = r.Handle, blockName = r.BlockName, created = r.Created, number,
                widthMm = r.WidthMm, heightMm = r.HeightMm,
            });
        });

    // ─────────── Quick-sketch door / window ───────────

    private static Task<ToolDispatchResult> DrawDoorByPoints(JsonObject args, CancellationToken ct) =>
        RunW("acad.openings.draw_door_by_points", args, ct, (doc, db, tr) =>
        {
            var a = Read<OpeningsDrawDoorDto>(args);
            var hp = AcadEnv.ToPoint3d(a.HingePoint);
            var ep = AcadEnv.ToPoint3d(a.LeafEnd);
            double dx = ep.X - hp.X, dy = ep.Y - hp.Y;
            double r = Math.Sqrt(dx * dx + dy * dy);
            if (r < 100)
                throw new ArgumentException("draw_door_by_points: hinge and leaf-end are less than 100mm apart.");
            double startAng = Math.Atan2(dy, dx);
            bool inward = string.Equals(a.SwingDirection, "IN", StringComparison.OrdinalIgnoreCase);
            double sweep = Math.PI / 2.0 * (inward ? 1.0 : -1.0);
            double arcStart = inward ? startAng : startAng + sweep;
            double arcEnd   = inward ? startAng + sweep : startAng;

            var leaf = new Line(hp, ep);
            var arc = new Arc(hp, r, arcStart, arcEnd);
            var layer = a.Layer ?? "A-DOOR";
            var lh = AcadEnv.Persist(db, tr, leaf, layer);
            var ah = AcadEnv.Persist(db, tr, arc, layer);
            return Wrap(new { entities = new[] { lh, ah } });
        });

    private static Task<ToolDispatchResult> DrawWindowByPoints(JsonObject args, CancellationToken ct) =>
        RunW("acad.openings.draw_window_by_points", args, ct, (doc, db, tr) =>
        {
            var a = Read<OpeningsDrawWindowDto>(args);
            var p1 = AcadEnv.ToPoint3d(a.Jamb1);
            var p2 = AcadEnv.ToPoint3d(a.Jamb2);
            double dx = p2.X - p1.X, dy = p2.Y - p1.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 100)
                throw new ArgumentException("draw_window_by_points: jambs are less than 100mm apart.");
            // unit normal perpendicular to the jamb line, length = wallThickness/2.
            double nx = -dy / len, ny = dx / len;
            double t = a.WallThickness / 2.0;
            var p1a = new Point3d(p1.X + nx * t, p1.Y + ny * t, 0);
            var p2a = new Point3d(p2.X + nx * t, p2.Y + ny * t, 0);
            var p1b = new Point3d(p1.X - nx * t, p1.Y - ny * t, 0);
            var p2b = new Point3d(p2.X - nx * t, p2.Y - ny * t, 0);

            var layer = a.Layer ?? "A-GLAZ";
            var h1 = AcadEnv.Persist(db, tr, new Line(p1a, p2a), layer);
            var h2 = AcadEnv.Persist(db, tr, new Line(p1b, p2b), layer);
            var h3 = AcadEnv.Persist(db, tr, new Line(p1, p2), layer);
            return Wrap(new { entities = new[] { h1, h2, h3 } });
        });

    // ─────────── Wall cutting ───────────

    private static Task<ToolDispatchResult> CutWallForOpening(JsonObject args, CancellationToken ct) =>
        RunW("acad.openings.cut_wall_for_opening", args, ct, (doc, db, tr) =>
        {
            var a = Read<OpeningsCutWallDto>(args);
            var id = AcadEnv.ResolveHandle(db, a.WallHandle);
            var ent = (Entity)tr.GetObject(id, OpenMode.ForWrite);

            Point3d start, end;
            string wallLayer;
            switch (ent)
            {
                case Line line:
                    start = line.StartPoint;
                    end = line.EndPoint;
                    wallLayer = line.Layer;
                    break;
                case Polyline pl:
                    if (pl.NumberOfVertices != 2)
                        throw new ArgumentException(
                            $"cut_wall_for_opening: polyline has {pl.NumberOfVertices} vertices; only 2-vertex polylines supported in D5. Use D6 'split_wall_at_opening' for multi-segment walls.");
                    start = pl.GetPoint3dAt(0);
                    end = pl.GetPoint3dAt(1);
                    wallLayer = pl.Layer;
                    break;
                default:
                    throw new ArgumentException($"cut_wall_for_opening: entity type {ent.GetType().Name} not supported. Provide a Line or 2-vertex Polyline handle.");
            }

            var j1 = AcadEnv.ToPoint3d(a.Jamb1);
            var j2 = AcadEnv.ToPoint3d(a.Jamb2);

            double ProjectParam(Point3d p)
            {
                var v = end - start;
                double len2 = v.X * v.X + v.Y * v.Y;
                if (len2 < 1e-9) return 0;
                return ((p.X - start.X) * v.X + (p.Y - start.Y) * v.Y) / len2;
            }

            double t1 = ProjectParam(j1);
            double t2 = ProjectParam(j2);
            if (t1 > t2) (t1, t2) = (t2, t1);
            if (t1 < -1e-6 || t2 > 1 + 1e-6)
                throw new ArgumentException(
                    $"cut_wall_for_opening: jambs project outside the wall segment (t1={t1:F3}, t2={t2:F3}). Provide jambs that lie on the wall axis.");

            Point3d LerpP(double t) =>
                new(start.X + (end.X - start.X) * t, start.Y + (end.Y - start.Y) * t, 0);

            var cut1 = LerpP(Math.Max(0, t1));
            var cut2 = LerpP(Math.Min(1, t2));
            double dx = cut2.X - cut1.X, dy = cut2.Y - cut1.Y;
            double gap = Math.Sqrt(dx * dx + dy * dy);

            // Erase original.
            ent.Erase();

            EntityHandle? leftH = null, rightH = null;
            if ((cut1 - start).Length > 1) // left survives
            {
                leftH = AcadEnv.Persist(db, tr, new Line(start, cut1), wallLayer);
            }
            if ((end - cut2).Length > 1) // right survives
            {
                rightH = AcadEnv.Persist(db, tr, new Line(cut2, end), wallLayer);
            }

            return Wrap(new
            {
                originalHandle = a.WallHandle,
                leftHandle = leftH?.Handle,
                rightHandle = rightH?.Handle,
                gapLengthMm = gap,
            });
        });

    // ─────────── Renumber ───────────

    private static Task<ToolDispatchResult> RenumberOpenings(JsonObject args, CancellationToken ct) =>
        RunW("acad.openings.renumber_openings", args, ct, (doc, db, tr) =>
        {
            var a = Read<OpeningsRenumberDto>(args);
            var kind = (a.Kind ?? "all").ToLowerInvariant();
            var order = (a.Order ?? "insertion").ToLowerInvariant();
            int pad = Math.Max(1, a.PadDigits);
            int startAt = Math.Max(0, a.StartAt);

            var doors = new List<BlockReference>();
            var windows = new List<BlockReference>();
            foreach (var br in EnumerateOpeningRefs(db, tr))
            {
                var btr = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                if (btr.Name.StartsWith("DOOR-", StringComparison.OrdinalIgnoreCase)) doors.Add(br);
                else if (btr.Name.StartsWith("WIN-", StringComparison.OrdinalIgnoreCase)) windows.Add(br);
            }

            if (order == "spatial")
            {
                doors = doors.OrderByDescending(b => b.Position.Y).ThenBy(b => b.Position.X).ToList();
                windows = windows.OrderByDescending(b => b.Position.Y).ThenBy(b => b.Position.X).ToList();
            }

            var changes = new List<object>();
            int dCount = 0, wCount = 0;

            void Assign(List<BlockReference> list, string prefix, ref int counter)
            {
                int i = startAt;
                foreach (var br in list)
                {
                    var w = (BlockReference)tr.GetObject(br.ObjectId, OpenMode.ForWrite);
                    string newNum = $"{prefix}-{i.ToString(new string('0', pad), CultureInfo.InvariantCulture)}";
                    string? oldNum = GetAttr(br, "NUMBER");
                    SetAttr(w, "NUMBER", newNum);
                    changes.Add(new { handle = br.Handle.ToString(), oldNumber = oldNum, newNumber = newNum });
                    counter++;
                    i++;
                }
            }

            if (kind is "doors" or "all") Assign(doors, a.PrefixDoor ?? "D", ref dCount);
            if (kind is "windows" or "all") Assign(windows, a.PrefixWindow ?? "W", ref wCount);

            return Wrap(new
            {
                doorsRenumbered = dCount,
                windowsRenumbered = wCount,
                changes,
            });
        });

    // ─────────── ListOpeningsInModel ───────────

    private static Task<ToolDispatchResult> ListOpeningsInModel(JsonObject args, CancellationToken ct) =>
        RunR("acad.openings.list_openings_in_model", args, ct, (doc, db, tr) =>
        {
            var a = Read<OpeningsListInModelDto>(args);
            var kind = (a.Kind ?? "all").ToLowerInvariant();

            var openings = new List<object>();
            foreach (var br in EnumerateOpeningRefs(db, tr))
            {
                var btr = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                bool isDoor = btr.Name.StartsWith("DOOR-", StringComparison.OrdinalIgnoreCase);
                bool isWindow = btr.Name.StartsWith("WIN-", StringComparison.OrdinalIgnoreCase);
                if (kind == "doors" && !isDoor) continue;
                if (kind == "windows" && !isWindow) continue;
                if (!string.IsNullOrWhiteSpace(a.LayerFilter) &&
                    !string.Equals(br.Layer, a.LayerFilter, StringComparison.OrdinalIgnoreCase))
                    continue;

                string? number = GetAttr(br, "NUMBER");
                string? type = GetAttr(br, "TYPE");
                double w = ParseDouble(GetAttr(br, "WIDTH_MM"));
                double h = ParseDouble(GetAttr(br, "HEIGHT_MM"));
                int rei = ParseInt(GetAttr(br, "REI"));
                int rc = ParseInt(GetAttr(br, "RC"));
                string? fireClass = GetAttr(br, "FIRE_CLASS");
                int acoustic = ParseInt(GetAttr(br, "ACOUSTIC_DB"));
                bool lead = GetAttr(br, "LEAD") == "1";
                string? rFrom = GetAttr(br, "ROOM_FROM");
                string? rTo   = GetAttr(br, "ROOM_TO");

                openings.Add(new
                {
                    handle = br.Handle.ToString(),
                    blockName = btr.Name,
                    kind = isDoor ? "door" : "window",
                    number, type,
                    widthMm = w, heightMm = h,
                    rei, rc, fireClass,
                    acousticDb = acoustic,
                    leadShielded = lead,
                    roomFrom = rFrom, roomTo = rTo,
                    position = new Point2dDto(br.Position.X, br.Position.Y),
                    rotationDeg = br.Rotation * 180.0 / Math.PI,
                    layer = br.Layer,
                });
            }
            return Wrap(new { openings, count = openings.Count });
        });

    private static double ParseDouble(string? s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0.0;

    private static int ParseInt(string? s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;

    // ─────────── Export schedule ───────────

    private static readonly string[] s_csvHeaders =
    {
        "NUMBER","KIND","BLOCK","TYPE","WIDTH_MM","HEIGHT_MM","REI","RC","FIRE_CLASS",
        "LEAF_DIR","SWING_DIR","SILL_MM","ACOUSTIC_DB","LEAD","ROOM_FROM","ROOM_TO","LAYER","HANDLE"
    };

    private static Task<ToolDispatchResult> ExportSchedule(JsonObject args, CancellationToken ct) =>
        RunR("acad.openings.export_schedule", args, ct, (doc, db, tr) =>
        {
            var a = Read<OpeningsExportScheduleDto>(args);
            var kind = (a.Kind ?? "all").ToLowerInvariant();
            var format = (a.Format ?? "csv").ToLowerInvariant();

            var rows = new List<Dictionary<string, string>>();
            foreach (var br in EnumerateOpeningRefs(db, tr))
            {
                var btr = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                bool isDoor = btr.Name.StartsWith("DOOR-", StringComparison.OrdinalIgnoreCase);
                bool isWindow = btr.Name.StartsWith("WIN-", StringComparison.OrdinalIgnoreCase);
                if (kind == "doors" && !isDoor) continue;
                if (kind == "windows" && !isWindow) continue;

                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["NUMBER"]     = GetAttr(br, "NUMBER") ?? "",
                    ["KIND"]       = isDoor ? "door" : "window",
                    ["BLOCK"]      = btr.Name,
                    ["TYPE"]       = GetAttr(br, "TYPE") ?? "",
                    ["WIDTH_MM"]   = GetAttr(br, "WIDTH_MM") ?? "",
                    ["HEIGHT_MM"]  = GetAttr(br, "HEIGHT_MM") ?? "",
                    ["REI"]        = GetAttr(br, "REI") ?? "",
                    ["RC"]         = GetAttr(br, "RC") ?? "",
                    ["FIRE_CLASS"] = GetAttr(br, "FIRE_CLASS") ?? "",
                    ["LEAF_DIR"]   = GetAttr(br, "LEAF_DIR") ?? "",
                    ["SWING_DIR"]  = GetAttr(br, "SWING_DIR") ?? "",
                    ["SILL_MM"]    = GetAttr(br, "SILL_MM") ?? "",
                    ["ACOUSTIC_DB"]= GetAttr(br, "ACOUSTIC_DB") ?? "",
                    ["LEAD"]       = GetAttr(br, "LEAD") ?? "",
                    ["ROOM_FROM"]  = GetAttr(br, "ROOM_FROM") ?? "",
                    ["ROOM_TO"]    = GetAttr(br, "ROOM_TO") ?? "",
                    ["LAYER"]      = br.Layer,
                    ["HANDLE"]     = br.Handle.ToString(),
                };
                rows.Add(row);
            }

            rows = rows.OrderBy(r => r["NUMBER"], StringComparer.OrdinalIgnoreCase).ToList();

            string content;
            if (format == "json")
            {
                content = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
            }
            else // csv
            {
                var sb = new StringBuilder();
                sb.AppendLine(string.Join(",", s_csvHeaders));
                foreach (var r in rows)
                    sb.AppendLine(string.Join(",", s_csvHeaders.Select(h => CsvEscape(r.TryGetValue(h, out var v) ? v : ""))));
                content = sb.ToString();
            }

            if (!string.IsNullOrWhiteSpace(a.OutputPath))
            {
                var path = a.OutputPath!;
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, content, Encoding.UTF8);
            }

            return Wrap(new
            {
                kind,
                format,
                outputPath = a.OutputPath,
                rowCount = rows.Count,
                content,
            });
        });

    private static string CsvEscape(string? v)
    {
        v ??= "";
        if (v.Contains(',') || v.Contains('"') || v.Contains('\n'))
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }
}

