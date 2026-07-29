// Helpers used by every domain plugin tool: layer ensuring, transaction wrapping,
// handle conversion, point conversion, error mapping. Keep these tiny and side-effect free.
//
// See rule 11 (transactions), rule 12 (error mapping), rule 19 (impl pattern).

using System;
using System.Globalization;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;
using AcadRuntime = Autodesk.AutoCAD.Runtime;

namespace AcadMcp.Plugin.Tools;

internal static class AcadEnv
{
    public static Document RequireActiveDocument()
    {
        var doc = AcadApp.DocumentManager?.MdiActiveDocument
                  ?? throw new InvalidOperationException("No active AutoCAD document.");
        return doc;
    }

    public static ObjectId EnsureLayer(Database db, Transaction tr, string? layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName)) return db.Clayer;
        var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (lt.Has(layerName))
        {
            return lt[layerName];
        }
        lt.UpgradeOpen();
        var ltr = new LayerTableRecord { Name = layerName };
        var id = lt.Add(ltr);
        tr.AddNewlyCreatedDBObject(ltr, true);
        return id;
    }

    public static EntityHandle Persist(Database db, Transaction tr, Entity ent, string? layer)
    {
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
        if (!string.IsNullOrWhiteSpace(layer))
        {
            ent.LayerId = EnsureLayer(db, tr, layer);
        }
        ms.AppendEntity(ent);
        tr.AddNewlyCreatedDBObject(ent, true);
        return ToHandle(ent);
    }

    public static EntityHandle ToHandle(Entity ent)
    {
        string layer = "<none>";
        try { layer = ent.Layer; } catch { }
        return new EntityHandle(
            Handle: ent.Handle.ToString(),
            ObjectClass: ent.GetRXClass().Name,
            Layer: layer,
            OwnerHandle: ent.OwnerId != ObjectId.Null ? ent.OwnerId.Handle.ToString() : null);
    }

    public static ObjectId ResolveHandle(Database db, string handleStr)
    {
        if (string.IsNullOrWhiteSpace(handleStr))
            throw new ArgumentException("handle is empty", nameof(handleStr));
        if (!long.TryParse(handleStr, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var num))
            throw new ArgumentException($"handle '{handleStr}' is not a valid hex Handle string", nameof(handleStr));
        var h = new Handle(num);
        if (!db.TryGetObjectId(h, out var id))
            throw new InvalidOperationException($"Handle '{handleStr}' not found in current database.");
        return id;
    }

    public static Point2d ToPoint2d(Point2dDto p) => new(p.X, p.Y);
    public static Point3d ToPoint3d(Point2dDto p, double z = 0) => new(p.X, p.Y, z);
    public static Point3d ToPoint3d(Point3dDto p) => new(p.X, p.Y, p.Z);
    public static Vector3d ToVector3d(Vector3dDto v) => new(v.X, v.Y, v.Z);
    public static Vector3d ToVector3d(Point3dDto p) => new(p.X, p.Y, p.Z);
    public static Point2dDto FromPoint(Point2d p) => new(p.X, p.Y);
    public static Point2dDto FromPoint(Point3d p) => new(p.X, p.Y);
    public static Point3dDto FromPoint3d(Point3d p) => new(p.X, p.Y, p.Z);

    public static BoundingBoxDto BoundsOf(Extents3d e) => new(
        new Point3dDto(e.MinPoint.X, e.MinPoint.Y, e.MinPoint.Z),
        new Point3dDto(e.MaxPoint.X, e.MaxPoint.Y, e.MaxPoint.Z));

    public static ColorDto ColorOf(Entity ent)
    {
        var c = ent.Color;
        return new ColorDto(c.Red, c.Green, c.Blue, c.IsByAci ? c.ColorIndex : (int?)null);
    }

    /// <summary>
    /// Convert our ColorDto into an AutoCAD Color. ACI takes precedence if present (1..255).
    /// </summary>
    public static Color FromColorDto(ColorDto dto)
    {
        if (dto.AciIndex.HasValue && dto.AciIndex.Value >= 1 && dto.AciIndex.Value <= 255)
        {
            return Color.FromColorIndex(ColorMethod.ByAci, (short)dto.AciIndex.Value);
        }
        return Color.FromRgb((byte)Math.Clamp(dto.R, 0, 255), (byte)Math.Clamp(dto.G, 0, 255), (byte)Math.Clamp(dto.B, 0, 255));
    }

    public static ColorDto ColorToDto(Color c) =>
        new(c.Red, c.Green, c.Blue, c.IsByAci ? c.ColorIndex : (int?)null);

    /// <summary>
    /// Snap a millimeter value to the nearest standard AutoCAD LineWeight enum.
    /// See rule 26-acad-api-traps.mdc on LineWeight being an enum.
    /// </summary>
    public static LineWeight NearestLineWeight(double mm)
    {
        var standard = new (double mm, LineWeight lw)[]
        {
            (0.00, LineWeight.LineWeight000), (0.05, LineWeight.LineWeight005),
            (0.09, LineWeight.LineWeight009), (0.13, LineWeight.LineWeight013),
            (0.15, LineWeight.LineWeight015), (0.18, LineWeight.LineWeight018),
            (0.20, LineWeight.LineWeight020), (0.25, LineWeight.LineWeight025),
            (0.30, LineWeight.LineWeight030), (0.35, LineWeight.LineWeight035),
            (0.40, LineWeight.LineWeight040), (0.50, LineWeight.LineWeight050),
            (0.53, LineWeight.LineWeight053), (0.60, LineWeight.LineWeight060),
            (0.70, LineWeight.LineWeight070), (0.80, LineWeight.LineWeight080),
            (0.90, LineWeight.LineWeight090), (1.00, LineWeight.LineWeight100),
            (1.06, LineWeight.LineWeight106), (1.20, LineWeight.LineWeight120),
            (1.40, LineWeight.LineWeight140), (1.58, LineWeight.LineWeight158),
            (2.00, LineWeight.LineWeight200), (2.11, LineWeight.LineWeight211),
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

    /// <summary>Convert AutoCAD LineWeight enum to millimeters (or null for ByLayer/ByBlock/Default).</summary>
    public static double? LineWeightToMm(LineWeight lw)
    {
        if (lw == LineWeight.ByLayer || lw == LineWeight.ByBlock || lw == LineWeight.ByLineWeightDefault)
            return null;
        // The enum value equals 100 * mm (e.g. LineWeight013 = 13).
        return ((int)lw) / 100.0;
    }

    /// <summary>
    /// Validate a name as a legal AutoCAD symbol (layer, block, dimstyle, textstyle, group...).
    /// Throws ArgumentException on failure with a clear message; see rule 28 traps #2 and #6.
    /// </summary>
    public static void ValidateSymbolName(string name, string what)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException($"{what} name cannot be empty.");
        if (name.Length > 255)
            throw new ArgumentException($"{what} name exceeds 255 characters.");
        try
        {
            SymbolUtilityServices.ValidateSymbolName(name, allowVerticalBar: false);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"{what} name '{name}' is not a valid AutoCAD symbol name: {ex.Message}");
        }
    }

    /// <summary>Resolve a linetype by name (must already be loaded in the linetype table).</summary>
    public static ObjectId ResolveLinetype(Database db, Transaction tr, string name)
    {
        var lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
        if (!lt.Has(name))
            throw new ArgumentException($"Linetype '{name}' is not loaded. Load it first via LINETYPE command or _-LINETYPE Load.");
        return lt[name];
    }

    /// <summary>Resolve a text style by name; falls back to "Standard" if missing.</summary>
    public static ObjectId ResolveTextStyleOrStandard(Database db, Transaction tr, string? name)
    {
        var ts = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
        if (!string.IsNullOrWhiteSpace(name) && ts.Has(name)) return ts[name];
        return ts["Standard"];
    }

    /// <summary>Resolve a dimension style by name; falls back to current Dimstyle if missing.</summary>
    public static ObjectId ResolveDimStyleOrCurrent(Database db, Transaction tr, string? name)
    {
        var dst = (DimStyleTable)tr.GetObject(db.DimStyleTableId, OpenMode.ForRead);
        if (!string.IsNullOrWhiteSpace(name) && dst.Has(name)) return dst[name];
        return db.Dimstyle;
    }

    /// <summary>Open a layer (read-only) by name; throws ArgumentException if missing.</summary>
    public static LayerTableRecord ResolveLayer(Database db, Transaction tr, string name, OpenMode mode = OpenMode.ForRead)
    {
        var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
        if (!lt.Has(name)) throw new ArgumentException($"Layer '{name}' does not exist.");
        return (LayerTableRecord)tr.GetObject(lt[name], mode);
    }
}
