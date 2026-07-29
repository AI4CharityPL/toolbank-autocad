// Plugin-side DTOs for acad-plumbing. JsonPropertyName MUST match the backend
// DTOs in Categories/Plumbing/PlumbingDtos.cs.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record PlumbListCatalogDto(
    [property: JsonPropertyName("categoryFilter")] string? CategoryFilter,
    [property: JsonPropertyName("domainFilter")]   string? DomainFilter,
    [property: JsonPropertyName("accessibleOnly")] bool AccessibleOnly);

internal sealed record PlumbListInModelDto(
    [property: JsonPropertyName("layerFilter")] string? LayerFilter,
    [property: JsonPropertyName("blockFilter")] string? BlockFilter);

internal sealed record PlumbInsertGenericDto(
    [property: JsonPropertyName("name")]       string Name,
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("scaleX")]     double ScaleX,
    [property: JsonPropertyName("scaleY")]     double ScaleY,
    [property: JsonPropertyName("layer")]      string? Layer,
    [property: JsonPropertyName("attributes")] Dictionary<string, string>? Attributes);

internal sealed record PlumbInsertWcDto(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("type")]       string Type,
    [property: JsonPropertyName("accessible")] bool Accessible,
    [property: JsonPropertyName("invId")]      string? InvId,
    [property: JsonPropertyName("layer")]      string? Layer);

internal sealed record PlumbInsertBasinDto(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("type")]       string Type,
    [property: JsonPropertyName("widthMm")]    double WidthMm,
    [property: JsonPropertyName("accessible")] bool Accessible,
    [property: JsonPropertyName("invId")]      string? InvId,
    [property: JsonPropertyName("layer")]      string? Layer);

internal sealed record PlumbInsertShowerDto(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("shape")]      string Shape,
    [property: JsonPropertyName("widthMm")]    double WidthMm,
    [property: JsonPropertyName("depthMm")]    double DepthMm,
    [property: JsonPropertyName("walkIn")]     bool WalkIn,
    [property: JsonPropertyName("invId")]      string? InvId,
    [property: JsonPropertyName("layer")]      string? Layer);

internal sealed record PlumbInsertBathtubDto(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("widthMm")]    double WidthMm,
    [property: JsonPropertyName("depthMm")]    double DepthMm,
    [property: JsonPropertyName("type")]       string Type,
    [property: JsonPropertyName("invId")]      string? InvId,
    [property: JsonPropertyName("layer")]      string? Layer);

internal sealed record PlumbInsertUrinalDto(
    [property: JsonPropertyName("position")]   Point2dDto Position,
    [property: JsonPropertyName("rotationDeg")] double RotationDeg,
    [property: JsonPropertyName("accessible")] bool Accessible,
    [property: JsonPropertyName("invId")]      string? InvId,
    [property: JsonPropertyName("layer")]      string? Layer);

internal sealed record PlumbPopulateDto(
    [property: JsonPropertyName("roomBoundaryHandle")] string? RoomBoundaryHandle,
    [property: JsonPropertyName("bboxMin")]    Point2dDto? BboxMin,
    [property: JsonPropertyName("bboxMax")]    Point2dDto? BboxMax,
    [property: JsonPropertyName("preset")]     string Preset,
    [property: JsonPropertyName("accessible")] bool Accessible,
    [property: JsonPropertyName("orientation")] string Orientation,
    [property: JsonPropertyName("roomName")]   string? RoomName,
    [property: JsonPropertyName("layer")]      string? Layer);
