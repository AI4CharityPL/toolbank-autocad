// MCP tool surface for the acad-geo category.
// See rule 19 (impl pattern), 20 ([McpTool]), 21 (naming), 22 (args/results).
//
// MEASURED, and it decides what this category can contain:
//   The class is GeoLocationData, not GeoData, though Database.GeoDataObject points at it.
//   NorthDirection is READ-ONLY and is an ANGLE (a double), not a vector - so there is no
//   set_north_direction tool; it is derived from the design and reference points.
//   There is NO GeoMap type and no GeoMapType/GeoMapResolution enums, so insert_map_image and
//   set_map_image_type are struck - online map imagery is not in this API.
//   list_coordinate_systems and set_coordinate_system collapse into set_geographic_location,
//   which takes the coordinate system by name; AutoCAD exposes no catalogue to enumerate.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Geo;

public static class GeoTools
{
    private const int T_NORMAL = 15_000;

    [McpTool("set_geographic_location", "Give the drawing a place on the Earth: the DESIGN point is a spot in the drawing, and the latitude and longitude say where that spot actually is. Optionally set the coordinate system by name, the altitude, and the horizontal and vertical units. Nothing in the geometry moves - a geographic location says what the existing coordinates MEAN, it does not change them. Latitude and longitude are taken as NAMED arguments rather than as a point on purpose: AutoCAD stores the reference point as (longitude, latitude, altitude), x being longitude, which is the reverse of how people say it aloud and is the easiest thing to get silently wrong. Replacing an existing location is allowed and reported. Read back through Database.GeoDataObject afterwards - a different route from the one that wrote it.", "geo",
        Intent = new[] { "set the geographic location of this drawing", "geolocate this drawing",
                         "ustaw lokalizacje geograficzna rysunku", "put this drawing at a latitude and longitude",
                         "przypisz wspolrzedne geograficzne do rysunku", "set lat long for the model",
                         "where on earth is this drawing" },
        RequiresPlugin = true)]
    public static Task<GeoSetResult> SetGeographicLocation(IPluginGateway gw, GeoSetArgs args, CancellationToken ct)
        => GeoProxy.CallAsync<GeoSetArgs, GeoSetResult>(gw, "acad.geo.set_geographic_location", args, T_NORMAL, ct);

    [McpTool("get_geographic_location", "Read the drawing's geographic location: latitude, longitude and altitude, plus the design point, coordinate system, units and north angle. Read-only. Latitude and longitude are pulled out BY NAME because the reference point stores them as (longitude, latitude, altitude) and reading that point positionally is the classic geo-data mistake. The north direction is reported as an ANGLE and is read-only in the API - it is derived from the design and reference points rather than stored, which is why no tool sets it. Refuses when the drawing has no location rather than answering with zeros.", "geo",
        Intent = new[] { "where is this drawing located", "get the geographic location",
                         "odczytaj lokalizacje geograficzna", "what latitude and longitude is this drawing",
                         "jakie wspolrzedne geograficzne ma rysunek", "is this drawing geolocated",
                         "show the geo coordinates" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<GeoGetResult> GetGeographicLocation(IPluginGateway gw, GeoNoArgs args, CancellationToken ct)
        => GeoProxy.CallAsync<GeoNoArgs, GeoGetResult>(gw, "acad.geo.get_geographic_location", args, T_NORMAL, ct);

    [McpTool("remove_geolocation", "Remove the drawing's geographic location. The previous location is reported so it can be put back. Nothing in the geometry moves - a location tells you what the coordinates mean on the Earth and does not change them - and any geo markers already placed stay exactly where they are, becoming ordinary annotation. Afterwards the conversion tools refuse until a location is set again. Verified gone by reading Database.GeoDataObject back.", "geo",
        Intent = new[] { "remove the geographic location", "ungeolocate this drawing",
                         "usun lokalizacje geograficzna", "clear the geo data from this drawing",
                         "skasuj wspolrzedne geograficzne rysunku", "detach the drawing from its map position",
                         "delete geolocation" },
        RequiresPlugin = true)]
    public static Task<GeoRemoveResult> RemoveGeolocation(IPluginGateway gw, GeoNoArgs args, CancellationToken ct)
        => GeoProxy.CallAsync<GeoNoArgs, GeoRemoveResult>(gw, "acad.geo.remove_geolocation", args, T_NORMAL, ct);

    [McpTool("convert_wcs_to_geo", "Convert a drawing point into latitude, longitude and altitude. Read-only. The result is broken out BY NAME because AutoCAD returns it as a point carrying (longitude, latitude, altitude) - x is longitude - and handing that back as a bare point invites reading it the way people speak, latitude first, which is a mistake with no symptom until the position turns out to be somewhere else entirely. convert_geo_to_wcs is the exact inverse: feeding one result into the other returns where you started. Requires the drawing to have a geographic location.", "geo",
        Intent = new[] { "convert this point to latitude and longitude", "what are the gps coordinates of this point",
                         "przelicz punkt na wspolrzedne geograficzne", "where on earth is this drawing point",
                         "zamien wspolrzedne rysunku na geograficzne", "wcs to lat long",
                         "get the real world position of a point" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<GeoToLatLonResult> ConvertWcsToGeo(IPluginGateway gw, GeoPointArgs args, CancellationToken ct)
        => GeoProxy.CallAsync<GeoPointArgs, GeoToLatLonResult>(gw, "acad.geo.convert_wcs_to_geo", args, T_NORMAL, ct);

    [McpTool("convert_geo_to_wcs", "Convert a latitude and longitude into a drawing point. Read-only. The exact inverse of convert_wcs_to_geo - feeding one result into the other returns the position you started from, which is the check worth running when a location looks wrong. Latitude and longitude are taken as NAMED arguments rather than a point, because the API orders them longitude-first and the two are trivially swapped when positional. Requires the drawing to have a geographic location.", "geo",
        Intent = new[] { "convert latitude and longitude to a drawing point", "where does this gps position land in the drawing",
                         "przelicz wspolrzedne geograficzne na punkt", "put this lat long into model coordinates",
                         "zamien wspolrzedne geograficzne na rysunkowe", "lat long to wcs",
                         "find the drawing point for a real world position" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<GeoToWcsResult> ConvertGeoToWcs(IPluginGateway gw, GeoLatLonArgs args, CancellationToken ct)
        => GeoProxy.CallAsync<GeoLatLonArgs, GeoToWcsResult>(gw, "acad.geo.convert_geo_to_wcs", args, T_NORMAL, ct);

    [McpTool("place_geo_marker", "Place a geo marker - an entity that records a position on the Earth, with optional notes. Give EITHER a drawing point OR a latitude and longitude, not both: they could disagree, and this tool should not be the one deciding which to believe. The result reports both the drawing point and the latitude and longitude it corresponds to, converted back through the geo data - so placing by coordinates shows you where that landed in the drawing, and placing by point shows you where on Earth it is. Requires the drawing to have a geographic location.", "geo",
        Intent = new[] { "place a geo marker", "mark this position on the map",
                         "wstaw znacznik geograficzny", "put a marker at this latitude and longitude",
                         "oznacz punkt geograficzny na rysunku", "add a gps marker",
                         "record a real world position in the drawing" },
        RequiresPlugin = true)]
    public static Task<GeoMarkerResult> PlaceGeoMarker(IPluginGateway gw, GeoMarkerArgs args, CancellationToken ct)
        => GeoProxy.CallAsync<GeoMarkerArgs, GeoMarkerResult>(gw, "acad.geo.place_geo_marker", args, T_NORMAL, ct);

    [McpTool("list_geo_markers", "List the geo markers in model space, each with its drawing position, notes, and the latitude and longitude it corresponds to. Read-only. IMPORTANT for reading the result: the coordinates are COMPUTED from the drawing position through the current geo data, so they are absent when the drawing has no geographic location - the markers are still listed, because they are real entities either way, and `hasGeoLocation` says which case you are looking at rather than leaving empty coordinates to be guessed at.", "geo",
        Intent = new[] { "list the geo markers", "what positions are marked in this drawing",
                         "lista znacznikow geograficznych", "show all gps markers",
                         "jakie znaczniki geograficzne sa na rysunku", "find geo markers",
                         "list marked real world positions" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<GeoMarkerListResult> ListGeoMarkers(IPluginGateway gw, GeoNoArgs args, CancellationToken ct)
        => GeoProxy.CallAsync<GeoNoArgs, GeoMarkerListResult>(gw, "acad.geo.list_geo_markers", args, T_NORMAL, ct);
}
