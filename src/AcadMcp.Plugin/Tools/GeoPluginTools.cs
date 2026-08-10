// AutoCAD plugin handlers for the acad-geo category.
// Registered under "acad.geo.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern), 26 (traps).
//
// MEASURED API shape, because two of the obvious names are wrong and one type is simply absent:
//
//   The class is GeoLocationData, NOT GeoData - though Database.GeoDataObject is the property
//   that points at it, which is what makes the wrong name so easy to reach for.
//
//   GeoLocationData.NorthDirection is READ-ONLY, and it is a double - an ANGLE - not a
//   vector. It is derived rather than set, so there is no set_north_direction tool.
//
//   There is no GeoMap type, and no GeoMapType or GeoMapResolution enum, so insert_map_image and
//   set_map_image_type are not buildable here - online map imagery is not in this API.

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
using AcadRt = Autodesk.AutoCAD.Runtime;

namespace AcadMcp.Plugin.Tools;

internal static class GeoPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.geo.set_geographic_location", SetGeographicLocation);
        host.Register("acad.geo.get_geographic_location", GetGeographicLocation);
        host.Register("acad.geo.remove_geolocation",      RemoveGeolocation);
        host.Register("acad.geo.convert_wcs_to_geo",      ConvertWcsToGeo);
        host.Register("acad.geo.convert_geo_to_wcs",      ConvertGeoToWcs);
        host.Register("acad.geo.place_geo_marker",        PlaceGeoMarker);
        host.Register("acad.geo.list_geo_markers",        ListGeoMarkers);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    /// MEASURED, and it is the trap of this whole category: Database.GeoDataObject THROWS
    /// eNullObjectId when the drawing has no geographic location - it does not return a null
    /// ObjectId. So `if (db.GeoDataObject.IsNull)` never gets the chance to run, and every tool
    /// here fails with an error about a null object id instead of saying what is actually wrong.
    /// Asking whether a drawing is geolocated therefore means catching, not testing.
    private static GeoLocationData? TryGetGeo(Database db, Transaction tr, OpenMode mode)
    {
        ObjectId id;
        try { id = db.GeoDataObject; }
        catch (AcadRt.Exception) { return null; }
        if (id.IsNull) return null;
        return (GeoLocationData)tr.GetObject(id, mode);
    }

    /// The drawing's geo data, or a refusal that says how to create it.
    private static GeoLocationData RequireGeo(Database db, Transaction tr, OpenMode mode)
        => TryGetGeo(db, tr, mode)
           ?? throw new ArgumentException(
               "This drawing has no geographic location. set_geographic_location gives it one; " +
               "until then a drawing's coordinates mean nothing in particular on the Earth.");

    private static object Describe(GeoLocationData g) => new
    {
        coordinateSystem = g.CoordinateSystem,
        designPoint = AcadEnv.FromPoint3d(g.DesignPoint),
        referencePoint = AcadEnv.FromPoint3d(g.ReferencePoint),
        // MEASURED: NorthDirection is a double - an ANGLE - not a vector, and it is read-only.
        northDirectionAngle = g.NorthDirection,
        horizontalUnits = g.HorizontalUnits.ToString(),
        verticalUnits = g.VerticalUnits.ToString(),
        upDirection = AcadEnv.FromPoint3d(new Point3d(
            g.UpDirection.X, g.UpDirection.Y, g.UpDirection.Z)),
        seaLevelElevation = g.SeaLevelElevation,
        doSeaLevelCorrection = g.DoSeaLevelCorrection,
        typeOfCoordinates = g.TypeOfCoordinates.ToString(),
    };

    // ─────────── the location itself ───────────

    private static Task<ToolDispatchResult> SetGeographicLocation(JsonObject args, CancellationToken ct) =>
        Run("acad.geo.set_geographic_location", args, ct, (doc, db, tr) =>
        {
            var a = Read<GeoArgsDto>(args);
            if (a.Latitude is null || a.Longitude is null)
                throw new ArgumentException(
                    "latitude and longitude are required: where in the world the design point is.");
            if (a.Latitude < -90 || a.Latitude > 90)
                throw new ArgumentException("latitude must be between -90 and 90 degrees.");
            if (a.Longitude < -180 || a.Longitude > 180)
                throw new ArgumentException("longitude must be between -180 and 180 degrees.");

            var existingGeo = TryGetGeo(db, tr, OpenMode.ForWrite);
            var replaced = existingGeo is not null;
            var g = existingGeo ?? new GeoLocationData();

            if (!replaced) g.BlockTableRecordId = db.CurrentSpaceId;

            // The DESIGN point is the spot in the drawing; the REFERENCE point is where that spot
            // is on the Earth, carried as longitude, latitude and altitude in that order - x is
            // longitude, not latitude, which is the reverse of how people say it aloud.
            g.DesignPoint = a.DesignPoint is not null ? AcadEnv.ToPoint3d(a.DesignPoint) : Point3d.Origin;
            g.ReferencePoint = new Point3d(a.Longitude.Value, a.Latitude.Value, a.Altitude ?? 0.0);
            if (!string.IsNullOrWhiteSpace(a.CoordinateSystem))
                g.CoordinateSystem = a.CoordinateSystem!;
            g.HorizontalUnits = ParseUnits(a.HorizontalUnits) ?? g.HorizontalUnits;
            g.VerticalUnits = ParseUnits(a.VerticalUnits) ?? g.VerticalUnits;

            if (!replaced)
            {
                g.PostToDb();
                tr.AddNewlyCreatedDBObject(g, true);
            }

            // Read back through Database.GeoDataObject, a different route from the object just
            // written - a location that did not attach would otherwise look identical.
            var back = RequireGeo(db, tr, OpenMode.ForRead);
            if (Math.Abs(back.ReferencePoint.Y - a.Latitude.Value) > 1e-9
                || Math.Abs(back.ReferencePoint.X - a.Longitude.Value) > 1e-9)
                throw new InvalidOperationException(
                    "The location reads back as longitude " + back.ReferencePoint.X + ", latitude " +
                    back.ReferencePoint.Y + " rather than what was set.");

            return Wrap(new
            {
                replaced,
                latitude = back.ReferencePoint.Y,
                longitude = back.ReferencePoint.X,
                altitude = back.ReferencePoint.Z,
                location = Describe(back),
                note = "The DESIGN point is a spot in the drawing and the REFERENCE point is where " +
                       "that spot sits on the Earth. Note the ordering trap: AutoCAD carries the " +
                       "reference point as (longitude, latitude, altitude) - x is LONGITUDE, the " +
                       "reverse of how people say it aloud - which is why this tool takes them as " +
                       "named arguments and never as a raw point. northDirection is derived from " +
                       "the geo data and is READ-ONLY in the API, so it is reported and cannot be " +
                       "set here.",
            });
        });

    private static UnitsValue? ParseUnits(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (Enum.TryParse<UnitsValue>(s, ignoreCase: true, out var v)) return v;
        throw new ArgumentException(
            "Unknown unit '" + s + "'. Use an AutoCAD unit name such as Meters, Millimeters, " +
            "Feet or Inches.");
    }

    private static Task<ToolDispatchResult> GetGeographicLocation(JsonObject args, CancellationToken ct) =>
        Run("acad.geo.get_geographic_location", args, ct, (doc, db, tr) =>
        {
            var g = RequireGeo(db, tr, OpenMode.ForRead);
            return Wrap(new
            {
                latitude = g.ReferencePoint.Y,
                longitude = g.ReferencePoint.X,
                altitude = g.ReferencePoint.Z,
                location = Describe(g),
                note = "latitude and longitude are pulled out by name because the reference point " +
                       "carries them as (longitude, latitude, altitude) - x is LONGITUDE - and " +
                       "reading that point positionally is the easiest mistake to make with geo " +
                       "data. northDirection is derived rather than stored and cannot be set.",
            });
        });

    private static Task<ToolDispatchResult> RemoveGeolocation(JsonObject args, CancellationToken ct) =>
        Run("acad.geo.remove_geolocation", args, ct, (doc, db, tr) =>
        {
            var g = RequireGeo(db, tr, OpenMode.ForWrite);
            var was = Describe(g);
            g.EraseFromDb();

            if (TryGetGeo(db, tr, OpenMode.ForRead) is not null)
                throw new InvalidOperationException(
                    "The geo data still reads back from the database after being erased.");

            return Wrap(new
            {
                removed = true,
                previous = was,
                note = "The drawing no longer has a geographic location, so the conversion tools " +
                       "will refuse until one is set again. Nothing in the geometry moves - a " +
                       "location tells you what the coordinates MEAN on the Earth and does not " +
                       "change them. Any geo markers already placed stay where they are; they " +
                       "become ordinary annotation.",
            });
        });

    // ─────────── conversions ───────────

    private static Task<ToolDispatchResult> ConvertWcsToGeo(JsonObject args, CancellationToken ct) =>
        Run("acad.geo.convert_wcs_to_geo", args, ct, (doc, db, tr) =>
        {
            var a = Read<GeoArgsDto>(args);
            if (a.Point is null)
                throw new ArgumentException("point is required: the drawing point to locate.");
            var g = RequireGeo(db, tr, OpenMode.ForRead);

            var wcs = AcadEnv.ToPoint3d(a.Point);
            Point3d lla;
            try { lla = g.TransformToLonLatAlt(wcs); }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD could not convert that point with " + ex.ErrorStatus +
                    ". The coordinate system on the geo data may not support the conversion.");
            }

            return Wrap(new
            {
                point = AcadEnv.FromPoint3d(wcs),
                longitude = lla.X,
                latitude = lla.Y,
                altitude = lla.Z,
                note = "The result is broken out by NAME because AutoCAD returns it as a Point3d " +
                       "carrying (longitude, latitude, altitude) - x is longitude - and handing " +
                       "that back as a bare point would invite reading it the way people speak, " +
                       "latitude first, which is a mistake with no symptom until the position is " +
                       "somewhere else entirely. convert_geo_to_wcs is the exact inverse.",
            });
        });

    private static Task<ToolDispatchResult> ConvertGeoToWcs(JsonObject args, CancellationToken ct) =>
        Run("acad.geo.convert_geo_to_wcs", args, ct, (doc, db, tr) =>
        {
            var a = Read<GeoArgsDto>(args);
            if (a.Latitude is null || a.Longitude is null)
                throw new ArgumentException("latitude and longitude are required.");
            if (a.Latitude < -90 || a.Latitude > 90)
                throw new ArgumentException("latitude must be between -90 and 90 degrees.");
            if (a.Longitude < -180 || a.Longitude > 180)
                throw new ArgumentException("longitude must be between -180 and 180 degrees.");
            var g = RequireGeo(db, tr, OpenMode.ForRead);

            Point3d wcs;
            try
            {
                // Longitude FIRST - the same ordering the API uses everywhere in geo data.
                wcs = g.TransformFromLonLatAlt(
                    new Point3d(a.Longitude.Value, a.Latitude.Value, a.Altitude ?? 0.0));
            }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD could not convert that position with " + ex.ErrorStatus + ".");
            }

            return Wrap(new
            {
                latitude = a.Latitude, longitude = a.Longitude, altitude = a.Altitude ?? 0.0,
                point = AcadEnv.FromPoint3d(wcs),
                note = "The exact inverse of convert_wcs_to_geo: feeding this result back through " +
                       "that tool returns the position you started from. Latitude and longitude " +
                       "are taken as named arguments rather than as a point, because the API " +
                       "orders them longitude-first and the two are easy to swap when they are " +
                       "positional.",
            });
        });

    // ─────────── markers ───────────

    private static Task<ToolDispatchResult> PlaceGeoMarker(JsonObject args, CancellationToken ct) =>
        Run("acad.geo.place_geo_marker", args, ct, (doc, db, tr) =>
        {
            var a = Read<GeoArgsDto>(args);

            // Argument validation comes FIRST, before the geo data is required. Otherwise these
            // refusals fire with eNullObjectId on a drawing that has no location, which is a
            // refusal for the wrong reason - it passes a test while proving nothing.
            //
            // Either a drawing point or a latitude and longitude - one of them, not both, because
            // accepting both would leave the tool deciding which to believe.
            bool byPoint = a.Point is not null;
            bool byLatLon = a.Latitude is not null || a.Longitude is not null;
            if (byPoint && byLatLon)
                throw new ArgumentException(
                    "Give either point, or latitude and longitude - not both, because they could " +
                    "disagree and this tool should not be the one deciding which to believe.");
            if (!byPoint && !byLatLon)
                throw new ArgumentException("Give either point, or latitude and longitude.");

            var g = RequireGeo(db, tr, OpenMode.ForRead);

            Point3d wcs;
            if (byPoint) wcs = AcadEnv.ToPoint3d(a.Point!);
            else
            {
                if (a.Latitude is null || a.Longitude is null)
                    throw new ArgumentException("Both latitude and longitude are required.");
                wcs = g.TransformFromLonLatAlt(
                    new Point3d(a.Longitude.Value, a.Latitude.Value, a.Altitude ?? 0.0));
            }

            var marker = new GeoPositionMarker { Position = wcs };
            if (!string.IsNullOrWhiteSpace(a.Notes)) marker.Notes = a.Notes!;
            if (a.LandingGap is not null) marker.LandingGap = a.LandingGap.Value;

            var handle = AcadEnv.Persist(db, tr, marker, a.Layer);

            var lla = g.TransformToLonLatAlt(marker.Position);
            return Wrap(new
            {
                entity = handle,
                position = AcadEnv.FromPoint3d(marker.Position),
                latitude = lla.Y,
                longitude = lla.X,
                altitude = lla.Z,
                notes = marker.Notes,
                note = "A geo marker is an ENTITY that records a position on the Earth. The result " +
                       "reports both the drawing point and the latitude and longitude it " +
                       "corresponds to, converted back through the geo data - so if you placed it " +
                       "by coordinates you can see where that landed in the drawing, and if you " +
                       "placed it by point you can see where on Earth it is.",
            });
        });

    private static Task<ToolDispatchResult> ListGeoMarkers(JsonObject args, CancellationToken ct) =>
        Run("acad.geo.list_geo_markers", args, ct, (doc, db, tr) =>
        {
            var g = TryGetGeo(db, tr, OpenMode.ForRead);

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var found = new List<object>();
            foreach (ObjectId id in ms)
            {
                if (id.IsErased) continue;                     // rule 26 §8
                if (tr.GetObject(id, OpenMode.ForRead) is not GeoPositionMarker m) continue;
                double? lat = null, lon = null, alt = null;
                if (g is not null)
                {
                    try
                    {
                        var lla = g.TransformToLonLatAlt(m.Position);
                        lon = lla.X; lat = lla.Y; alt = lla.Z;
                    }
                    catch { /* a marker outside the projection is still a marker */ }
                }
                found.Add(new
                {
                    handle = m.Handle.ToString(),
                    position = AcadEnv.FromPoint3d(m.Position),
                    latitude = lat, longitude = lon, altitude = alt,
                    notes = m.Notes,
                    layer = m.Layer,
                });
            }

            return Wrap(new
            {
                count = found.Count,
                hasGeoLocation = g is not null,
                markers = found,
                note = "Model space only. Latitude and longitude are computed from the drawing " +
                       "position through the current geo data, so they are ABSENT when the drawing " +
                       "has no geographic location - the markers are still listed, because they " +
                       "are real entities either way, and hasGeoLocation says which case you are " +
                       "looking at rather than leaving empty coordinates to be guessed at.",
            });
        });
}
