// Typed DTOs for acad-plumbing. See rule 19-tool-implementation-pattern
// and rule 63-sanitary-fixtures-wt for the block catalogue.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Plumbing;

#region catalog + query

public sealed record ListPlumbingCatalogArgs(
    [property: JsonPropertyName("categoryFilter")] string? CategoryFilter = null,
    [property: JsonPropertyName("domainFilter")]   string? DomainFilter = null,
    [property: JsonPropertyName("accessibleOnly")] bool AccessibleOnly = false);

public sealed record PlumbingCatalogEntry(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("category")]    string Category,
    [property: JsonPropertyName("domain")]      string Domain,
    [property: JsonPropertyName("widthMm")]     double WidthMm,
    [property: JsonPropertyName("depthMm")]     double DepthMm,
    [property: JsonPropertyName("accessible")]  bool Accessible,
    [property: JsonPropertyName("standard")]    string Standard,
    [property: JsonPropertyName("description")] string Description);

public sealed record ListPlumbingCatalogResult(
    [property: JsonPropertyName("entries")] IReadOnlyList<PlumbingCatalogEntry> Entries,
    [property: JsonPropertyName("count")]   int Count);

public sealed record ListPlumbingInModelArgs(
    [property: JsonPropertyName("layerFilter")] string? LayerFilter = null,
    [property: JsonPropertyName("blockFilter")] string? BlockFilter = null);

public sealed record PlumbingRefInfo(
    [property: JsonPropertyName("handle")]      string Handle,
    [property: JsonPropertyName("blockName")]   string BlockName,
    [property: JsonPropertyName("layer")]       string Layer,
    [property: JsonPropertyName("position")]    Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("invId")]       string? InvId,
    [property: JsonPropertyName("type")]        string? Type,
    [property: JsonPropertyName("accessible")]  bool Accessible);

public sealed record ListPlumbingInModelResult(
    [property: JsonPropertyName("references")] IReadOnlyList<PlumbingRefInfo> References,
    [property: JsonPropertyName("count")]      int Count);

#endregion

#region inserts

public sealed record InsertPlumbingArgs(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("scaleX")]     double ScaleX = 1.0,
    [property: JsonPropertyName("scaleY")]     double ScaleY = 1.0,
    [property: JsonPropertyName("layer")]      string? Layer = null,
    [property: JsonPropertyName("attributes")] Dictionary<string, string>? Attributes = null);

public sealed record InsertWcArgs(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("type")]       string Type = "floor-standing",
    [property: JsonPropertyName("accessible")] bool Accessible = false,
    [property: JsonPropertyName("invId")]      string? InvId = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record InsertBasinArgs(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("type")]       string Type = "standard",
    [property: JsonPropertyName("widthMm")]    double WidthMm = 600.0,
    [property: JsonPropertyName("accessible")] bool Accessible = false,
    [property: JsonPropertyName("invId")]      string? InvId = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record InsertShowerArgs(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("shape")]      string Shape = "square",
    [property: JsonPropertyName("widthMm")]    double WidthMm = 900.0,
    [property: JsonPropertyName("depthMm")]    double DepthMm = 900.0,
    [property: JsonPropertyName("walkIn")]     bool WalkIn = false,
    [property: JsonPropertyName("invId")]      string? InvId = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record InsertBathtubArgs(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("widthMm")]    double WidthMm = 1700.0,
    [property: JsonPropertyName("depthMm")]    double DepthMm = 700.0,
    [property: JsonPropertyName("type")]       string Type = "standard",
    [property: JsonPropertyName("invId")]      string? InvId = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record InsertUrinalArgs(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("accessible")] bool Accessible = false,
    [property: JsonPropertyName("invId")]      string? InvId = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

#endregion

#region populate

public sealed record PopulateBathroomArgs(
    [property: JsonPropertyName("roomBoundaryHandle")] string? RoomBoundaryHandle = null,
    [property: JsonPropertyName("bboxMin")]    Point2dDto? BboxMin = null,
    [property: JsonPropertyName("bboxMax")]    Point2dDto? BboxMax = null,
    [property: JsonPropertyName("preset")]     string Preset = "wc-public",
    [property: JsonPropertyName("accessible")] bool Accessible = false,
    [property: JsonPropertyName("orientation")] string Orientation = "north",
    [property: JsonPropertyName("roomName")]   string? RoomName = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record PopulateBathroomResult(
    [property: JsonPropertyName("preset")]     string Preset,
    [property: JsonPropertyName("accessible")] bool Accessible,
    [property: JsonPropertyName("inserted")]   int Inserted,
    [property: JsonPropertyName("handles")]    IReadOnlyList<string> Handles,
    [property: JsonPropertyName("items")]      IReadOnlyList<string> Items,
    [property: JsonPropertyName("warnings")]   IReadOnlyList<string> Warnings);

#endregion

#region result

public sealed record PlumbingInsertResult(
    [property: JsonPropertyName("entity")]     EntityHandle Entity,
    [property: JsonPropertyName("blockName")]  string BlockName,
    [property: JsonPropertyName("created")]    bool Created,
    [property: JsonPropertyName("widthMm")]    double WidthMm,
    [property: JsonPropertyName("depthMm")]    double DepthMm,
    [property: JsonPropertyName("accessible")] bool Accessible);

#endregion
