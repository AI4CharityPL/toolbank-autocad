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

        // roadmap 3.3 - finding text across a drawing
        host.Register("acad.annotations.list_text_by_pattern",  ListTextByPattern);
        host.Register("acad.annotations.find_replace_text",     FindReplaceText);
        host.Register("acad.annotations.export_text_content",   ExportTextContent);
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
}
