// Typed DTOs for the acad-furniture category.
// Mirrors plugin-side argument shapes 1-to-1. JsonPropertyName MUST match plugin readers.
// See rule 19-tool-implementation-pattern.mdc and rule 64-furniture-density-per-room.mdc.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Backend.Categories.Furniture;

#region catalog + query

public sealed record ListFurnitureCatalogArgs(
    [property: JsonPropertyName("categoryFilter")] string? CategoryFilter = null,
    [property: JsonPropertyName("domainFilter")]   string? DomainFilter = null);

public sealed record FurnitureCatalogEntry(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("category")]    string Category,
    [property: JsonPropertyName("domain")]      string Domain,
    [property: JsonPropertyName("widthMm")]     double WidthMm,
    [property: JsonPropertyName("depthMm")]     double DepthMm,
    [property: JsonPropertyName("description")] string Description);

public sealed record ListFurnitureCatalogResult(
    [property: JsonPropertyName("entries")] IReadOnlyList<FurnitureCatalogEntry> Entries,
    [property: JsonPropertyName("count")]   int Count);

public sealed record ListFurnitureInModelArgs(
    [property: JsonPropertyName("layerFilter")] string? LayerFilter = null,
    [property: JsonPropertyName("blockFilter")] string? BlockFilter = null);

public sealed record FurnitureRefInfo(
    [property: JsonPropertyName("handle")]   string Handle,
    [property: JsonPropertyName("blockName")] string BlockName,
    [property: JsonPropertyName("layer")]    string Layer,
    [property: JsonPropertyName("position")] Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("invId")]    string? InvId,
    [property: JsonPropertyName("type")]     string? Type,
    [property: JsonPropertyName("note")]     string? Note);

public sealed record ListFurnitureInModelResult(
    [property: JsonPropertyName("references")] IReadOnlyList<FurnitureRefInfo> References,
    [property: JsonPropertyName("count")]      int Count);

#endregion

#region generic insert

public sealed record InsertFurnitureArgs(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("scaleX")]     double ScaleX = 1.0,
    [property: JsonPropertyName("scaleY")]     double ScaleY = 1.0,
    [property: JsonPropertyName("layer")]      string? Layer = null,
    [property: JsonPropertyName("attributes")] Dictionary<string, string>? Attributes = null);

#endregion

#region specialised inserts

public sealed record InsertBedArgs(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("type")]       string Type = "standard",
    [property: JsonPropertyName("invId")]      string? InvId = null,
    [property: JsonPropertyName("room")]       string? Room = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record InsertChairArgs(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("type")]       string Type = "office",
    [property: JsonPropertyName("invId")]      string? InvId = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record InsertDeskArgs(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("widthMm")]    double WidthMm = 1600.0,
    [property: JsonPropertyName("depthMm")]    double DepthMm = 800.0,
    [property: JsonPropertyName("type")]       string Type = "office",
    [property: JsonPropertyName("invId")]      string? InvId = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record InsertCabinetArgs(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("widthMm")]    double WidthMm = 800.0,
    [property: JsonPropertyName("depthMm")]    double DepthMm = 400.0,
    [property: JsonPropertyName("type")]       string Type = "storage",
    [property: JsonPropertyName("invId")]      string? InvId = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record InsertSofaArgs(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("seats")]      int Seats = 3,
    [property: JsonPropertyName("type")]       string Type = "lounge",
    [property: JsonPropertyName("invId")]      string? InvId = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record InsertTableArgs(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg = 0.0,
    [property: JsonPropertyName("shape")]      string Shape = "rectangle",
    [property: JsonPropertyName("widthMm")]    double WidthMm = 1200.0,
    [property: JsonPropertyName("depthMm")]    double DepthMm = 800.0,
    [property: JsonPropertyName("type")]       string Type = "meeting",
    [property: JsonPropertyName("invId")]      string? InvId = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

#endregion

#region populate room

public sealed record PopulateRoomArgs(
    [property: JsonPropertyName("roomBoundaryHandle")] string? RoomBoundaryHandle = null,
    [property: JsonPropertyName("bboxMin")]    Point2dDto? BboxMin = null,
    [property: JsonPropertyName("bboxMax")]    Point2dDto? BboxMax = null,
    [property: JsonPropertyName("preset")]     string Preset = "office",
    [property: JsonPropertyName("orientation")] string Orientation = "north",
    [property: JsonPropertyName("roomName")]   string? RoomName = null,
    [property: JsonPropertyName("layer")]      string? Layer = null);

public sealed record PopulateRoomResult(
    [property: JsonPropertyName("preset")]     string Preset,
    [property: JsonPropertyName("inserted")]   int Inserted,
    [property: JsonPropertyName("handles")]    IReadOnlyList<string> Handles,
    [property: JsonPropertyName("items")]      IReadOnlyList<string> Items,
    [property: JsonPropertyName("warnings")]   IReadOnlyList<string> Warnings);

#endregion

#region common result

public sealed record FurnitureInsertResult(
    [property: JsonPropertyName("entity")]     EntityHandle Entity,
    [property: JsonPropertyName("blockName")]  string BlockName,
    [property: JsonPropertyName("created")]    bool Created,
    [property: JsonPropertyName("widthMm")]    double WidthMm,
    [property: JsonPropertyName("depthMm")]    double DepthMm);

#endregion
