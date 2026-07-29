// AutoCAD plugin handlers for the acad-annotations category.
// Registered under "acad.annotations.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern),
//        27 (text & table traps).

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

internal static class AnnotationsPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.annotations.add_dbtext",            AddDBText);
        host.Register("acad.annotations.update_dbtext",         UpdateDBText);
        host.Register("acad.annotations.add_mtext",             AddMText);
        host.Register("acad.annotations.update_mtext",          UpdateMText);
        host.Register("acad.annotations.add_mleader_text",      AddMLeaderText);
        host.Register("acad.annotations.add_mleader_block",     AddMLeaderBlock);
        host.Register("acad.annotations.add_table",             AddTable);
        host.Register("acad.annotations.set_table_cell",        SetTableCell);
        host.Register("acad.annotations.create_text_style",     CreateTextStyle);
        host.Register("acad.annotations.set_current_text_style", SetCurrentTextStyle);
        host.Register("acad.annotations.list_text_styles",      ListTextStyles);
        host.Register("acad.annotations.delete_text_style",     DeleteTextStyle);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct, Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    // ─────────── DBText ───────────

    private static AttachmentPoint ParseAttachment(string? a) => (a ?? "TopLeft") switch
    {
        "TopLeft"      => AttachmentPoint.TopLeft,
        "TopCenter"    => AttachmentPoint.TopCenter,
        "TopRight"     => AttachmentPoint.TopRight,
        "MiddleLeft"   => AttachmentPoint.MiddleLeft,
        "MiddleCenter" => AttachmentPoint.MiddleCenter,
        "MiddleRight"  => AttachmentPoint.MiddleRight,
        "BottomLeft"   => AttachmentPoint.BottomLeft,
        "BottomCenter" => AttachmentPoint.BottomCenter,
        "BottomRight"  => AttachmentPoint.BottomRight,
        _ => throw new ArgumentException($"attachmentPoint '{a}' is not one of TopLeft/TopCenter/TopRight/MiddleLeft/MiddleCenter/MiddleRight/BottomLeft/BottomCenter/BottomRight.")
    };

    private static (TextHorizontalMode h, TextVerticalMode v)? ParseAlignment(string? align) => (align ?? "Left") switch
    {
        "Left"         => (TextHorizontalMode.TextLeft,    TextVerticalMode.TextBase),
        "Center"       => (TextHorizontalMode.TextCenter,  TextVerticalMode.TextBase),
        "Right"        => (TextHorizontalMode.TextRight,   TextVerticalMode.TextBase),
        "Middle"       => (TextHorizontalMode.TextMid,     TextVerticalMode.TextBase),
        "BaseLeft"     => (TextHorizontalMode.TextLeft,    TextVerticalMode.TextBase),
        "BaseCenter"   => (TextHorizontalMode.TextCenter,  TextVerticalMode.TextBase),
        "BaseRight"    => (TextHorizontalMode.TextRight,   TextVerticalMode.TextBase),
        "TopLeft"      => (TextHorizontalMode.TextLeft,    TextVerticalMode.TextTop),
        "TopCenter"    => (TextHorizontalMode.TextCenter,  TextVerticalMode.TextTop),
        "TopRight"     => (TextHorizontalMode.TextRight,   TextVerticalMode.TextTop),
        "BottomLeft"   => (TextHorizontalMode.TextLeft,    TextVerticalMode.TextBottom),
        "BottomCenter" => (TextHorizontalMode.TextCenter,  TextVerticalMode.TextBottom),
        "BottomRight"  => (TextHorizontalMode.TextRight,   TextVerticalMode.TextBottom),
        _ => throw new ArgumentException($"alignment '{align}' is not recognised.")
    };

    private static Task<ToolDispatchResult> AddDBText(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.add_dbtext", args, ct, (doc, db, tr) =>
        {
            var a = Read<AddDBTextArgsDto>(args);
            if (a.Height <= 0) throw new ArgumentException("height must be > 0.");
            var t = new DBText
            {
                Position    = AcadEnv.ToPoint3d(a.Position),
                Height      = a.Height,
                TextString  = a.Contents ?? "",
                Rotation    = a.RotationDeg * Math.PI / 180.0,
                TextStyleId = AcadEnv.ResolveTextStyleOrStandard(db, tr, a.TextStyle),
            };
            var hv = ParseAlignment(a.Alignment);
            if (hv.HasValue && (hv.Value.h != TextHorizontalMode.TextLeft || hv.Value.v != TextVerticalMode.TextBase))
            {
                t.HorizontalMode = hv.Value.h;
                t.VerticalMode   = hv.Value.v;
                // For non-default alignment, AlignmentPoint controls placement (trap #1 rule 27).
                t.AlignmentPoint = AcadEnv.ToPoint3d(a.Position);
            }
            return Wrap(new { entity = AcadEnv.Persist(db, tr, t, a.Layer) });
        });

    private static Task<ToolDispatchResult> UpdateDBText(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.update_dbtext", args, ct, (doc, db, tr) =>
        {
            var a = Read<UpdateDBTextArgsDto>(args);
            var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle), OpenMode.ForWrite);
            if (ent is not DBText t)
                throw new ArgumentException($"Handle '{a.Handle}' is a {ent.GetRXClass().Name}, not DBText. For MText use update_mtext.");
            t.TextString = a.Contents ?? "";
            return Wrap(new { entity = AcadEnv.ToHandle(t) });
        });

    // ─────────── MText ───────────

    private static Task<ToolDispatchResult> AddMText(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.add_mtext", args, ct, (doc, db, tr) =>
        {
            var a = Read<AddMTextArgsDto>(args);
            if (a.TextHeight <= 0) throw new ArgumentException("textHeight must be > 0.");
            var m = new MText
            {
                Location        = AcadEnv.ToPoint3d(a.Position),
                Contents        = a.Contents ?? "",
                TextHeight      = a.TextHeight,
                Width           = a.Width,                       // 0 = auto-width (rule 27 trap #2)
                Rotation        = a.RotationDeg * Math.PI / 180.0,
                Attachment      = ParseAttachment(a.AttachmentPoint),
                TextStyleId     = AcadEnv.ResolveTextStyleOrStandard(db, tr, a.TextStyle),
            };
            return Wrap(new { entity = AcadEnv.Persist(db, tr, m, a.Layer) });
        });

    private static Task<ToolDispatchResult> UpdateMText(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.update_mtext", args, ct, (doc, db, tr) =>
        {
            var a = Read<UpdateMTextArgsDto>(args);
            var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle), OpenMode.ForWrite);
            if (ent is not MText m)
                throw new ArgumentException($"Handle '{a.Handle}' is a {ent.GetRXClass().Name}, not MText. For DBText use update_dbtext.");
            m.Contents = a.Contents ?? "";
            return Wrap(new { entity = AcadEnv.ToHandle(m) });
        });

    // ─────────── MLeader ───────────

    private static Task<ToolDispatchResult> AddMLeaderText(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.add_mleader_text", args, ct, (doc, db, tr) =>
        {
            var a = Read<AddMLeaderArgsDto>(args);
            if (a.TextHeight <= 0) throw new ArgumentException("textHeight must be > 0.");
            // Trap #6 (rule 27): MLeader needs leader, then leader-line, then vertex; THEN content.
            var ml = new MLeader();
            ml.SetDatabaseDefaults();
            ml.ContentType = ContentType.MTextContent;
            int leaderIdx = ml.AddLeader();
            int lineIdx   = ml.AddLeaderLine(leaderIdx);
            ml.AddFirstVertex(lineIdx, AcadEnv.ToPoint3d(a.ArrowTip));
            ml.AddLastVertex(lineIdx,  AcadEnv.ToPoint3d(a.TextPosition));
            ml.EnableDogleg = a.EnableDogleg;

            var mt = new MText
            {
                Contents   = a.Contents ?? "",
                Location   = AcadEnv.ToPoint3d(a.TextPosition),
                TextHeight = a.TextHeight,
            };
            ml.MText = mt;
            return Wrap(new { entity = AcadEnv.Persist(db, tr, ml, a.Layer) });
        });

    private static Task<ToolDispatchResult> AddMLeaderBlock(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.add_mleader_block", args, ct, (doc, db, tr) =>
        {
            var a = Read<AddBlockMLeaderArgsDto>(args);
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            if (!bt.Has(a.BlockName))
                throw new ArgumentException($"Block '{a.BlockName}' is not defined. Define it first via acad.blocks.define_block.");

            var ml = new MLeader();
            ml.SetDatabaseDefaults();
            ml.ContentType = ContentType.BlockContent;
            int leaderIdx = ml.AddLeader();
            int lineIdx   = ml.AddLeaderLine(leaderIdx);
            ml.AddFirstVertex(lineIdx, AcadEnv.ToPoint3d(a.ArrowTip));
            ml.AddLastVertex(lineIdx,  AcadEnv.ToPoint3d(a.BlockPosition));
            ml.BlockContentId = bt[a.BlockName];
            ml.BlockPosition  = AcadEnv.ToPoint3d(a.BlockPosition);
            ml.BlockScale     = new Scale3d(a.Scale, a.Scale, a.Scale);
            return Wrap(new { entity = AcadEnv.Persist(db, tr, ml, a.Layer) });
        });

    // ─────────── Tables ───────────

    private static Task<ToolDispatchResult> AddTable(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.add_table", args, ct, (doc, db, tr) =>
        {
            var a = Read<AddTableArgsDto>(args);
            if (a.Rows <= 0 || a.Cols <= 0) throw new ArgumentException("rows and cols must be > 0.");
            var table = new Table();
            table.SetDatabaseDefaults();
            table.SetSize(a.Rows, a.Cols);
            for (int r = 0; r < a.Rows; r++) table.Rows[r].Height = a.RowHeight;
            for (int c = 0; c < a.Cols; c++) table.Columns[c].Width = a.ColWidth;
            table.Position = AcadEnv.ToPoint3d(a.Position);
            if (!string.IsNullOrWhiteSpace(a.TextStyle))
            {
                // Apply chosen TextStyle to every cell (Table itself has no TextStyleId on this SDK).
                var tsId = AcadEnv.ResolveTextStyleOrStandard(db, tr, a.TextStyle);
                for (int r = 0; r < a.Rows; r++)
                    for (int c = 0; c < a.Cols; c++)
                        table.Cells[r, c].TextStyleId = tsId;
            }
            // Fill cells from data array if present.
            if (a.Data is not null)
            {
                for (int r = 0; r < Math.Min(a.Rows, a.Data.Count); r++)
                {
                    var row = a.Data[r];
                    if (row is null) continue;
                    for (int c = 0; c < Math.Min(a.Cols, row.Count); c++)
                    {
                        table.Cells[r, c].TextString = row[c] ?? "";
                    }
                }
            }
            // Trap #8 (rule 27): GenerateLayout finalises geometry. Must be called BEFORE Append.
            table.GenerateLayout();
            return Wrap(new { entity = AcadEnv.Persist(db, tr, table, a.Layer) });
        });

    private static Task<ToolDispatchResult> SetTableCell(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.set_table_cell", args, ct, (doc, db, tr) =>
        {
            var a = Read<SetTableCellArgsDto>(args);
            var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle), OpenMode.ForWrite);
            if (ent is not Table t)
                throw new ArgumentException($"Handle '{a.Handle}' is a {ent.GetRXClass().Name}, not Table.");
            if (a.Row < 0 || a.Row >= t.Rows.Count) throw new ArgumentException($"row {a.Row} is out of range (0..{t.Rows.Count - 1}).");
            if (a.Col < 0 || a.Col >= t.Columns.Count) throw new ArgumentException($"col {a.Col} is out of range (0..{t.Columns.Count - 1}).");
            t.Cells[a.Row, a.Col].TextString = a.Contents ?? "";
            t.GenerateLayout();
            return Wrap(new { entity = AcadEnv.ToHandle(t) });
        });

    // ─────────── Text styles ───────────

    private static Task<ToolDispatchResult> CreateTextStyle(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.create_text_style", args, ct, (doc, db, tr) =>
        {
            var a = Read<CreateTextStyleArgsDto>(args);
            AcadEnv.ValidateSymbolName(a.Name, "TextStyle");
            if (string.IsNullOrWhiteSpace(a.Font)) throw new ArgumentException("font (TTF face name or .shx) is required.");
            var ts = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForWrite);
            if (ts.Has(a.Name))
            {
                return Wrap(new { name = a.Name });
            }
            var rec = new TextStyleTableRecord
            {
                Name              = a.Name,
                TextSize          = a.Height,
                XScale            = a.WidthFactor <= 0 ? 1.0 : a.WidthFactor,
                ObliquingAngle    = a.ObliqueDeg * Math.PI / 180.0,
            };
            // Choose between .shx (FileName) or TTF (Font).
            if (a.Font.EndsWith(".shx", StringComparison.OrdinalIgnoreCase))
            {
                rec.FileName = a.Font;
            }
            else
            {
                rec.Font = new Autodesk.AutoCAD.GraphicsInterface.FontDescriptor(
                    a.Font, false, false, 0, 0);
            }
            ts.Add(rec);
            tr.AddNewlyCreatedDBObject(rec, true);
            return Wrap(new { name = a.Name });
        });

    private static Task<ToolDispatchResult> SetCurrentTextStyle(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.set_current_text_style", args, ct, (doc, db, tr) =>
        {
            var a = Read<TextStyleNameArgDto>(args);
            var ts = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (!ts.Has(a.Name)) throw new ArgumentException($"Text style '{a.Name}' does not exist.");
            db.Textstyle = ts[a.Name];
            return Wrap(new { name = a.Name });
        });

    private static Task<ToolDispatchResult> ListTextStyles(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.list_text_styles", args, ct, (doc, db, tr) =>
        {
            var ts = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            var styles = new List<string>();
            foreach (ObjectId id in ts)
            {
                var rec = (TextStyleTableRecord)tr.GetObject(id, OpenMode.ForRead);
                styles.Add(rec.Name);
            }
            string current;
            try
            {
                var cur = (TextStyleTableRecord)tr.GetObject(db.Textstyle, OpenMode.ForRead);
                current = cur.Name;
            }
            catch { current = "Standard"; }
            return Wrap(new { styles, current });
        });

    private static Task<ToolDispatchResult> DeleteTextStyle(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.delete_text_style", args, ct, (doc, db, tr) =>
        {
            var a = Read<TextStyleNameArgDto>(args);
            if (string.Equals(a.Name, "Standard", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Text style 'Standard' is protected and cannot be deleted.");
            var ts = (TextStyleTable)tr.GetObject(db.TextStyleTableId, OpenMode.ForRead);
            if (!ts.Has(a.Name)) return Wrap(new { affected = 0 });
            var rec = (TextStyleTableRecord)tr.GetObject(ts[a.Name], OpenMode.ForWrite);
            rec.Erase(true);
            return Wrap(new { affected = 1 });
        });
}
