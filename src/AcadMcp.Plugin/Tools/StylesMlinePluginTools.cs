// AutoCAD plugin handlers for multiline (MLINE) styles — roadmap 2.3.
//
// Split into its own file rather than added to StylesPluginTools because an MLINE style is a
// different shape from the three style types that live there. Those are bags of scalar
// properties driven by a catalogue in AcadMcp.Shared.Catalogs; this one is an ordered list of
// parallel line ELEMENTS, each with an offset from the centreline, a colour and a linetype.
// There is nothing to advertise with a list_mlinestyle_properties, because there is no property
// catalogue — which is exactly why it does not exist.
//
// Two facts about MLINE styles drive most of the code below:
//
//  1. A style that entities reference cannot be redefined. AutoCAD raises on the attempt, so
//     both write paths check first and refuse with a sentence that says what to do instead.
//     Reporting success over a change AutoCAD rejected is the failure mode that shipped in
//     publish.import_page_setup, and it is not repeated here.
//  2. Mitre angles are clamped to 10..170 degrees. Outside that AutoCAD throws eInvalidInput,
//     which tells a caller nothing. Checked here so the error names the argument.

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

namespace AcadMcp.Plugin.Tools;

internal static class StylesMlinePluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.styles.list_mlinestyles", ListMlineStyles);
        host.Register("acad.styles.create_mlinestyle", CreateMlineStyle);
        host.Register("acad.styles.modify_mlinestyle", ModifyMlineStyle);
    }

    private static T Read<T>(JsonObject a) => JsonSerializer.Deserialize<T>(a, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static DBDictionary MlineDict(Database db, Transaction tr, OpenMode mode)
        => (DBDictionary)tr.GetObject(db.MLStyleDictionaryId, mode);

    // ─────────── caps ───────────

    private static string CapName(MlineStyle s, bool start) => start
        ? (s.StartRoundCap ? "round" : s.StartSquareCap ? "square" : s.StartInnerArcs ? "innerArcs" : "none")
        : (s.EndRoundCap ? "round" : s.EndSquareCap ? "square" : s.EndInnerArcs ? "innerArcs" : "none");

    private static void SetCap(MlineStyle s, string? cap, bool start)
    {
        if (cap is null) return;
        bool round = false, square = false, arcs = false;
        switch (cap.Trim().ToLowerInvariant())
        {
            case "none": break;
            case "round": round = true; break;
            case "square": case "line": square = true; break;
            case "innerarcs": case "inner": arcs = true; break;
            default:
                throw new ArgumentException(
                    "Unknown cap '" + cap + "'. Use one of: none, round, square, innerArcs.");
        }
        if (start) { s.StartRoundCap = round; s.StartSquareCap = square; s.StartInnerArcs = arcs; }
        else { s.EndRoundCap = round; s.EndSquareCap = square; s.EndInnerArcs = arcs; }
    }

    /// <summary>Does any MLINE entity reference this style? If so AutoCAD forbids redefining it.</summary>
    private static bool StyleInUse(Database db, Transaction tr, ObjectId styleId)
    {
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        foreach (ObjectId btrId in bt)
        {
            var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
            foreach (ObjectId entId in btr)
            {
                if (entId.ObjectClass.DxfName != "MLINE") continue;
                if (tr.GetObject(entId, OpenMode.ForRead) is Mline m && m.Style == styleId) return true;
            }
        }
        return false;
    }

    private static void ApplyElements(MlineStyle style, Database db, Transaction tr,
                                      IReadOnlyList<MlineElementSpecDto> specs)
    {
        if (specs.Count < 1)
            throw new ArgumentException(
                "elements must contain at least one line; a style with no elements draws nothing.");

        var lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);

        // Replace wholesale rather than merge. The elements are an ordered geometric set, so a
        // partial merge has no meaning - "change the second one" is not a thing a caller can mean
        // when the offsets decide the ordering.
        while (style.Elements.Count > 0) style.Elements.RemoveAt(0);

        // AutoCAD stores elements outermost-first. Sorting here means a caller can pass them in
        // any order and get the same style back.
        foreach (var spec in specs.OrderByDescending(e => e.Offset))
        {
            var colour = spec.ColorIndex is int ci
                ? Color.FromColorIndex(ColorMethod.ByAci, (short)ci)
                : Color.FromColorIndex(ColorMethod.ByBlock, 0);

            var ltId = db.ByLayerLinetype;
            if (!string.IsNullOrWhiteSpace(spec.Linetype))
            {
                if (!lt.Has(spec.Linetype))
                    throw new ArgumentException(
                        "Linetype '" + spec.Linetype + "' is not loaded in this drawing. " +
                        "Load it first, or omit it to inherit ByLayer.");
                ltId = lt[spec.Linetype];
            }
            style.Elements.Add(new MlineStyleElement(spec.Offset, colour, ltId), true);
        }
    }

    /// <summary>AutoCAD's own default mitre angle. A freshly constructed MlineStyle does NOT have it.</summary>
    private const double DefaultMitreDegrees = 90.0;

    /// <summary>
    /// Give a newly constructed style AutoCAD's default mitre angles before anything else is
    /// applied, so an explicit startAngle/endAngle from the caller still wins.
    /// </summary>
    /// <remarks>
    /// This is load-bearing and was found by looking at the drawing rather than at a return code.
    /// <c>new MlineStyle()</c> leaves StartAngle and EndAngle at 0, where AutoCAD's own default
    /// is 90 degrees. A mitre angle of zero means the end of the multiline is cut PARALLEL to its
    /// own direction, which never closes: an open MLINE drawn with such a style runs off to
    /// roughly offset x 10,000 units. A 200mm wall six metres long came back with a bounding box
    /// 2,006,000 units wide.
    ///
    /// Every call still returned healthy JSON, every property read back correctly, and 35 of 36
    /// assertions passed. Only the exported PNG showed it - the drawing was one enormous arc.
    /// Closed multilines are unaffected, because they have no ends to cap, which is why the room
    /// in that same test was pixel-correct while every open run was not.
    /// </remarks>
    private static void ApplyAutocadDefaults(MlineStyle s)
    {
        s.StartAngle = DefaultMitreDegrees * Math.PI / 180.0;
        s.EndAngle = DefaultMitreDegrees * Math.PI / 180.0;
    }

    private static void ApplyScalars(MlineStyle s, string? description, bool? showMiters,
                                     double? startAngle, double? endAngle,
                                     string? startCap, string? endCap, int? fillColorIndex)
    {
        if (description is not null) s.Description = description;
        if (showMiters is bool sm) s.ShowMiters = sm;

        if (startAngle is double sa)
        {
            if (sa < 10 || sa > 170)
                throw new ArgumentException(
                    "startAngle " + sa + " is outside AutoCAD's 10..170 degree range for mitre angles.");
            s.StartAngle = sa * Math.PI / 180.0;
        }
        if (endAngle is double ea)
        {
            if (ea < 10 || ea > 170)
                throw new ArgumentException(
                    "endAngle " + ea + " is outside AutoCAD's 10..170 degree range for mitre angles.");
            s.EndAngle = ea * Math.PI / 180.0;
        }

        SetCap(s, startCap, start: true);
        SetCap(s, endCap, start: false);

        // A negative index is the documented way to say "no fill", since Filled and FillColor are
        // two properties and leaving them inconsistent produces a style that reports a fill
        // colour it does not draw.
        if (fillColorIndex is int fc)
        {
            s.Filled = fc >= 0;
            if (fc >= 0) s.FillColor = Color.FromColorIndex(ColorMethod.ByAci, (short)fc);
        }
    }

    private static object Info(Database db, Transaction tr, string name, MlineStyle s, bool inUse)
    {
        var els = new List<object>();
        double min = double.MaxValue, max = double.MinValue;
        foreach (MlineStyleElement e in s.Elements)
        {
            min = Math.Min(min, e.Offset);
            max = Math.Max(max, e.Offset);
            els.Add(new
            {
                offset = e.Offset,
                colorIndex = e.Color.ColorMethod == ColorMethod.ByAci ? (int?)e.Color.ColorIndex : null,
                linetype = e.LinetypeId.IsNull
                    ? null
                    : ((LinetypeTableRecord)tr.GetObject(e.LinetypeId, OpenMode.ForRead)).Name,
            });
        }

        return new
        {
            name,
            description = s.Description ?? "",
            elements = els,
            totalWidth = s.Elements.Count == 0 ? 0d : max - min,
            showMiters = s.ShowMiters,
            // Reported in degrees, and reported at all because their absence is what let a
            // degenerate 0-degree default ship undetected: every other field read back perfectly
            // while the drawing was two million units wide.
            startAngle = Math.Round(s.StartAngle * 180.0 / Math.PI, 3),
            endAngle = Math.Round(s.EndAngle * 180.0 / Math.PI, 3),
            startCap = CapName(s, true),
            endCap = CapName(s, false),
            filled = s.Filled,
            inUse,
        };
    }

    // ─────────── tools ───────────

    private static Task<ToolDispatchResult> ListMlineStyles(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunReadAsync("acad.styles.list_mlinestyles", ct, (doc, db, tr) =>
        {
            var dict = MlineDict(db, tr, OpenMode.ForRead);
            var styles = new List<object>();
            foreach (DBDictionaryEntry e in dict)
            {
                var s = (MlineStyle)tr.GetObject(e.Value, OpenMode.ForRead);
                styles.Add(Info(db, tr, e.Key, s, StyleInUse(db, tr, e.Value)));
            }
            return Wrap(new { styles, count = styles.Count });
        });

    private static Task<ToolDispatchResult> CreateMlineStyle(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.styles.create_mlinestyle", ct, (doc, db, tr) =>
        {
            var a = Read<CreateMlineStyleArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name)) throw new ArgumentException("name is required.");
            AcadEnv.ValidateSymbolName(a.Name, "MlineStyle");
            if (a.Elements is null || a.Elements.Count == 0)
                throw new ArgumentException(
                    "elements is required: an MLINE style is defined by its parallel lines. " +
                    "A 200mm wall is [{offset:100},{offset:-100}].");

            var dict = MlineDict(db, tr, OpenMode.ForWrite);
            foreach (DBDictionaryEntry e in dict)
            {
                if (!string.Equals(e.Key, a.Name, StringComparison.OrdinalIgnoreCase)) continue;
                if (!a.Overwrite)
                    throw new ArgumentException(
                        "A multiline style named '" + e.Key + "' already exists. Pass overwrite:true, " +
                        "or use modify_mlinestyle to change part of it.");
                if (StyleInUse(db, tr, e.Value))
                    throw new InvalidOperationException(
                        "Multiline style '" + e.Key + "' is used by MLINE entities in this drawing and " +
                        "AutoCAD will not allow it to be redefined. Create it under a different name, or " +
                        "erase those entities first. list_mlinestyles reports inUse for this reason.");

                var existing = (MlineStyle)tr.GetObject(e.Value, OpenMode.ForWrite);
                ApplyAutocadDefaults(existing);
                ApplyElements(existing, db, tr, a.Elements);
                ApplyScalars(existing, a.Description, a.ShowMiters, a.StartAngle, a.EndAngle,
                             a.StartCap, a.EndCap, a.FillColorIndex);
                return Wrap(new { mlineStyle = Info(db, tr, e.Key, existing, false), created = false });
            }

            var style = new MlineStyle { Name = a.Name };
            ApplyAutocadDefaults(style);
            ApplyElements(style, db, tr, a.Elements);
            ApplyScalars(style, a.Description, a.ShowMiters, a.StartAngle, a.EndAngle,
                         a.StartCap, a.EndCap, a.FillColorIndex);

            // SetAt then AddNewlyCreatedDBObject, in that order: the object must be owned before
            // the transaction is told about it.
            dict.SetAt(a.Name, style);
            tr.AddNewlyCreatedDBObject(style, true);

            return Wrap(new { mlineStyle = Info(db, tr, a.Name, style, false), created = true });
        });

    private static Task<ToolDispatchResult> ModifyMlineStyle(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.styles.modify_mlinestyle", ct, (doc, db, tr) =>
        {
            var a = Read<ModifyMlineStyleArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name)) throw new ArgumentException("name is required.");

            var dict = MlineDict(db, tr, OpenMode.ForRead);
            if (!dict.Contains(a.Name))
                throw new ArgumentException(
                    "No multiline style named '" + a.Name + "'. Use list_mlinestyles.");

            var id = dict.GetAt(a.Name);
            if (StyleInUse(db, tr, id))
                throw new InvalidOperationException(
                    "Multiline style '" + a.Name + "' is used by MLINE entities in this drawing and " +
                    "AutoCAD will not allow it to be modified. Erase those entities first, or define a " +
                    "new style under another name.");

            var s = (MlineStyle)tr.GetObject(id, OpenMode.ForWrite);
            if (a.Elements is { Count: > 0 }) ApplyElements(s, db, tr, a.Elements);
            ApplyScalars(s, a.Description, a.ShowMiters, a.StartAngle, a.EndAngle,
                         a.StartCap, a.EndCap, a.FillColorIndex);

            return Wrap(new { mlineStyle = Info(db, tr, a.Name, s, false), created = false });
        });
}
