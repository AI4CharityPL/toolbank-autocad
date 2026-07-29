// Plugin-side DTOs for acad-furniture. Must stay binary-compatible with
// the backend DTOs in Categories/Furniture/FurnitureDtos.cs (JsonPropertyName).

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

// ── catalog / query ────────────────────────────────────────────────────
internal sealed record FurnitureListCatalogArgsDto(
    [property: JsonPropertyName("categoryFilter")] string? CategoryFilter,
    [property: JsonPropertyName("domainFilter")]   string? DomainFilter);

internal sealed record FurnitureListInModelArgsDto(
    [property: JsonPropertyName("layerFilter")] string? LayerFilter,
    [property: JsonPropertyName("blockFilter")] string? BlockFilter);

// ── generic insert ─────────────────────────────────────────────────────
internal sealed record FurnitureInsertGenericDto(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("scaleX")]     double ScaleX,
    [property: JsonPropertyName("scaleY")]     double ScaleY,
    [property: JsonPropertyName("layer")]      string? Layer,
    [property: JsonPropertyName("attributes")] Dictionary<string, string>? Attributes);

// ── specialised inserts ────────────────────────────────────────────────
internal sealed record FurnitureInsertBedDto(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("type")]       string Type,
    [property: JsonPropertyName("invId")]      string? InvId,
    [property: JsonPropertyName("room")]       string? Room,
    [property: JsonPropertyName("layer")]      string? Layer);

internal sealed record FurnitureInsertChairDto(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("type")]       string Type,
    [property: JsonPropertyName("invId")]      string? InvId,
    [property: JsonPropertyName("layer")]      string? Layer);

internal sealed record FurnitureInsertDeskDto(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("widthMm")]    double WidthMm,
    [property: JsonPropertyName("depthMm")]    double DepthMm,
    [property: JsonPropertyName("type")]       string Type,
    [property: JsonPropertyName("invId")]      string? InvId,
    [property: JsonPropertyName("layer")]      string? Layer);

internal sealed record FurnitureInsertCabinetDto(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("widthMm")]    double WidthMm,
    [property: JsonPropertyName("depthMm")]    double DepthMm,
    [property: JsonPropertyName("type")]       string Type,
    [property: JsonPropertyName("invId")]      string? InvId,
    [property: JsonPropertyName("layer")]      string? Layer);

internal sealed record FurnitureInsertSofaDto(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("seats")]      int Seats,
    [property: JsonPropertyName("type")]       string Type,
    [property: JsonPropertyName("invId")]      string? InvId,
    [property: JsonPropertyName("layer")]      string? Layer);

internal sealed record FurnitureInsertTableDto(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("shape")]      string Shape,
    [property: JsonPropertyName("widthMm")]    double WidthMm,
    [property: JsonPropertyName("depthMm")]    double DepthMm,
    [property: JsonPropertyName("type")]       string Type,
    [property: JsonPropertyName("invId")]      string? InvId,
    [property: JsonPropertyName("layer")]      string? Layer);

// ── populate room ──────────────────────────────────────────────────────
internal sealed record FurniturePopulateRoomDto(
    [property: JsonPropertyName("roomBoundaryHandle")] string? RoomBoundaryHandle,
    [property: JsonPropertyName("bboxMin")]    Point2dDto? BboxMin,
    [property: JsonPropertyName("bboxMax")]    Point2dDto? BboxMax,
    [property: JsonPropertyName("preset")]     string Preset,
    [property: JsonPropertyName("orientation")] string Orientation,
    [property: JsonPropertyName("roomName")]   string? RoomName,
    [property: JsonPropertyName("layer")]      string? Layer);
