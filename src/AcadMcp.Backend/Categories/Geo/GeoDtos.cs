// Typed DTOs for the acad-geo category. Mirrors plugin-side wire shape.
// See rule 19-tool-implementation-pattern.md.

using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Geo;

// NULLABLE on purpose, and it is not a style choice. A non-nullable double turns an OMITTED
// argument into 0.0, and 0 is a perfectly valid latitude and longitude - the Gulf of Guinea. The
// tool would then set a location nobody asked for and report success. Nullable lets the plugin
// refuse a missing value instead of silently inventing one.
public sealed record GeoSetArgs(
    [property: JsonPropertyName("latitude")]         double? Latitude = null,
    [property: JsonPropertyName("longitude")]        double? Longitude = null,
    [property: JsonPropertyName("altitude")]         double? Altitude = null,
    [property: JsonPropertyName("designPoint")]      Point3dDto? DesignPoint = null,
    [property: JsonPropertyName("coordinateSystem")] string? CoordinateSystem = null,
    [property: JsonPropertyName("horizontalUnits")]  string? HorizontalUnits = null,
    [property: JsonPropertyName("verticalUnits")]    string? VerticalUnits = null);

public sealed record GeoNoArgs();

public sealed record GeoPointArgs(
    [property: JsonPropertyName("point")] Point3dDto Point);

// Nullable for the same reason as GeoSetArgs: an omitted coordinate must be refused, not
// defaulted to a real place on the Earth.
public sealed record GeoLatLonArgs(
    [property: JsonPropertyName("latitude")]  double? Latitude = null,
    [property: JsonPropertyName("longitude")] double? Longitude = null,
    [property: JsonPropertyName("altitude")]  double? Altitude = null);

public sealed record GeoMarkerArgs(
    [property: JsonPropertyName("point")]      Point3dDto? Point = null,
    [property: JsonPropertyName("latitude")]   double? Latitude = null,
    [property: JsonPropertyName("longitude")]  double? Longitude = null,
    [property: JsonPropertyName("altitude")]   double? Altitude = null,
    [property: JsonPropertyName("notes")]      string? Notes = null,
    [property: JsonPropertyName("landingGap")] double? LandingGap = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record GeoLocationInfo(
    [property: JsonPropertyName("coordinateSystem")]     string? CoordinateSystem,
    [property: JsonPropertyName("designPoint")]          Point3dDto DesignPoint,
    [property: JsonPropertyName("referencePoint")]       Point3dDto ReferencePoint,
    [property: JsonPropertyName("northDirectionAngle")]  double NorthDirectionAngle,
    [property: JsonPropertyName("horizontalUnits")]      string? HorizontalUnits,
    [property: JsonPropertyName("verticalUnits")]        string? VerticalUnits,
    [property: JsonPropertyName("upDirection")]          Point3dDto UpDirection,
    [property: JsonPropertyName("seaLevelElevation")]    double SeaLevelElevation,
    [property: JsonPropertyName("doSeaLevelCorrection")] bool DoSeaLevelCorrection,
    [property: JsonPropertyName("typeOfCoordinates")]    string? TypeOfCoordinates);

public sealed record GeoSetResult(
    [property: JsonPropertyName("replaced")]  bool Replaced,
    [property: JsonPropertyName("latitude")]  double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("altitude")]  double Altitude,
    [property: JsonPropertyName("location")]  GeoLocationInfo Location,
    [property: JsonPropertyName("note")]      string Note);

public sealed record GeoGetResult(
    [property: JsonPropertyName("latitude")]  double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("altitude")]  double Altitude,
    [property: JsonPropertyName("location")]  GeoLocationInfo Location,
    [property: JsonPropertyName("note")]      string Note);

public sealed record GeoRemoveResult(
    [property: JsonPropertyName("removed")]  bool Removed,
    [property: JsonPropertyName("previous")] GeoLocationInfo Previous,
    [property: JsonPropertyName("note")]     string Note);

public sealed record GeoToLatLonResult(
    [property: JsonPropertyName("point")]     Point3dDto Point,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("latitude")]  double Latitude,
    [property: JsonPropertyName("altitude")]  double Altitude,
    [property: JsonPropertyName("note")]      string Note);

public sealed record GeoToWcsResult(
    [property: JsonPropertyName("latitude")]  double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("altitude")]  double Altitude,
    [property: JsonPropertyName("point")]     Point3dDto Point,
    [property: JsonPropertyName("note")]      string Note);

public sealed record GeoMarkerResult(
    [property: JsonPropertyName("entity")]    EntityHandle Entity,
    [property: JsonPropertyName("position")]  Point3dDto Position,
    [property: JsonPropertyName("latitude")]  double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("altitude")]  double Altitude,
    [property: JsonPropertyName("notes")]     string? Notes,
    [property: JsonPropertyName("note")]      string Note);

public sealed record GeoMarkerInfo(
    [property: JsonPropertyName("handle")]    string Handle,
    [property: JsonPropertyName("position")]  Point3dDto Position,
    [property: JsonPropertyName("latitude")]  double? Latitude,
    [property: JsonPropertyName("longitude")] double? Longitude,
    [property: JsonPropertyName("altitude")]  double? Altitude,
    [property: JsonPropertyName("notes")]     string? Notes,
    [property: JsonPropertyName("layer")]     string? Layer);

public sealed record GeoMarkerListResult(
    [property: JsonPropertyName("count")]           int Count,
    [property: JsonPropertyName("hasGeoLocation")]  bool HasGeoLocation,
    [property: JsonPropertyName("markers")]         IReadOnlyList<GeoMarkerInfo> Markers,
    [property: JsonPropertyName("note")]            string Note);
