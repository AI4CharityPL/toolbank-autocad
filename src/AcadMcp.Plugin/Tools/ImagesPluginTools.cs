// AutoCAD plugin handlers for the acad-images category (roadmap 3.5, raster half).
// Registered under "acad.images.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern), 26 (traps).
//
// MEASURED API shape - see the header comment in the backend's ImagesTools.cs for the full
// probe trail. The three surprises that shape this file:
//
//   RasterImage.Width/Height are READ-ONLY, derived from Orientation (origin + two vectors)
//   crossed with the source pixel size. attach_image places the image at a UNIT-scale
//   orientation first so it can MEASURE the native size, computes the vectors that give the
//   requested drawing size, sets Orientation again, then reads Width/Height back and refuses
//   if they do not match what was asked for.
//
//   There is no per-entity background-transparency property anywhere in the managed API -
//   nine candidate names failed to compile - so this category has no set_image_transparency.
//
//   The image border is IMAGEFRAME, a DRAWING-WIDE system variable, not a per-entity property
//   (RasterImage.ShowImageBorder does not exist) - same shape as XCLIPFRAME.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AcadMcp.Plugin.Tools;

internal static class ImagesPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.images.attach_image",      AttachImage);
        host.Register("acad.images.list_images",        ListImages);
        host.Register("acad.images.detach_image",       DetachImage);
        host.Register("acad.images.clip_image",         ClipImage);
        host.Register("acad.images.set_image_adjust",   SetImageAdjust);
        host.Register("acad.images.set_image_frame",    SetImageFrame);
        host.Register("acad.images.set_image_path",     SetImagePath);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    // ─────────── shared helpers ───────────

    private static ObjectId ImageDictId(Database db) => RasterImageDef.GetImageDictionary(db);

    private static DBDictionary GetOrCreateImageDict(Database db, Transaction tr)
    {
        var dictId = ImageDictId(db);
        if (!dictId.IsNull)
            return (DBDictionary)tr.GetObject(dictId, OpenMode.ForWrite);

        var nod = (DBDictionary)tr.GetObject(db.NamedObjectsDictionaryId, OpenMode.ForWrite);
        var dict = new DBDictionary();
        nod.SetAt("ACAD_IMAGE_DICT", dict);
        tr.AddNewlyCreatedDBObject(dict, true);
        return dict;
    }

    private static string? FindDefName(DBDictionary dict, ObjectId defId)
    {
        foreach (DBDictionaryEntry e in dict)
            if (e.Value == defId) return e.Key;
        return null;
    }

    private static BlockTableRecord ModelSpace(Database db, Transaction tr, OpenMode mode)
    {
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        return (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], mode);
    }

    private static RasterImage RequireImage(Database db, Transaction tr, string? handle, OpenMode mode)
    {
        if (string.IsNullOrWhiteSpace(handle))
            throw new ArgumentException("handle is required: which image.");
        var id = AcadEnv.ResolveHandle(db, handle!);
        if (tr.GetObject(id, mode) is not RasterImage img)
            throw new ArgumentException($"Handle '{handle}' is not a raster image entity.");
        return img;
    }

    private static object DescribeImage(Transaction tr, RasterImage img, DBDictionary? dict)
    {
        string name = dict is null ? "<unknown>" : (FindDefName(dict, img.ImageDefId) ?? "<unknown>");
        string path = "";
        try
        {
            var def = (RasterImageDef)tr.GetObject(img.ImageDefId, OpenMode.ForRead);
            path = def.ActiveFileName ?? def.SourceFileName ?? "";
        }
        catch (Exception) { /* def missing or unreadable - report what we can */ }

        double rotDeg = Math.Atan2(img.Orientation.Xaxis.Y, img.Orientation.Xaxis.X) * 180.0 / Math.PI;
        return new
        {
            handle = img.Handle.ToString(),
            name,
            path,
            insertionPoint = AcadEnv.FromPoint3d(img.Orientation.Origin),
            width = img.Width,
            height = img.Height,
            rotationDegrees = rotDeg,
            extents = AcadEnv.BoundsOf(img.GeometricExtents),
            clipped = img.IsClipped,
            adjust = new { brightness = (int)img.Brightness, contrast = (int)img.Contrast, fade = (int)img.Fade },
            layer = img.Layer,
        };
    }

    // ─────────── attaching ───────────

    private static Task<ToolDispatchResult> AttachImage(JsonObject args, CancellationToken ct) =>
        Run("acad.images.attach_image", args, ct, (doc, db, tr) =>
        {
            var a = Read<ImageAttachArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Path))
                throw new ArgumentException("path is required: the image file to attach.");
            if (!File.Exists(a.Path))
                throw new ArgumentException($"No file at '{a.Path}'.");
            var insertion = AcadEnv.ToPoint3d(a.InsertionPoint);
            if (a.Width is null || a.Width <= 0)
                throw new ArgumentException("width is required and must be positive (drawing units).");
            if (a.Height is not null && a.Height <= 0)
                throw new ArgumentException("height must be positive if given.");

            var dict = GetOrCreateImageDict(db, tr);
            string name = string.IsNullOrWhiteSpace(a.Name)
                ? Path.GetFileNameWithoutExtension(a.Path)
                : a.Name!;
            // A name already in use is only refused if it points at a DIFFERENT file - matching
            // real AutoCAD, which lets you place the same image definition more than once. Two
            // entities then share one RasterImageDef, which is what makes detach_image's "was
            // this the last placement" branch a real, reachable case rather than dead code.
            bool reusedDef;
            ObjectId defId;
            string requestedResolved = Path.GetFullPath(a.Path!);
            if (dict.Contains(name))
            {
                var existingId = dict.GetAt(name);
                var existingDef = (RasterImageDef)tr.GetObject(existingId, OpenMode.ForRead);
                string existingResolved = Path.GetFullPath(
                    existingDef.ActiveFileName ?? existingDef.SourceFileName ?? "");
                if (!string.Equals(existingResolved, requestedResolved, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException(
                        $"An image called '{name}' is already attached from a different file " +
                        $"('{existingDef.ActiveFileName}'). Pick a different name, or detach_image " +
                        "the existing one first.");
                defId = existingId;
                reusedDef = true;
            }
            else
            {
                var def = new RasterImageDef { SourceFileName = a.Path! };
                def.Load();
                if (!def.IsLoaded)
                    throw new InvalidOperationException(
                        $"AutoCAD could not load '{a.Path}' as an image - check the format is one " +
                        "it supports (PNG, JPG, BMP, TIFF and a handful of others).");
                defId = dict.SetAt(name, def);
                tr.AddNewlyCreatedDBObject(def, true);
                reusedDef = false;
            }

            var img = new RasterImage { ImageDefId = defId };
            var ms = ModelSpace(db, tr, OpenMode.ForWrite);
            if (!string.IsNullOrWhiteSpace(a.Layer)) img.LayerId = AcadEnv.EnsureLayer(db, tr, a.Layer);
            ms.AppendEntity(img);
            tr.AddNewlyCreatedDBObject(img, true);
            // MEASURED: AssociateRasterDef modifies the def's reactor list, so it needs the def
            // open FOR WRITE - opening it ForRead compiles fine but throws a FATAL internal
            // AutoCAD error (dbobji.cpp eNotOpenForWrite) that freezes the UI thread rather than
            // raising a catchable .NET exception. Cost a full deploy cycle to find; see rule 26.
            var defForAssociate = (RasterImageDef)tr.GetObject(defId, OpenMode.ForWrite);
            img.AssociateRasterDef(defForAssociate);

            // MEASURED: RasterImage.Width/Height are simply the LENGTHS of Orientation's two
            // axis vectors - not multiplied by the source pixel count. ImageWidth/ImageHeight
            // are the actual pixel dimensions and do NOT change with Orientation, which is why
            // they - not Width/Height - are the right pair to compute an aspect ratio from. A
            // first version measured Width/Height at a unit-vector Orientation, which reports
            // 1/1 for ANY image regardless of pixel aspect and silently squared every placement.
            double pxW = img.ImageWidth, pxH = img.ImageHeight;
            if (pxW <= 0 || pxH <= 0)
                throw new InvalidOperationException(
                    "Could not read the image's pixel size after loading it - ImageWidth=" + pxW +
                    " ImageHeight=" + pxH + ".");

            double finalWidth = a.Width!.Value;
            double expectedHeight = a.Height ?? finalWidth * pxH / pxW;
            double thetaRad = (a.RotationDegrees ?? 0) * Math.PI / 180.0;
            double cos = Math.Cos(thetaRad), sin = Math.Sin(thetaRad);
            var uAxis = new Vector3d(cos, sin, 0) * finalWidth;
            var vAxis = new Vector3d(-sin, cos, 0) * expectedHeight;
            img.Orientation = new CoordinateSystem3d(insertion, uAxis, vAxis);

            // PROVEN, not assumed: the placed size is read back and must match what was asked.
            double finalW = img.Width, finalH = img.Height;
            double tolW = Math.Max(1e-6, a.Width.Value * 1e-6);
            double tolH = Math.Max(1e-6, expectedHeight * 1e-6);
            if (Math.Abs(finalW - a.Width.Value) > tolW)
                throw new InvalidOperationException(
                    $"Placed width reads back as {finalW}, not the requested {a.Width.Value}.");
            if (Math.Abs(finalH - expectedHeight) > tolH)
                throw new InvalidOperationException(
                    $"Placed height reads back as {finalH}, not the expected {expectedHeight}.");

            return Wrap(new
            {
                image = DescribeImage(tr, img, dict),
                reusedDefinition = reusedDef,
                note = (reusedDef
                    ? "Reused the existing definition '" + name + "' - same source file, same " +
                      "name, so this is a second placement of it rather than a duplicate. "
                    : "") +
                       "Height was computed to preserve the source aspect ratio because none was " +
                       "given. The placed width and height were both read back from the entity " +
                       "and checked against what was requested before this returned.",
            });
        });

    // ─────────── reading ───────────

    private static Task<ToolDispatchResult> ListImages(JsonObject args, CancellationToken ct) =>
        Run("acad.images.list_images", args, ct, (doc, db, tr) =>
        {
            var dictId = ImageDictId(db);
            var found = new List<object>();
            if (!dictId.IsNull)
            {
                var dict = (DBDictionary)tr.GetObject(dictId, OpenMode.ForRead);
                var ms = ModelSpace(db, tr, OpenMode.ForRead);
                foreach (ObjectId eid in ms)
                {
                    if (eid.IsErased) continue;                        // rule 26 section 8
                    if (tr.GetObject(eid, OpenMode.ForRead) is RasterImage img)
                        found.Add(DescribeImage(tr, img, dict));
                }
            }
            return Wrap(new
            {
                count = found.Count,
                images = found,
                note = "No image dictionary yet means no image has ever been attached in this " +
                       "drawing, reported as zero rather than an error.",
            });
        });

    // ─────────── detaching ───────────

    private static Task<ToolDispatchResult> DetachImage(JsonObject args, CancellationToken ct) =>
        Run("acad.images.detach_image", args, ct, (doc, db, tr) =>
        {
            var a = Read<ImageHandleArgsDto>(args);
            var img = RequireImage(db, tr, a.Handle, OpenMode.ForWrite);
            var defId = img.ImageDefId;

            var dictId = ImageDictId(db);
            DBDictionary? dict = dictId.IsNull ? null : (DBDictionary)tr.GetObject(dictId, OpenMode.ForWrite);
            string name = dict is null ? "<unknown>" : (FindDefName(dict, defId) ?? "<unknown>");

            img.Erase();

            bool stillUsed = false;
            var ms = ModelSpace(db, tr, OpenMode.ForRead);
            foreach (ObjectId eid in ms)
            {
                if (eid.IsErased) continue;
                if (tr.GetObject(eid, OpenMode.ForRead) is RasterImage other && other.ImageDefId == defId)
                {
                    stillUsed = true;
                    break;
                }
            }

            bool defRemoved = false;
            if (!stillUsed && dict is not null && name != "<unknown>" && dict.Contains(name))
            {
                var def = (RasterImageDef)tr.GetObject(defId, OpenMode.ForWrite);
                dict.Remove(name);
                def.Erase();
                defRemoved = true;
            }

            if (!img.IsErased)
                throw new InvalidOperationException("The entity did not actually erase.");

            return Wrap(new
            {
                handle = a.Handle,
                name,
                defRemoved,
                note = defRemoved
                    ? "No other placement used the same source file, so the definition was " +
                      "removed as well as the entity."
                    : "Another placement still uses the same source file, so only this entity " +
                      "was removed; the source stays available for it.",
            });
        });

    // ─────────── clipping ───────────

    private static Task<ToolDispatchResult> ClipImage(JsonObject args, CancellationToken ct) =>
        Run("acad.images.clip_image", args, ct, (doc, db, tr) =>
        {
            var a = Read<ImageClipArgsDto>(args);
            var img = RequireImage(db, tr, a.Handle, OpenMode.ForWrite);

            var extentsBefore = AcadEnv.BoundsOf(img.GeometricExtents);
            double pxW = img.ImageWidth, pxH = img.ImageHeight;

            // MEASURED: SetClipBoundary(ClipBoundaryType.Invalid, <empty>) and
            // SetClipBoundary(ClipBoundaryType.Rectangle, <2 points>) both throw eInvalidInput -
            // Rectangle is what ClipBoundaryType reports back, not a shape SetClipBoundary
            // accepts as input, and there is no way to hand it an empty boundary. Un-clipping is
            // IsClipped = false alone, leaving the old boundary stored but inactive; clipping
            // always goes through Poly with an EXPLICITLY CLOSED ring (first point repeated).
            int count = a.Points?.Count ?? 0;
            if (count == 0)
            {
                img.IsClipped = false;
            }
            else
            {
                if (count < 2)
                    throw new ArgumentException(
                        "points needs at least 2 (a rectangle, two opposite corners) or 3+ (a polygon).");
                var pts = new Point2dCollection();
                if (count == 2)
                {
                    var p0 = a.Points![0];
                    var p1 = a.Points[1];
                    pts.Add(new Point2d(p0.X, p0.Y));
                    pts.Add(new Point2d(p1.X, p0.Y));
                    pts.Add(new Point2d(p1.X, p1.Y));
                    pts.Add(new Point2d(p0.X, p1.Y));
                    pts.Add(new Point2d(p0.X, p0.Y));
                }
                else
                {
                    foreach (var p in a.Points!) pts.Add(new Point2d(p.X, p.Y));
                    var first = a.Points![0];
                    var last = a.Points[a.Points.Count - 1];
                    if (System.Math.Abs(first.X - last.X) > 1e-9 || System.Math.Abs(first.Y - last.Y) > 1e-9)
                        pts.Add(new Point2d(first.X, first.Y));
                }
                img.SetClipBoundary(ClipBoundaryType.Poly, pts);
                img.IsClipped = true;
            }

            var boundaryBack = img.GetClipBoundary();
            var extentsAfter = AcadEnv.BoundsOf(img.GeometricExtents);

            return Wrap(new
            {
                handle = a.Handle,
                clipped = img.IsClipped,
                boundaryPointCount = boundaryBack?.Count ?? 0,
                imageWidthPx = pxW,
                imageHeightPx = pxH,
                extentsBefore,
                extentsAfter,
                note = "points are in IMAGE PIXEL SPACE - (0,0) to (imageWidthPx, imageHeightPx) - " +
                       "not drawing coordinates. Omitting points removes the clip.",
            });
        });

    // ─────────── adjust ───────────

    private static Task<ToolDispatchResult> SetImageAdjust(JsonObject args, CancellationToken ct) =>
        Run("acad.images.set_image_adjust", args, ct, (doc, db, tr) =>
        {
            var a = Read<ImageAdjustArgsDto>(args);
            if (a.Brightness is null && a.Contrast is null && a.Fade is null)
                throw new ArgumentException(
                    "Nothing to change. Give at least one of brightness, contrast, fade.");
            foreach (var (label, v) in new (string, int?)[]
                     { ("brightness", a.Brightness), ("contrast", a.Contrast), ("fade", a.Fade) })
                if (v is not null && (v < 0 || v > 100))
                    throw new ArgumentException($"{label} runs 0-100, got {v}.");

            var img = RequireImage(db, tr, a.Handle, OpenMode.ForWrite);

            var before = new { brightness = (int)img.Brightness, contrast = (int)img.Contrast, fade = (int)img.Fade };
            var changed = new List<string>();
            if (a.Brightness is not null) { img.Brightness = (byte)a.Brightness.Value; changed.Add("brightness"); }
            if (a.Contrast is not null) { img.Contrast = (byte)a.Contrast.Value; changed.Add("contrast"); }
            if (a.Fade is not null) { img.Fade = (byte)a.Fade.Value; changed.Add("fade"); }
            var after = new { brightness = (int)img.Brightness, contrast = (int)img.Contrast, fade = (int)img.Fade };

            return Wrap(new
            {
                handle = a.Handle,
                before,
                after,
                note = "Only " + string.Join(", ", changed) + " changed; the others are read and " +
                       "reported unchanged. AutoCAD's own defaults are 50/50/0.",
            });
        });

    // ─────────── frame (drawing-wide) ───────────

    private static Task<ToolDispatchResult> SetImageFrame(JsonObject args, CancellationToken ct) =>
        Run("acad.images.set_image_frame", args, ct, (doc, db, tr) =>
        {
            var a = Read<ImageFrameArgsDto>(args);
            if (a.Frame is null || a.Frame < 0 || a.Frame > 2)
                throw new ArgumentException("frame is required and must be 0, 1 or 2.");

            short before = Convert.ToInt16(AcadApp.GetSystemVariable("IMAGEFRAME"));
            AcadApp.SetSystemVariable("IMAGEFRAME", (short)a.Frame.Value);
            short after = Convert.ToInt16(AcadApp.GetSystemVariable("IMAGEFRAME"));

            if (after != a.Frame.Value)
                throw new InvalidOperationException(
                    $"IMAGEFRAME reads back as {after} after being set to {a.Frame.Value}.");

            return Wrap(new
            {
                before = (int)before,
                after = (int)after,
                note = "IMAGEFRAME is drawing-wide, like XCLIPFRAME - it affects every image in " +
                       "the drawing, not one entity, which is why this tool takes no handle.",
            });
        });

    // ─────────── repath ───────────

    private static Task<ToolDispatchResult> SetImagePath(JsonObject args, CancellationToken ct) =>
        Run("acad.images.set_image_path", args, ct, (doc, db, tr) =>
        {
            var a = Read<ImagePathArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.NewPath))
                throw new ArgumentException("newPath is required.");
            if (!File.Exists(a.NewPath))
                throw new ArgumentException($"No file at '{a.NewPath}'.");

            var img = RequireImage(db, tr, a.Handle, OpenMode.ForRead);
            var defId = img.ImageDefId;
            var def = (RasterImageDef)tr.GetObject(defId, OpenMode.ForWrite);
            string previousPath = def.ActiveFileName ?? def.SourceFileName ?? "";

            var dictId = ImageDictId(db);
            string name = "<unknown>";
            if (!dictId.IsNull)
            {
                var dict = (DBDictionary)tr.GetObject(dictId, OpenMode.ForRead);
                name = FindDefName(dict, defId) ?? "<unknown>";
            }

            def.SourceFileName = a.NewPath!;
            def.Load();

            var ms = ModelSpace(db, tr, OpenMode.ForRead);
            var affected = new List<string>();
            foreach (ObjectId eid in ms)
            {
                if (eid.IsErased) continue;
                if (tr.GetObject(eid, OpenMode.ForRead) is RasterImage other && other.ImageDefId == defId)
                    affected.Add(other.Handle.ToString());
            }

            return Wrap(new
            {
                handle = a.Handle,
                name,
                previousPath,
                newPath = def.ActiveFileName,
                loaded = def.IsLoaded,
                affectedHandles = affected,
                note = "Every entity that shares this source definition is affected, listed above " +
                       "by handle, because this changes the definition rather than one placement.",
            });
        });
}
