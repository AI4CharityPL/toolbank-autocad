// Plugin-side DTOs for acad-schedules primitives.
// Mirrors the Backend DTOs in Categories/Schedules/SchedulesDtos.cs.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using AcadMcp.Shared;

namespace AcadMcp.Plugin.Tools;

internal sealed record EnsureTableStyleArgsDto(
    [property: JsonPropertyName("name")]                 string Name,
    [property: JsonPropertyName("titleTextHeight")]      double TitleTextHeight = 5.0,
    [property: JsonPropertyName("headerTextHeight")]     double HeaderTextHeight = 3.5,
    [property: JsonPropertyName("bodyTextHeight")]       double BodyTextHeight = 2.5,
    [property: JsonPropertyName("textStyle")]            string TextStyle = "Standard",
    [property: JsonPropertyName("titleFillAci")]         int TitleFillAci = 0,
    [property: JsonPropertyName("headerFillAci")]        int HeaderFillAci = 0,
    [property: JsonPropertyName("makeCurrent")]          bool MakeCurrent = false);

internal sealed record EnsureTableStyleResultDto(
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("created")]     bool Created,
    [property: JsonPropertyName("updated")]     bool Updated,
    [property: JsonPropertyName("madeCurrent")] bool MadeCurrent);

internal sealed record ListTableStylesResultDto(
    [property: JsonPropertyName("styles")] IReadOnlyList<string> Styles);

internal sealed record ListRoomLabelsArgsDto(
    [property: JsonPropertyName("labelLayers")]  IReadOnlyList<string>? LabelLayers = null,
    [property: JsonPropertyName("boundaryLayer")] string? BoundaryLayer = null,
    // When true, scan text on EVERY layer (ignore labelLayers) and treat ANY closed polyline as a
    // candidate room boundary (ignore boundaryLayer). Makes room lookup work on non-standard layers.
    [property: JsonPropertyName("allLayers")]    bool AllLayers = false);

internal sealed record RoomLabelDto(
    [property: JsonPropertyName("handle")]  string Handle,
    [property: JsonPropertyName("text")]    string Text,
    [property: JsonPropertyName("layer")]   string Layer,
    [property: JsonPropertyName("position")] Point2dDto Position,
    [property: JsonPropertyName("heightMm")] double HeightMm,
    [property: JsonPropertyName("areaM2")]  double? AreaM2,
    // "dbtext" or "mtext" — lets the Backend pick update_dbtext vs update_mtext when correcting a label.
    [property: JsonPropertyName("kind")]    string? Kind = null,
    // Bounding box (mm) of the closed boundary polyline that contains this label, when found.
    // Lets the Backend assign openings/furniture to a room and report its dimensions.
    [property: JsonPropertyName("boundsMinX")] double? BoundsMinX = null,
    [property: JsonPropertyName("boundsMinY")] double? BoundsMinY = null,
    [property: JsonPropertyName("boundsMaxX")] double? BoundsMaxX = null,
    [property: JsonPropertyName("boundsMaxY")] double? BoundsMaxY = null);

internal sealed record ListRoomLabelsResultDto(
    [property: JsonPropertyName("rooms")] IReadOnlyList<RoomLabelDto> Rooms,
    [property: JsonPropertyName("count")] int Count);

// ─────────── get_room_region ───────────

internal sealed record GetRoomRegionArgsDto(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("cellMm")] double? CellMm = null,
    // Parsed from label text; rejects flood regions &gt; 3× this area (corridor leak).
    [property: JsonPropertyName("labelAreaM2")] double? LabelAreaM2 = null,
    // When true (default), seal every door/window block in the model during flood-fill.
    [property: JsonPropertyName("sealAllDoors")] bool SealAllDoors = true);

internal sealed record OutlinePointDto(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y);

internal sealed record RoomRegionResultDto(
    [property: JsonPropertyName("found")]   bool Found,
    // "flood" (wall-aware flood-fill), "raycast" (axis rays to walls) or "polyline" (smallest closed loop).
    [property: JsonPropertyName("method")]  string Method,
    [property: JsonPropertyName("areaM2")]  double? AreaM2,
    [property: JsonPropertyName("boundsMinX")] double? BoundsMinX,
    [property: JsonPropertyName("boundsMinY")] double? BoundsMinY,
    [property: JsonPropertyName("boundsMaxX")] double? BoundsMaxX,
    [property: JsonPropertyName("boundsMaxY")] double? BoundsMaxY,
    [property: JsonPropertyName("outline")] IReadOnlyList<OutlinePointDto> Outline);

// ─────────── resize_room_boundary ───────────
// 2026-08-12: correct_room_area could rewrite a room label's TEXT but left the boundary
// polyline it was labelling exactly where it was drawn - a wall edit could leave a
// numerically-correct label sitting over a visually wrong (too small/large) outline. This
// primitive finds the boundary polygon by containment (same point-in-polygon test the flood
// fallback already uses) rather than by handle, since correct_room_area only ever has the
// room's label POSITION to hand, not the boundary's own handle.

internal sealed record ResizeRoomBoundaryDto(
    [property: JsonPropertyName("x")] double X,
    [property: JsonPropertyName("y")] double Y,
    [property: JsonPropertyName("vertices")] IReadOnlyList<Point2dDto> Vertices,
    [property: JsonPropertyName("boundaryLayer")] string? BoundaryLayer = null);

internal sealed record ResizeRoomBoundaryResultDto(
    [property: JsonPropertyName("found")]        bool Found,
    [property: JsonPropertyName("oldHandle")]    string? OldHandle,
    [property: JsonPropertyName("newHandle")]    string? NewHandle,
    [property: JsonPropertyName("areaBeforeMm2")] double? AreaBeforeMm2,
    [property: JsonPropertyName("areaAfterMm2")]  double? AreaAfterMm2,
    [property: JsonPropertyName("note")]         string Note);

internal sealed record FindScheduleTablesArgsDto(
    [property: JsonPropertyName("titleContains")] string? TitleContains = null,
    [property: JsonPropertyName("layerFilter")]   string? LayerFilter = null);

internal sealed record ScheduleTableDto(
    [property: JsonPropertyName("handle")]    string Handle,
    [property: JsonPropertyName("title")]     string Title,
    [property: JsonPropertyName("rows")]      int Rows,
    [property: JsonPropertyName("cols")]      int Cols,
    [property: JsonPropertyName("layer")]     string Layer,
    [property: JsonPropertyName("position")]  Point2dDto Position);

internal sealed record FindScheduleTablesResultDto(
    [property: JsonPropertyName("tables")] IReadOnlyList<ScheduleTableDto> Tables,
    [property: JsonPropertyName("count")]  int Count);
