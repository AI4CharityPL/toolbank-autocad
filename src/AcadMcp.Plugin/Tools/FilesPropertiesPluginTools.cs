// AutoCAD plugin handlers for drawing properties — the surviving half of roadmap 2.4.
//
// These are the fields the Windows file dialog and DWGPROPS show: title, subject, author,
// keywords, comments, last saved by, revision number, hyperlink base, plus any number of custom
// name/value pairs. They matter here for a reason beyond bookkeeping: `acad-fields` can place a
// FIELD bound to a drawing property, so a title block that reads its project name from here stays
// correct when the property changes, instead of being retyped on every sheet.
//
// The API has a shape worth stating, because getting it wrong produces a silent no-op:
// Database.SummaryInfo returns an IMMUTABLE DatabaseSummaryInfo. Mutating what it hands back
// changes nothing. A write goes through DatabaseSummaryInfoBuilder — seeded FROM the current
// info, or every unset field is blanked — and ends with an assignment back to db.SummaryInfo.
// That last line is the one that would be easy to omit and impossible to notice from a return
// code, so it is the thing the verification re-reads in a separate call.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;

namespace AcadMcp.Plugin.Tools;

internal static class FilesPropertiesPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.files.list_drawing_properties", ListDrawingProperties);
        host.Register("acad.files.set_drawing_properties", SetDrawingProperties);
        host.Register("acad.files.set_drawing_custom_property", SetDrawingCustomProperty);
    }

    private static T Read<T>(JsonObject a) => JsonSerializer.Deserialize<T>(a, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Dictionary<string, string> CustomOf(DatabaseSummaryInfo info)
    {
        var custom = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var table = info.CustomProperties;
        while (table.MoveNext())
        {
            var entry = table.Entry;
            if (entry.Key is string k) custom[k] = entry.Value as string ?? "";
        }
        return custom;
    }

    private static object Snapshot(Database db)
    {
        var i = db.SummaryInfo;
        return new
        {
            title = i.Title ?? "",
            subject = i.Subject ?? "",
            author = i.Author ?? "",
            keywords = i.Keywords ?? "",
            comments = i.Comments ?? "",
            lastSavedBy = i.LastSavedBy ?? "",
            revisionNumber = i.RevisionNumber ?? "",
            hyperlinkBase = i.HyperlinkBase ?? "",
            custom = CustomOf(i),
        };
    }

    /// <summary>
    /// A builder seeded from the drawing's current properties.
    /// </summary>
    /// <remarks>
    /// Seeding is not optional. A fresh DatabaseSummaryInfoBuilder starts empty, so assigning one
    /// back after setting a single field would blank every other property in the drawing —
    /// a set_drawing_properties call that quietly erased the author while setting the title.
    /// </remarks>
    private static DatabaseSummaryInfoBuilder SeededBuilder(Database db)
    {
        var i = db.SummaryInfo;
        var b = new DatabaseSummaryInfoBuilder
        {
            Title = i.Title,
            Subject = i.Subject,
            Author = i.Author,
            Keywords = i.Keywords,
            Comments = i.Comments,
            LastSavedBy = i.LastSavedBy,
            RevisionNumber = i.RevisionNumber,
            HyperlinkBase = i.HyperlinkBase,
        };
        foreach (var kv in CustomOf(i)) b.CustomPropertyTable.Add(kv.Key, kv.Value);
        return b;
    }

    private static Task<ToolDispatchResult> ListDrawingProperties(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunReadAsync("acad.files.list_drawing_properties", ct, (doc, db, tr) =>
            Wrap(new
            {
                properties = Snapshot(db),
                note = "These are the DWGPROPS fields. acad-fields can bind a field to any of " +
                       "them, so a title block reading its project name from here updates itself " +
                       "instead of being retyped on every sheet.",
            }));

    private static Task<ToolDispatchResult> SetDrawingProperties(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.files.set_drawing_properties", ct, (doc, db, tr) =>
        {
            var a = Read<SetDrawingPropertiesArgsDto>(args);
            var b = SeededBuilder(db);

            var applied = new List<string>();
            void Apply(string? value, string name, Action<string> set)
            {
                if (value is null) return;          // absent = leave alone
                set(value);                          // "" = clear deliberately
                applied.Add(name);
            }

            Apply(a.Title, "title", v => b.Title = v);
            Apply(a.Subject, "subject", v => b.Subject = v);
            Apply(a.Author, "author", v => b.Author = v);
            Apply(a.Keywords, "keywords", v => b.Keywords = v);
            Apply(a.Comments, "comments", v => b.Comments = v);
            Apply(a.RevisionNumber, "revisionNumber", v => b.RevisionNumber = v);
            Apply(a.HyperlinkBase, "hyperlinkBase", v => b.HyperlinkBase = v);

            if (applied.Count == 0)
                throw new ArgumentException(
                    "Nothing to set. Pass at least one of title, subject, author, keywords, " +
                    "comments, revisionNumber, hyperlinkBase. An empty string clears a property; " +
                    "omitting it leaves it alone. Custom properties go through " +
                    "set_drawing_custom_property.");

            db.SummaryInfo = b.ToDatabaseSummaryInfo();

            return Wrap(new { applied, properties = Snapshot(db) });
        });

    private static Task<ToolDispatchResult> SetDrawingCustomProperty(JsonObject args, CancellationToken ct) =>
        PluginToolRunner.RunWriteAsync("acad.files.set_drawing_custom_property", ct, (doc, db, tr) =>
        {
            var a = Read<SetCustomPropertyArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Name))
                throw new ArgumentException("name is required.");

            var b = SeededBuilder(db);
            var existed = b.CustomPropertyTable.Contains(a.Name);

            // value:null is the delete. There is no separate remove tool because "set it to
            // nothing" and "remove it" are the same intent, and two tools for one intent is
            // something an agent has to choose between for no benefit.
            if (a.Value is null)
            {
                if (!existed)
                    throw new ArgumentException(
                        "No custom property named '" + a.Name + "' to remove. " +
                        "list_drawing_properties reports the ones that exist.");
                b.CustomPropertyTable.Remove(a.Name);
            }
            else
            {
                if (existed) b.CustomPropertyTable.Remove(a.Name);
                b.CustomPropertyTable.Add(a.Name, a.Value);
            }

            db.SummaryInfo = b.ToDatabaseSummaryInfo();

            return Wrap(new
            {
                name = a.Name,
                action = a.Value is null ? "removed" : existed ? "replaced" : "added",
                properties = Snapshot(db),
            });
        });
}
