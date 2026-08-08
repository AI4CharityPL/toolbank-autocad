// AutoCAD plugin handlers for the acad-annotations category.
// Registered under "acad.annotations.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern),
//        27 (text & table traps).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcadColor = Autodesk.AutoCAD.Colors.Color;
using AcadRt = Autodesk.AutoCAD.Runtime;

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

        // roadmap 3.3 - finding text across a drawing
        host.Register("acad.annotations.list_text_by_pattern",  ListTextByPattern);
        host.Register("acad.annotations.find_replace_text",     FindReplaceText);
        host.Register("acad.annotations.export_text_content",   ExportTextContent);

        // roadmap 3.3 - where text sits and how big it is
        host.Register("acad.annotations.set_text_justification", SetTextJustification);
        host.Register("acad.annotations.text_fit",               TextFit);
        host.Register("acad.annotations.scale_text_in_place",    ScaleTextInPlace);

        // roadmap 3.3 - how an MText presents itself
        host.Register("acad.annotations.background_mask_mtext",  BackgroundMaskMText);
        host.Register("acad.annotations.mtext_column_settings",  MTextColumnSettings);

        // roadmap 3.3 - symbols and stacked fractions
        host.Register("acad.annotations.insert_symbol",          InsertSymbol);
        host.Register("acad.annotations.stack_fraction",         StackFraction);

        // roadmap 3.3 - converting between text and mtext
        host.Register("acad.annotations.text_to_mtext",          TextToMText);
        host.Register("acad.annotations.explode_mtext_to_text",  ExplodeMTextToText);
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
                Width           = a.WrapWidth ?? a.Width,      // 0 = auto-width (rule 27 trap #2)
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

    // ─────────── roadmap 3.3: finding text across a drawing ───────────
    //
    // Text lives in six different places and a search that only reads DBText and MText will
    // quietly miss most of a real sheet: a room name is MText, a level tag is a block ATTRIBUTE,
    // a note is an MLeader, a schedule is a Table, and a dimension can carry a text override.
    // All six are read here and the per-type counts are reported, so "0 matches" can be told
    // apart from "0 matches in the two types I bothered to look at".
    //
    // The trap that makes replacement different from search: MText.Contents carries FORMATTING
    // CODES - \fArial|b0|i0;, \W0.8;, {\H1.5x;...}. A replacement done blindly on that string can
    // land inside a code and silently change the formatting instead of the words, or corrupt the
    // entity outright. MText.Text is the rendered text and is read-only, so it cannot be written
    // to directly - but it CAN be used to check the result, which is what happens below.

    private sealed class TextSlot
    {
        public string Handle = "";
        public string Type = "";
        public string Layer = "";
        public string Text = "";          // what a reader sees
        public string Raw = "";           // what is stored, codes and all
        public double[] Position = new double[3];
        public Func<string, bool>? Write; // null when this slot is read-only
    }

    /// <summary>Everything in model space that carries text a person would read.</summary>
    private static List<TextSlot> TextSlots(Database db, Transaction tr, string? layerFilter,
                                            IReadOnlyList<string>? only)
    {
        var wanted = only is { Count: > 0 }
            ? new HashSet<string>(only, StringComparer.OrdinalIgnoreCase)
            : null;
        var slots = new List<TextSlot>();

        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

        void Add(Entity owner, string type, string text, string raw, Point3d at,
                 Func<string, bool>? write)
            => slots.Add(new TextSlot
            {
                Handle = owner.Handle.ToString(),
                Type = type,
                Layer = owner.Layer,
                Text = text,
                Raw = raw,
                Position = new[] { at.X, at.Y, at.Z },
                Write = write,
            });

        foreach (ObjectId id in ms)
        {
            if (tr.GetObject(id, OpenMode.ForRead) is not Entity ent) continue;
            if (wanted is not null && !wanted.Contains(ent.Handle.ToString())) continue;
            if (!string.IsNullOrEmpty(layerFilter) &&
                !string.Equals(ent.Layer, layerFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            switch (ent)
            {
                case DBText t:
                    Add(t, "DBText", t.TextString, t.TextString, t.Position, s =>
                    {
                        var w = (DBText)tr.GetObject(t.ObjectId, OpenMode.ForWrite);
                        w.TextString = s;
                        return w.TextString == s;
                    });
                    break;

                case MText m:
                    Add(m, "MText", m.Text, m.Contents, m.Location, s =>
                    {
                        var w = (MText)tr.GetObject(m.ObjectId, OpenMode.ForWrite);
                        w.Contents = s;
                        return w.Contents == s;
                    });
                    break;

                case MLeader ml when ml.ContentType == ContentType.MTextContent:
                {
                    var content = ml.MText;
                    if (content is null) break;
                    Add(ml, "MLeader", content.Text, content.Contents, content.Location, s =>
                    {
                        var w = (MLeader)tr.GetObject(ml.ObjectId, OpenMode.ForWrite);
                        var c = w.MText;
                        if (c is null) return false;
                        c.Contents = s;
                        w.MText = c;   // the MText is a COPY; it has to be assigned back
                        return w.MText?.Contents == s;
                    });
                    break;
                }

                case Dimension d when !string.IsNullOrEmpty(d.DimensionText):
                    Add(d, "Dimension", d.DimensionText, d.DimensionText, d.TextPosition, s =>
                    {
                        var w = (Dimension)tr.GetObject(d.ObjectId, OpenMode.ForWrite);
                        w.DimensionText = s;
                        return w.DimensionText == s;
                    });
                    break;

                // A Table IS a BlockReference in AutoCAD's object model, so this case has
                // to come FIRST - otherwise the block branch swallows every table and the
                // schedule text is never scanned. The compiler catches it here; nothing
                // downstream would have.
                case Table tb:
                    for (int row = 0; row < tb.Rows.Count; row++)
                    for (int col = 0; col < tb.Columns.Count; col++)
                    {
                        string cell;
                        try { cell = tb.Cells[row, col].TextString ?? ""; }
                        catch { continue; }
                        if (cell.Length == 0) continue;
                        int r = row, c2 = col;
                        slots.Add(new TextSlot
                        {
                            Handle = tb.Handle.ToString(),
                            Type = "TableCell:" + r + "," + c2,
                            Layer = tb.Layer,
                            Text = cell,
                            Raw = cell,
                            Position = new[] { tb.Position.X, tb.Position.Y, tb.Position.Z },
                            Write = s =>
                            {
                                var w = (Table)tr.GetObject(tb.ObjectId, OpenMode.ForWrite);
                                w.Cells[r, c2].TextString = s;
                                return w.Cells[r, c2].TextString == s;
                            },
                        });
                    }
                    break;

                case BlockReference br:
                    foreach (ObjectId attId in br.AttributeCollection)
                    {
                        if (tr.GetObject(attId, OpenMode.ForRead) is not AttributeReference at)
                            continue;
                        var tag = at.Tag;
                        slots.Add(new TextSlot
                        {
                            Handle = at.Handle.ToString(),
                            Type = "Attribute:" + tag,
                            Layer = br.Layer,
                            Text = at.TextString,
                            Raw = at.TextString,
                            Position = new[] { at.Position.X, at.Position.Y, at.Position.Z },
                            Write = s =>
                            {
                                var w = (AttributeReference)tr.GetObject(at.ObjectId,
                                                                         OpenMode.ForWrite);
                                w.TextString = s;
                                return w.TextString == s;
                            },
                        });
                    }
                    break;
            }
        }
        return slots;
    }

    /// <summary>A compiled matcher for the pattern options the two search tools share.</summary>
    private static Regex BuildMatcher(string pattern, bool isRegex, bool matchCase, bool wholeWord)
    {
        var opts = matchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
        var body = isRegex ? pattern : Regex.Escape(pattern);
        if (wholeWord) body = @"\b(?:" + body + @")\b";
        try { return new Regex(body, opts); }
        catch (ArgumentException ex)
        {
            throw new ArgumentException(
                "The pattern is not a valid regular expression: " + ex.Message +
                ". Drop regex: true to search for it as literal text instead.");
        }
    }

    private static Task<ToolDispatchResult> ListTextByPattern(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunReadAsync("acad.annotations.list_text_by_pattern", ct, (doc, db, tr) =>
        {
            var a = Read<TextSearchArgsDto>(args);
            if (string.IsNullOrEmpty(a.Pattern))
                throw new ArgumentException(
                    "pattern is required. Pass regex: true to treat it as a regular expression; " +
                    "by default it is literal text.");

            var rx = BuildMatcher(a.Pattern!, a.Regex == true, a.MatchCase == true,
                                  a.WholeWord == true);
            var limit = a.Limit ?? 500;
            if (limit < 1) throw new ArgumentException("limit must be at least 1.");

            var slots = TextSlots(db, tr, a.LayerFilter, a.Handles);
            var byType = new Dictionary<string, int>(StringComparer.Ordinal);
            var hits = new List<object>();
            int total = 0;

            foreach (var s in slots)
            {
                var kind = s.Type.Split(':')[0];
                byType[kind] = byType.TryGetValue(kind, out var n) ? n + 1 : 1;
                // Matched against the RENDERED text, not the stored string. A search over
                // MText.Contents would hit "Arial" inside a font code and report a match nobody
                // can see on the sheet.
                var m = rx.Matches(s.Text);
                if (m.Count == 0) continue;
                total += m.Count;
                if (hits.Count < limit)
                    hits.Add(new
                    {
                        handle = s.Handle,
                        type = s.Type,
                        layer = s.Layer,
                        text = s.Text,
                        occurrences = m.Count,
                        position = s.Position,
                    });
            }

            return Wrap(new
            {
                pattern = a.Pattern,
                regex = a.Regex == true,
                matchCase = a.MatchCase == true,
                wholeWord = a.WholeWord == true,
                scanned = slots.Count,
                scannedByType = byType,
                matched = hits.Count,
                occurrences = total,
                truncated = total > 0 && hits.Count >= limit,
                results = hits,
                note = "Matched against the RENDERED text, not the stored string - a search over " +
                       "MText.Contents would hit words inside formatting codes and report a match " +
                       "nobody can see. scannedByType says what was actually looked at, so no " +
                       "matches in a drawing full of text can be told from no matches at all.",
            });
        });

    private static Task<ToolDispatchResult> FindReplaceText(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.find_replace_text", args, ct, (doc, db, tr) =>
        {
            var a = Read<FindReplaceArgsDto>(args);
            if (string.IsNullOrEmpty(a.Find))
                throw new ArgumentException("find is required: the text to look for.");
            if (a.ReplaceWith is null)
                throw new ArgumentException(
                    "replaceWith is required. To search without changing anything, use " +
                    "list_text_by_pattern, or pass dryRun: true here to see what would change.");

            var rx = BuildMatcher(a.Find!, a.Regex == true, a.MatchCase == true,
                                  a.WholeWord == true);
            var dry = a.DryRun == true;
            var slots = TextSlots(db, tr, a.LayerFilter, a.Handles);

            var changed = new List<object>();
            var skipped = new List<object>();
            int occurrences = 0;

            foreach (var s in slots)
            {
                var hits = rx.Matches(s.Text);
                if (hits.Count == 0) continue;

                if (s.Write is null)
                {
                    skipped.Add(new { handle = s.Handle, type = s.Type, reason = "read-only" });
                    continue;
                }

                // Replace in the STORED string, which is what has to be written back, but only
                // after checking the stored and rendered strings agree. When an MText carries
                // formatting codes the two differ, and a blind replacement on the stored string
                // can land inside a code - changing the font instead of the words, or breaking
                // the entity. Those are reported rather than attempted.
                if (!string.Equals(s.Raw, s.Text, StringComparison.Ordinal))
                {
                    var wouldBe = rx.Replace(s.Raw, a.ReplaceWith!);
                    var rawHits = rx.Matches(s.Raw).Count;
                    if (rawHits != hits.Count)
                    {
                        skipped.Add(new
                        {
                            handle = s.Handle,
                            type = s.Type,
                            reason = "this text carries MText formatting codes, and the pattern " +
                                     "matches " + rawHits + " time(s) in the stored string against " +
                                     hits.Count + " in what is rendered - replacing would change " +
                                     "a formatting code rather than the words. Edit it with " +
                                     "update_mtext, or narrow the pattern.",
                            renderedText = s.Text,
                        });
                        continue;
                    }
                    if (!dry && !s.Write(wouldBe))
                    {
                        skipped.Add(new
                        {
                            handle = s.Handle, type = s.Type,
                            reason = "the new text did not read back after being written",
                        });
                        continue;
                    }
                    occurrences += hits.Count;
                    changed.Add(new
                    {
                        handle = s.Handle, type = s.Type, layer = s.Layer,
                        before = s.Text, after = rx.Replace(s.Text, a.ReplaceWith!),
                        occurrences = hits.Count, hadFormattingCodes = true,
                    });
                    continue;
                }

                var after = rx.Replace(s.Raw, a.ReplaceWith!);
                if (!dry && !s.Write(after))
                {
                    skipped.Add(new
                    {
                        handle = s.Handle, type = s.Type,
                        reason = "the new text did not read back after being written",
                    });
                    continue;
                }
                occurrences += hits.Count;
                changed.Add(new
                {
                    handle = s.Handle, type = s.Type, layer = s.Layer,
                    before = s.Text, after, occurrences = hits.Count,
                    hadFormattingCodes = false,
                });
            }

            return Wrap(new
            {
                find = a.Find,
                replaceWith = a.ReplaceWith,
                dryRun = dry,
                scanned = slots.Count,
                entitiesChanged = changed.Count,
                occurrences,
                changed,
                skipped,
                note = (dry
                        ? "dryRun: NOTHING was written. This is what would change. "
                        : "Every write was read back; anything that did not take is on the " +
                          "skipped list rather than counted. ") +
                       "Text carrying MText formatting codes is only touched when the pattern " +
                       "matches the same number of times in the stored string as in the rendered " +
                       "one - otherwise the replacement would be landing inside a code, and it " +
                       "is skipped with that reason instead.",
            });
        });

    private static Task<ToolDispatchResult> ExportTextContent(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunReadAsync("acad.annotations.export_text_content", ct, (doc, db, tr) =>
        {
            var a = Read<ExportTextArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Path))
                throw new ArgumentException("path is required: where to write the file.");

            var fmt = (a.Format ?? "csv").Trim().ToLowerInvariant();
            if (fmt is not ("csv" or "txt"))
                throw new ArgumentException(
                    "format must be csv or txt. csv carries the handle, type, layer and text of " +
                    "every item; txt is the text alone, one item per line.");

            var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(a.Path!));
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                throw new ArgumentException("The folder " + dir + " does not exist.");

            var slots = TextSlots(db, tr, a.LayerFilter, null);
            var sb = new System.Text.StringBuilder();
            if (fmt == "csv")
            {
                sb.AppendLine("handle,type,layer,text");
                foreach (var s in slots)
                    sb.AppendLine(string.Join(",", Csv(s.Handle), Csv(s.Type), Csv(s.Layer),
                                              Csv(s.Text)));
            }
            else
            {
                foreach (var s in slots) sb.AppendLine(s.Text);
            }
            System.IO.File.WriteAllText(a.Path!, sb.ToString(), new System.Text.UTF8Encoding(true));

            var written = new System.IO.FileInfo(a.Path!);
            if (!written.Exists || written.Length == 0)
                throw new InvalidOperationException(
                    "Nothing arrived at " + a.Path + ", so this is not being reported as a " +
                    "successful export.");

            return Wrap(new
            {
                path = written.FullName,
                format = fmt,
                items = slots.Count,
                bytes = written.Length,
                note = "Written UTF-8 with a byte order mark, so Excel opens it with accented " +
                       "characters intact rather than as mojibake. The text column is what a " +
                       "reader sees, with MText formatting codes already resolved.",
            });
        });

    /// <summary>One CSV field, quoted only when it has to be.</summary>
    private static string Csv(string s) =>
        s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0
            ? s
            : "\"" + s.Replace("\"", "\"\"") + "\"";

    // ─────────── roadmap 3.3: where text sits and how big it is ───────────
    //
    // What these three have in common is that the obvious implementation MOVES THE TEXT, and the
    // result reads as a success either way.
    //
    // Setting DBText.Justify on its own relocates the text, because the justification decides
    // which point of the text sits on the alignment point - change the justification and the
    // same anchor now means somewhere else. AutoCAD's JUSTIFYTEXT exists precisely because the
    // naive version is wrong. So the extent is measured before and after and a text that moved
    // is a failure, not an edit.

    private static readonly Dictionary<string, AttachmentPoint> Justifications =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["TopLeft"] = AttachmentPoint.TopLeft,
            ["TopCenter"] = AttachmentPoint.TopCenter,
            ["TopRight"] = AttachmentPoint.TopRight,
            ["MiddleLeft"] = AttachmentPoint.MiddleLeft,
            ["MiddleCenter"] = AttachmentPoint.MiddleCenter,
            ["MiddleRight"] = AttachmentPoint.MiddleRight,
            ["BottomLeft"] = AttachmentPoint.BottomLeft,
            ["BottomCenter"] = AttachmentPoint.BottomCenter,
            ["BottomRight"] = AttachmentPoint.BottomRight,
            ["BaseLeft"] = AttachmentPoint.BaseLeft,
            ["BaseCenter"] = AttachmentPoint.BaseCenter,
            ["BaseRight"] = AttachmentPoint.BaseRight,
        };

    // An earlier version of this computed where the anchor OUGHT to go, by reading the corner of
    // the extents box that each justification names. It was guessing, and it worked by luck for
    // some of them. Two ways it was wrong, both measured:
    //
    //   BottomRight moved the text by 3.333 - exactly a third of its height. "ANCHOR TEST" is
    //   all capitals, so the bottom of its extents box IS the baseline, while Bottom
    //   justification anchors below the descenders. The box does not tell you where a
    //   justification line is; it tells you where the ink is.
    //
    //   BaseLeft threw eNotApplicable. It is the DEFAULT justification, and AutoCAD uses
    //   Position rather than AlignmentPoint for that one, so assigning the alignment point is
    //   not a thing you may do.
    //
    // So the displacement is MEASURED instead of predicted: set the justification, see where the
    // text landed, and move it back by the difference. That is exact for every justification and
    // needs to know nothing about what any of them mean.

    private static Task<ToolDispatchResult> SetTextJustification(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.set_text_justification", args, ct, (doc, db, tr) =>
        {
            var a = Read<JustifyTextArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0)
                throw new ArgumentException("handles is required: which text to re-justify.");
            if (string.IsNullOrWhiteSpace(a.Justification) ||
                !Justifications.TryGetValue(a.Justification!.Trim(), out var target))
                throw new ArgumentException(
                    "justification must be one of: " +
                    string.Join(", ", Justifications.Keys) + ". The Base row sits on the text's " +
                    "BASELINE, which is where descenders hang below; the Bottom row sits under " +
                    "them.");

            var changed = new List<object>();
            foreach (var h in a.Handles)
            {
                var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForWrite);
                Extents3d before;
                try { before = ent.GeometricExtents; }
                catch
                {
                    throw new ArgumentException(
                        "Entity " + h + " has no extents to re-justify around - it may be empty " +
                        "text.");
                }

                string kind, was;
                switch (ent)
                {
                    case DBText t:
                        kind = "DBText";
                        was = t.Justify.ToString();
                        t.Justify = target;
                        t.AdjustAlignment(db);
                        break;
                    case MText m:
                        kind = "MText";
                        was = m.Attachment.ToString();
                        m.Attachment = target;
                        break;
                    default:
                        throw new ArgumentException(
                            "Entity " + h + " is a " + ent.GetType().Name + ". Justification " +
                            "applies to single-line text and MText.");
                }

                // Where it landed, and the correction back. Measured, not predicted.
                var landed = ent.GeometricExtents;
                var back = before.MinPoint - landed.MinPoint;
                var jumped = back.Length;
                if (jumped > 1e-12) ent.TransformBy(Matrix3d.Displacement(back));

                // The claim of this tool, measured. A naive implementation gets everything else
                // right and leaves the text somewhere else on the sheet.
                var after = ent.GeometricExtents;
                var moved = Math.Max(
                    before.MinPoint.DistanceTo(after.MinPoint),
                    before.MaxPoint.DistanceTo(after.MaxPoint));
                if (moved > 1e-6)
                    throw new InvalidOperationException(
                        "Re-justifying " + h + " moved it by " + moved + ". Changing the " +
                        "justification is meant to change which point anchors the text, not " +
                        "where the text is, so this is not being reported as success.");

                changed.Add(new
                {
                    handle = h, type = kind, justificationBefore = was,
                    justification = target.ToString(),
                    // How far the text jumped when the justification alone was changed, and
                    // therefore how far it had to be put back. Reported because it is the size
                    // of the mistake this tool exists to undo.
                    correctedBy = jumped,
                    movedBy = moved,
                });
            }

            return Wrap(new
            {
                affected = changed.Count,
                justification = target.ToString(),
                items = changed,
                note = "The text has NOT moved - movedBy is the measured proof. What changed is " +
                       "which point anchors it, so later edits and any grip on it work from the " +
                       "new corner. Setting the justification without moving the anchor is what " +
                       "makes text jump across a sheet.",
            });
        });

    private static Task<ToolDispatchResult> TextFit(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.text_fit", args, ct, (doc, db, tr) =>
        {
            var a = Read<TextFitArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Handle))
                throw new ArgumentException("handle is required: the single-line text to fit.");
            if (a.Point1 is null || a.Point2 is null)
                throw new ArgumentException(
                    "point1 and point2 are required: the two points the text has to span.");

            var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, a.Handle!), OpenMode.ForWrite);
            if (ent is not DBText t)
                throw new ArgumentException(
                    "Entity " + a.Handle + " is a " + ent.GetType().Name + ", not single-line " +
                    "text. MText has its own width and wraps instead of stretching - set its " +
                    "Width with add_mtext, or use set_mtext_width.");

            var p1 = AcadEnv.ToPoint3d(a.Point1);
            var p2 = AcadEnv.ToPoint3d(a.Point2);
            var span = p1.DistanceTo(p2);
            if (span < 1e-9)
                throw new ArgumentException(
                    "point1 and point2 are the same point, so there is no distance to fit into.");
            if (string.IsNullOrEmpty(t.TextString))
                throw new ArgumentException(
                    "This text is empty, so there is nothing to stretch between the two points.");

            var heightBefore = t.Height;
            var widthFactorBefore = t.WidthFactor;

            // AutoCAD's own Fit alignment does the work: the text is stretched horizontally to
            // run from Position to AlignmentPoint while its HEIGHT stays put. That last part is
            // what separates a fit from a scale, and it is measured below rather than assumed.
            t.HorizontalMode = TextHorizontalMode.TextFit;
            t.Position = p1;
            t.AlignmentPoint = p2;
            t.AdjustAlignment(db);

            var e = ent.GeometricExtents;
            var fittedWidth = Math.Sqrt(
                Math.Pow(e.MaxPoint.X - e.MinPoint.X, 2) +
                Math.Pow(e.MaxPoint.Y - e.MinPoint.Y, 2));

            if (Math.Abs(t.Height - heightBefore) > 1e-9)
                throw new InvalidOperationException(
                    "Fitting changed the text height from " + heightBefore + " to " + t.Height +
                    ". A fit stretches the text sideways and leaves the height alone - that is " +
                    "the whole difference from scaling it.");

            return Wrap(new
            {
                handle = a.Handle,
                span,
                fittedWidth,
                height = t.Height,
                heightBefore,
                widthFactor = t.WidthFactor,
                widthFactorBefore,
                point1 = new[] { p1.X, p1.Y, p1.Z },
                point2 = new[] { p2.X, p2.Y, p2.Z },
                note = "The text now runs from point1 to point2 and its HEIGHT is unchanged at " +
                       t.Height + " - that is what separates a fit from a scale. The text is " +
                       "left on AutoCAD's Fit alignment, so editing its contents re-stretches it " +
                       "between the same two points rather than overflowing them.",
            });
        });

    private static Task<ToolDispatchResult> ScaleTextInPlace(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.scale_text_in_place", args, ct, (doc, db, tr) =>
        {
            var a = Read<ScaleTextArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0)
                throw new ArgumentException("handles is required: which text to resize.");

            var haveFactor = a.Factor is not null;
            var haveHeight = a.NewHeight is not null;
            if (haveFactor == haveHeight)
                throw new ArgumentException(
                    "Give EITHER factor OR newHeight, not both and not neither. factor multiplies " +
                    "each text's own height, so a mixed selection keeps its relative sizes; " +
                    "newHeight makes every one of them that height.");
            if (haveFactor && a.Factor <= 0)
                throw new ArgumentException("factor must be greater than zero.");
            if (haveHeight && a.NewHeight <= 0)
                throw new ArgumentException("newHeight must be greater than zero.");

            var changed = new List<object>();
            foreach (var h in a.Handles)
            {
                var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForWrite);

                double before, now;
                double[] anchorBefore, anchorAfter;
                string kind;

                switch (ent)
                {
                    case DBText t:
                    {
                        kind = "DBText";
                        before = t.Height;
                        // The anchor is the alignment point when the text is justified, and the
                        // position when it is not - reading the wrong one makes a left-justified
                        // text appear to hold still while a centred one drifts.
                        var justified = t.HorizontalMode != TextHorizontalMode.TextLeft ||
                                        t.VerticalMode != TextVerticalMode.TextBase;
                        var anchor = justified ? t.AlignmentPoint : t.Position;
                        anchorBefore = new[] { anchor.X, anchor.Y, anchor.Z };
                        t.Height = haveFactor ? before * a.Factor!.Value : a.NewHeight!.Value;
                        t.AdjustAlignment(db);
                        var after = justified ? t.AlignmentPoint : t.Position;
                        anchorAfter = new[] { after.X, after.Y, after.Z };
                        now = t.Height;
                        break;
                    }
                    case MText m:
                    {
                        kind = "MText";
                        before = m.TextHeight;
                        anchorBefore = new[] { m.Location.X, m.Location.Y, m.Location.Z };
                        m.TextHeight = haveFactor ? before * a.Factor!.Value : a.NewHeight!.Value;
                        anchorAfter = new[] { m.Location.X, m.Location.Y, m.Location.Z };
                        now = m.TextHeight;
                        break;
                    }
                    default:
                        throw new ArgumentException(
                            "Entity " + h + " is a " + ent.GetType().Name + ". This resizes " +
                            "single-line text and MText.");
                }

                // IN PLACE is the entire claim. modify.scale would move every one of these
                // towards a common base point; each of these has to hold its own.
                var drift = Math.Sqrt(
                    Math.Pow(anchorAfter[0] - anchorBefore[0], 2) +
                    Math.Pow(anchorAfter[1] - anchorBefore[1], 2));
                if (drift > 1e-6)
                    throw new InvalidOperationException(
                        "Text " + h + " moved by " + drift + " while being resized. This scales " +
                        "each text about its OWN anchor; use modify.scale when everything should " +
                        "move towards a common base point.");

                changed.Add(new
                {
                    handle = h, type = kind,
                    heightBefore = before, height = now,
                    anchor = anchorAfter, movedBy = drift,
                });
            }

            return Wrap(new
            {
                affected = changed.Count,
                factor = a.Factor,
                newHeight = a.NewHeight,
                items = changed,
                note = "Each text was resized about its OWN anchor and movedBy proves none of " +
                       "them drifted. modify.scale is the other thing: it scales distances too, " +
                       "so a row of tags would bunch towards the base point as well as growing.",
            });
        });

    // ─────────── roadmap 3.3: how an MText presents itself ───────────

    /// <summary>A column property, or null when the MText has no columns to have one.</summary>
    /// <remarks>
    /// ColumnCount, ColumnWidth and ColumnGutterWidth all THROW eNotApplicable when ColumnType
    /// is NoColumns - they are unanswerable, not merely unset, exactly like Polyline.ConstantWidth
    /// on a polyline whose segments differ. Reading one while building the RESULT is what made
    /// this tool fail with a bare eNotApplicable that appeared to come from the edit; the edit
    /// had not been reached.
    /// </remarks>
    private static T? SafeColumn<T>(Func<T> read) where T : struct
    {
        try { return read(); }
        catch (AcadRt.Exception) { return null; }
    }

    private static MText RequireMText(Database db, Transaction tr, string? handle, OpenMode mode)
    {
        if (string.IsNullOrWhiteSpace(handle))
            throw new ArgumentException("handle is required: the MText to change.");
        var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, handle!), mode);
        if (ent is MText m) return m;
        throw new ArgumentException(
            "Entity " + handle + " is a " + ent.GetType().Name + ", not an MText. Single-line " +
            "text has no background mask and no columns - those belong to MText.");
    }

    private static Task<ToolDispatchResult> BackgroundMaskMText(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.background_mask_mtext", args, ct, (doc, db, tr) =>
        {
            var a = Read<BackgroundMaskArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0)
                throw new ArgumentException("handles is required: which MText to mask.");

            var on = a.Enabled ?? true;
            var useDrawing = a.UseDrawingBackground == true;

            if (on && useDrawing && a.Color is not null)
                throw new ArgumentException(
                    "useDrawingBackground and color contradict each other: one takes whatever " +
                    "the drawing background happens to be, the other paints a fixed colour. " +
                    "Give one.");
            if (on && !useDrawing && a.Color is null)
                throw new ArgumentException(
                    "A mask needs a colour. Pass color, or useDrawingBackground: true to follow " +
                    "the drawing background - which is what you want on a sheet that may be " +
                    "plotted on white and viewed on black.");

            // AutoCAD's own limits. Below 1 the mask would be smaller than the text it is meant
            // to protect, which reads as a mask that does not work rather than one that is off.
            var scale = a.ScaleFactor ?? 1.5;
            if (on && (scale < 1.0 || scale > 5.0))
                throw new ArgumentException(
                    "scaleFactor must be between 1 and 5; AutoCAD's own range. 1 hugs the text " +
                    "exactly and anything below it would leave the text poking out of its own " +
                    "mask. 1.5 is the default.");

            var changed = new List<object>();
            foreach (var h in a.Handles)
            {
                var m = RequireMText(db, tr, h, OpenMode.ForWrite);
                var wasOn = m.BackgroundFill;

                m.BackgroundFill = on;
                if (on)
                {
                    m.UseBackgroundColor = useDrawing;
                    if (!useDrawing && a.Color is not null)
                        m.BackgroundFillColor = a.Color.AciIndex is int aci && aci >= 0
                            ? AcadColor.FromColorIndex(ColorMethod.ByAci, (short)aci)
                            : AcadColor.FromRgb((byte)a.Color.R, (byte)a.Color.G, (byte)a.Color.B);
                    m.BackgroundScaleFactor = scale;
                }

                // Read back. BackgroundFill is one of those properties that accepts an
                // assignment and can be overruled by the entity's own state, and a mask that
                // did not take looks exactly like one that was never asked for.
                if (m.BackgroundFill != on)
                    throw new InvalidOperationException(
                        "The mask on " + h + " reads back as " + m.BackgroundFill + " after " +
                        "being set to " + on + ", so the change did not take.");

                changed.Add(new
                {
                    handle = h,
                    enabledBefore = wasOn,
                    enabled = m.BackgroundFill,
                    usesDrawingBackground = m.BackgroundFill && m.UseBackgroundColor,
                    scaleFactor = m.BackgroundFill ? m.BackgroundScaleFactor : (double?)null,
                });
            }

            return Wrap(new
            {
                affected = changed.Count,
                enabled = on,
                items = changed,
                note = on
                    ? "The mask is drawn BEHIND the text and hides whatever it covers - the " +
                      "point of it on a busy plan. It does not change the MText's extents, so " +
                      "the entity measures the same as it did; look at the drawing to see it."
                    : "The mask is off. Anything behind the text shows through again.",
            });
        });

    private static Task<ToolDispatchResult> MTextColumnSettings(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.mtext_column_settings", args, ct, (doc, db, tr) =>
        {
            var a = Read<MTextColumnArgsDto>(args);
            var m = RequireMText(db, tr, a.Handle, OpenMode.ForWrite);

            var mode = (a.Mode ?? "static").Trim().ToLowerInvariant();
            if (mode is not ("none" or "static" or "dynamic"))
                throw new ArgumentException(
                    "mode must be none, static or dynamic. 'static' is a fixed number of columns " +
                    "you choose; 'dynamic' lets AutoCAD flow the text into as many as the height " +
                    "allows; 'none' puts it back to a single block of text.");

            var beforeType = m.ColumnType.ToString();
            var beforeCount = SafeColumn(() => m.ColumnCount);
            // The MText's OWN wrap width, which columns overwrite with the total. Captured so
            // the caller can see it change and put it back.
            var mtextWidthBefore = m.Width;
            var e0 = m.GeometricExtents;
            var w0 = e0.MaxPoint.X - e0.MinPoint.X;
            var h0 = e0.MaxPoint.Y - e0.MinPoint.Y;

            // Each assignment is attributed, because "eNotApplicable" on its own does not say
            // WHICH property AutoCAD objected to - and the column properties have to be set in
            // an order the API does not document.
            void Set(string what, Action act)
            {
                try { act(); }
                catch (AcadRt.Exception ex)
                {
                    throw new InvalidOperationException(
                        "Setting " + what + " on " + a.Handle + " threw " + ex.ErrorStatus +
                        ". The MText is " + (m.Width > 0 ? m.Width + " wide" : "auto-width") +
                        " and currently " + m.ColumnType + ".");
                }
            }

            if (mode == "none")
            {
                Set("ColumnType=NoColumns", () => m.ColumnType = ColumnType.NoColumns);
            }
            else
            {
                if (a.Width is null || a.Width <= 0)
                    throw new ArgumentException(
                        "width is required and must be greater than zero: how wide ONE column " +
                        "is. The MText's overall width becomes count*width plus the gutters, " +
                        "which is why it is given per column rather than in total.");
                var gutter = a.Gutter ?? a.Width.Value * 0.1;
                if (gutter < 0)
                    throw new ArgumentException("gutter cannot be negative.");

                // Order matters and is not documented. MEASURED, after an attributed failure
                // said which assignment AutoCAD objected to: ColumnWidth throws NotApplicable
                // while the MText is still NoColumns, so the type has to be set FIRST and the
                // column geometry after it. An earlier attempt at the reverse order was a guess,
                // made because a bare eNotApplicable looked like it came from the type - it came
                // from reading ColumnCount to build the result, before any of this ran.
                if (mode == "static")
                {
                    var count = a.Count ?? 2;
                    if (count < 2)
                        throw new ArgumentException(
                            "count must be at least 2 for static columns; one column is mode " +
                            "'none'.");
                    Set("ColumnType=StaticColumns", () => m.ColumnType = ColumnType.StaticColumns);
                    Set("ColumnCount", () => m.ColumnCount = count);
                }
                else
                {
                    Set("ColumnType=DynamicColumns",
                        () => m.ColumnType = ColumnType.DynamicColumns);
                    Set("ColumnAutoHeight", () => m.ColumnAutoHeight = a.AutoHeight ?? true);
                }
                Set("ColumnWidth", () => m.ColumnWidth = a.Width!.Value);
                Set("ColumnGutterWidth", () => m.ColumnGutterWidth = gutter);
            }

            var e1 = m.GeometricExtents;
            var w1 = e1.MaxPoint.X - e1.MinPoint.X;
            var h1 = e1.MaxPoint.Y - e1.MinPoint.Y;

            return Wrap(new
            {
                handle = a.Handle,
                modeBefore = beforeType,
                mode = m.ColumnType.ToString(),
                countBefore = beforeCount,
                count = SafeColumn(() => m.ColumnCount),
                width = SafeColumn(() => m.ColumnWidth),
                gutter = SafeColumn(() => m.ColumnGutterWidth),
                // The measurement that separates "the property was set" from "the text reflowed".
                // Columns are a LAYOUT change, so the drawn extent has to move; a count that
                // reads 3 over an unchanged block of text is a property nobody applied.
                widthBefore = w0,
                drawnWidth = w1,
                heightBefore = h0,
                drawnHeight = h1,
                mtextWidthBefore,
                mtextWidth = m.Width,
                note = "widthBefore/drawnWidth and heightBefore/drawnHeight are how you tell the " +
                       "text actually REFLOWED from a column count that was merely stored - " +
                       "splitting a block into columns widens it and shortens it. " +
                       (mode == "none"
                            ? "Measured, and worth knowing: mode='none' removes the columns but " +
                              "does NOT restore the MText's original wrap width - it keeps " +
                              "whatever the columns made it (" + m.Width + ", against " +
                              mtextWidthBefore + " before this call), so the text comes back as " +
                              "one WIDE block rather than the narrow one you started with. Set " +
                              "the width back yourself if that matters."
                            : "Note that this overwrites the MText's own wrap width with the " +
                              "total across the columns: it was " + mtextWidthBefore +
                              " and is now " + m.Width + "."),
            });
        });

    // ─────────── roadmap 3.3: symbols and stacked fractions ───────────
    //
    // Both of these write CONTROL CODES into a text string, and both can therefore succeed at
    // writing while failing at meaning: a diameter symbol that comes out as the literal "%%c",
    // or a fraction that stays inline while the entity dutifully reports a \S in its contents.
    //
    // So neither is judged on what was written. insert_symbol is judged on the RENDERED text -
    // the character has to be there, not the code for it. stack_fraction cannot be judged that
    // way, because a stacked "1/2" renders as the same three characters as an unstacked one, so
    // it is judged on the drawn EXTENT: stacking puts the halves on two levels, which makes the
    // text taller and narrower.

    /// <summary>The symbols a draughtsman actually reaches for, and the character each is.</summary>
    /// <remarks>
    /// DBText and MText do NOT take the same codes. Single-line text uses AutoCAD's %% codes and
    /// understands only three of them; MText takes \U+ escapes and the whole of Unicode. Writing
    /// the MText form into a DBText leaves the literal text "\U+2205" on the sheet, which is the
    /// failure this table exists to prevent.
    /// </remarks>
    private static readonly Dictionary<string, (string Char, string? DbTextCode)> Symbols =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["diameter"]    = ("∅", "%%c"),
            ["degrees"]     = ("°", "%%d"),
            ["plusminus"]   = ("±", "%%p"),
            ["centreline"]  = ("℄", null),
            ["centerline"]  = ("℄", null),
            ["delta"]       = ("Δ", null),
            ["phi"]         = ("Φ", null),
            ["omega"]       = ("Ω", null),
            ["ohm"]         = ("Ω", null),
            ["almostequal"] = ("≈", null),
            ["notequal"]    = ("≠", null),
            ["angle"]       = ("∠", null),
            ["squared"]     = ("²", null),
            ["cubed"]       = ("³", null),
            ["property"]    = ("⅊", null),
            ["monument"]    = ("∅", null),
        };

    private static Task<ToolDispatchResult> InsertSymbol(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.insert_symbol", args, ct, (doc, db, tr) =>
        {
            var a = Read<InsertSymbolArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0)
                throw new ArgumentException("handles is required: which text to insert into.");
            if (string.IsNullOrWhiteSpace(a.Symbol))
                throw new ArgumentException(
                    "symbol is required. Use a name - " + string.Join(", ", Symbols.Keys) +
                    " - or a Unicode code point written as U+00B0, or the character itself.");

            var name = a.Symbol!.Trim();
            string ch;
            string? dbCode = null;
            if (Symbols.TryGetValue(name, out var known))
            {
                ch = known.Char;
                dbCode = known.DbTextCode;
            }
            else if (name.StartsWith("U+", StringComparison.OrdinalIgnoreCase) &&
                     int.TryParse(name.Substring(2), System.Globalization.NumberStyles.HexNumber,
                                  System.Globalization.CultureInfo.InvariantCulture, out var cp))
            {
                ch = char.ConvertFromUtf32(cp);
            }
            else if (name.Length <= 2)
            {
                ch = name;   // the character itself
            }
            else
            {
                throw new ArgumentException(
                    "'" + name + "' is not a known symbol name, a U+XXXX code point, or a single " +
                    "character. Known names: " + string.Join(", ", Symbols.Keys) + ".");
            }

            var where = (a.Where ?? "end").Trim().ToLowerInvariant();
            if (where is not ("end" or "start"))
                throw new ArgumentException("where must be 'end' or 'start'.");

            var changed = new List<object>();
            foreach (var h in a.Handles)
            {
                var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForWrite);

                string kind, before, stored, rendered;
                int inserted;
                // Whether the symbol went in as a CONTROL CODE (which only becomes a glyph when
                // AutoCAD draws it) or as the character itself. The two can be verified in
                // different ways and only one of them can be verified here at all.
                var viaCode = false;

                switch (ent)
                {
                    case DBText t:
                    {
                        kind = "DBText";
                        before = t.TextString;
                        // Single-line text renders only the three %% codes. Anything else has to
                        // go in as the character itself, which works when the style's font has
                        // the glyph and shows a box when it does not - said in the note rather
                        // than discovered on a plot.
                        var token = dbCode ?? ch;
                        viaCode = dbCode is not null;
                        (stored, inserted) = Splice(before, token, where, a.Replace);
                        t.TextString = stored;
                        // NOT a rendering. DBText.TextString gives back what is STORED, control
                        // codes and all - %%c stays "%%c" here and becomes a diameter sign only
                        // when AutoCAD draws it. Asking this string for the glyph is asking a
                        // question it cannot answer.
                        rendered = t.TextString;
                        break;
                    }
                    case MText m:
                    {
                        kind = "MText";
                        before = m.Text;
                        // MText takes the character directly; \U+ escapes are equivalent and
                        // uglier to read back.
                        (stored, inserted) = Splice(m.Contents, ch, where, a.Replace);
                        m.Contents = stored;
                        rendered = m.Text;
                        break;
                    }
                    default:
                        throw new ArgumentException(
                            "Entity " + h + " is a " + ent.GetType().Name + ". Symbols go into " +
                            "single-line text and MText.");
                }

                if (inserted == 0)
                    throw new ArgumentException(
                        "Nothing was inserted into " + h + ": the placeholder '" + a.Replace +
                        "' does not appear in its text, which reads \"" + before + "\".");

                // Checked only where checking means something. When the symbol went in as the
                // CHARACTER, it has to be in what the entity reads back - a tool that wrote a
                // code instead would pass every other assertion and put a literal "%%c" on the
                // sheet. When it went in as a control code there is nothing to check here:
                // DBText hands back the code, and whether it becomes a glyph is a question only
                // the drawing can answer. Saying so beats a check that always passes.
                if (!viaCode && !rendered.Contains(ch, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "The text on " + h + " reads back as \"" + rendered + "\", which does " +
                        "not contain the symbol that was asked for, so the insertion did not " +
                        "take.");

                changed.Add(new
                {
                    handle = h, type = kind, before, rendered, stored, insertions = inserted,
                    viaControlCode = viaCode,
                });
            }

            return Wrap(new
            {
                affected = changed.Count,
                symbol = name,
                character = ch,
                items = changed,
                note = "Where the symbol went in as the CHARACTER - which is every MText, and " +
                       "single-line text for anything outside %%c, %%d and %%p - the entity is " +
                       "read back and the glyph has to be there. Where it went in as a CONTROL " +
                       "CODE, viaControlCode says so and nothing here can confirm the glyph: " +
                       "DBText hands back \"%%c\", and it becomes a diameter sign only when " +
                       "AutoCAD draws it. Look at a plot for those. A symbol inserted as a " +
                       "character also needs a text style whose font HAS that glyph, or it " +
                       "draws as a box - see KNOWN-GAPS A8, where m2 came out as m?.",
            });
        });

    /// <summary>Insert a token at one end, or in place of a placeholder. Returns the count.</summary>
    private static (string Result, int Count) Splice(string text, string token, string where,
                                                     string? placeholder)
    {
        if (!string.IsNullOrEmpty(placeholder))
        {
            var n = 0;
            var idx = 0;
            var sb = new System.Text.StringBuilder();
            while (true)
            {
                var at = text.IndexOf(placeholder!, idx, StringComparison.Ordinal);
                if (at < 0) break;
                sb.Append(text, idx, at - idx).Append(token);
                idx = at + placeholder!.Length;
                n++;
            }
            sb.Append(text, idx, text.Length - idx);
            return (sb.ToString(), n);
        }
        return where == "start" ? (token + text, 1) : (text + token, 1);
    }

    private static Task<ToolDispatchResult> StackFraction(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.stack_fraction", args, ct, (doc, db, tr) =>
        {
            var a = Read<StackFractionArgsDto>(args);
            var m = RequireMText(db, tr, a.Handle, OpenMode.ForWrite);

            var style = (a.Style ?? "horizontal").Trim().ToLowerInvariant();
            var sep = style switch
            {
                "horizontal" => "/",   // a bar between them
                "diagonal"   => "#",   // a slash between them
                "tolerance"  => "^",   // no bar at all - upper and lower limits
                _ => throw new ArgumentException(
                    "style must be horizontal (a bar between the halves), diagonal (a slash), or " +
                    "tolerance (no bar, which is how an upper and lower limit are written)."),
            };

            // Numbers either side of a slash, not already inside a \S...; group.
            var rx = new Regex(a.Pattern ?? @"(?<!\\S[^;]{0,40})\b(\d+)/(\d+)\b");
            var before = m.Text;
            var beforeStored = m.Contents;
            var e0 = m.GeometricExtents;
            var w0 = e0.MaxPoint.X - e0.MinPoint.X;
            var h0 = e0.MaxPoint.Y - e0.MinPoint.Y;

            var found = new List<string>();
            var stored = rx.Replace(beforeStored, mm =>
            {
                found.Add(mm.Value);
                return "\\S" + mm.Groups[1].Value + sep + mm.Groups[2].Value + ";";
            });

            if (found.Count == 0)
                throw new ArgumentException(
                    "No fraction to stack in \"" + before + "\". This looks for digits either " +
                    "side of a slash, such as 1/2. Pass your own regular expression as pattern " +
                    "if the text is written differently.");

            m.Contents = stored;

            var e1 = m.GeometricExtents;
            var w1 = e1.MaxPoint.X - e1.MinPoint.X;
            var h1 = e1.MaxPoint.Y - e1.MinPoint.Y;

            // A stacked fraction renders as the SAME characters as an unstacked one, so the
            // rendered text cannot tell you whether it worked. The drawn extent can: two levels
            // of digits are taller than one. Checked here rather than left to the caller.
            if (h1 <= h0)
                throw new InvalidOperationException(
                    "The text is " + h1 + " tall against " + h0 + " before stacking, so the " +
                    "halves did not go onto two levels and this is not being reported as a " +
                    "stacked fraction. The contents now read \"" + m.Contents + "\".");

            return Wrap(new
            {
                handle = a.Handle,
                style,
                stacked = found.Count,
                fractions = found,
                before,
                stored = m.Contents,
                widthBefore = w0,
                drawnWidth = w1,
                heightBefore = h0,
                drawnHeight = h1,
                note = "A stacked fraction RENDERS as the same characters as an unstacked one - " +
                       "\"1/2\" either way - so the rendered text cannot tell you it worked. The " +
                       "drawn extent can, and does: " + h0 + " tall before, " + h1 + " after, " +
                       "because the halves are now on two levels.",
            });
        });

    // ─────────── roadmap 3.3: converting between text and mtext ───────────
    //
    // The trap in text_to_mtext is ORDER. Combining lines in the order their handles happen to
    // come in produces a paragraph whose sentences are shuffled, and every count in the result
    // is still right: N in, one out, all the words present. So the lines are sorted into READING
    // order - down the page, then across - and the order used is reported, because it is the one
    // thing a caller cannot check from the count.

    private static Task<ToolDispatchResult> TextToMText(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.text_to_mtext", args, ct, (doc, db, tr) =>
        {
            var a = Read<TextToMTextArgsDto>(args);
            if (a.Handles is null || a.Handles.Count == 0)
                throw new ArgumentException(
                    "handles is required: the single-line texts to combine into one MText.");

            var items = new List<(string Handle, DBText Text)>();
            foreach (var h in a.Handles)
            {
                var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForWrite);
                if (ent is not DBText t)
                    throw new ArgumentException(
                        "Entity " + h + " is a " + ent.GetType().Name + ", not single-line text. " +
                        "This combines DBText into one MText; an MText is already one.");
                items.Add((h, t));
            }

            // Reading order, not handle order. Down the page first, then across - and lines
            // within half a text height of each other count as the same line, or two labels
            // side by side would be split across paragraphs by a rounding error.
            var tol = items.Max(i => i.Text.Height) * 0.5;
            var ordered = items
                .OrderByDescending(i => Math.Round(i.Text.Position.Y / Math.Max(tol, 1e-9)))
                .ThenBy(i => i.Text.Position.X)
                .ToList();

            var first = ordered[0].Text;
            var minX = items.Min(i => i.Text.Position.X);
            var maxY = items.Max(i => i.Text.Position.Y + i.Text.Height);

            // \P is MText's paragraph break. Each source line becomes its own paragraph, which
            // is what keeps them on separate lines however the MText is later re-wrapped.
            var lines = ordered.Select(i => i.Text.TextString).ToList();
            var contents = string.Join("\\P", lines);

            var m = new MText
            {
                Location = new Point3d(minX, maxY, 0),
                Contents = contents,
                TextHeight = first.Height,
                Width = a.Width ?? 0,
                TextStyleId = first.TextStyleId,
                Attachment = AttachmentPoint.TopLeft,
            };
            var handle = AcadEnv.Persist(db, tr, m, a.Layer ?? first.Layer);

            // Every source line has to survive into what the MText renders. A join that dropped
            // one, or mangled it on the way through the paragraph break, would still return a
            // handle and a plausible count.
            var renderedNow = m.Text;
            foreach (var line in lines)
                if (line.Length > 0 && !renderedNow.Contains(line, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "The combined MText renders as \"" + renderedNow + "\", which does not " +
                        "contain the source line \"" + line + "\", so this is not being reported " +
                        "as a successful combine.");

            var keep = a.KeepOriginal == true;
            if (!keep) foreach (var i in items) i.Text.Erase();

            return Wrap(new
            {
                entity = handle,
                combined = items.Count,
                readingOrder = lines,
                sourceHandles = ordered.Select(o => o.Handle).ToList(),
                contents = m.Contents,
                rendered = renderedNow,
                originalsKept = keep,
                note = "The lines were sorted into READING order - down the page, then across - " +
                       "not the order the handles arrived in, which would shuffle the sentences " +
                       "while every count in this result stayed correct. readingOrder is what " +
                       "was actually used. The originals are erased unless keepOriginal is true.",
            });
        });

    private static Task<ToolDispatchResult> ExplodeMTextToText(JsonObject args, CancellationToken ct) =>
        Run("acad.annotations.explode_mtext_to_text", args, ct, (doc, db, tr) =>
        {
            var a = Read<ExplodeMTextArgsDto>(args);
            var m = RequireMText(db, tr, a.Handle, OpenMode.ForWrite);
            var before = m.Text;

            var pieces = new DBObjectCollection();
            m.Explode(pieces);
            if (pieces.Count == 0)
                throw new InvalidOperationException(
                    "Exploding " + a.Handle + " produced nothing. Its text reads \"" + before +
                    "\".");

            var made = new List<object>();
            foreach (DBObject o in pieces)
            {
                if (o is not Entity e) continue;
                var h = AcadEnv.Persist(db, tr, e, a.Layer);
                made.Add(new
                {
                    handle = h.Handle,
                    type = e.GetType().Name,
                    text = e is DBText dt ? dt.TextString : (e is MText mt ? mt.Text : null),
                });
            }

            var keep = a.KeepOriginal == true;
            if (!keep) m.Erase();

            return Wrap(new
            {
                entities = made,
                pieces = made.Count,
                before,
                originalKept = keep,
                note = "One piece per LINE, not per word. Formatting that lived in the MText - " +
                       "columns, a background mask, a stacked fraction - has nowhere to go on a " +
                       "single-line text and does not survive; the words do. The original is " +
                       "erased unless keepOriginal is true.",
            });
        });
}
