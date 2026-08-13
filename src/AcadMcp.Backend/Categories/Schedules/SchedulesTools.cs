// acad-schedules category. 5 composite tools that assemble AutoCAD Tables from
// existing drawing content, plus an update tool that refreshes previously
// generated schedules in-place:
//   * generate_door_schedule    — "ZESTAWIENIE STOLARKI DRZWIOWEJ"
//   * generate_window_schedule  — "ZESTAWIENIE STOLARKI OKIENNEJ"
//   * generate_room_schedule    — "ZESTAWIENIE POMIESZCZEŃ"
//   * generate_finish_legend    — "LEGENDA WYKOŃCZEŃ"
//   * update_schedules          — find existing schedule tables + rebuild data
//
// All drawing calls go through primitive plugin tools (rule 35 §2):
//   acad.schedules.ensure_table_style      (new)
//   acad.schedules.list_room_labels        (new)
//   acad.schedules.find_schedule_tables    (new)
//   acad.openings.list_openings_in_model   (existing)
//   acad.annotations.add_table             (existing)
//   acad.annotations.set_table_cell        (existing)
//   acad.modify.erase                      (existing — used by update_schedules)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Categories.Architecture;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared;
using AcadMcp.Shared.Geometry;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Schedules;

public static class SchedulesTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 20_000;
    private const int T_SLOW = 60_000;
    private const int T_AUDIT_REGION = 15_000;

    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    // ─────────────────────────────────────────────────────────────
    // generate_door_schedule
    // ─────────────────────────────────────────────────────────────

    [McpTool("generate_door_schedule",
        "Build a door schedule table (ZESTAWIENIE STOLARKI DRZWIOWEJ) from every opening of kind=door currently in model space. Columns: NR, TYP, SZER., WYS., REI, OGNIOOCH., RC, DB, POM. OD, POM. DO. Uses attribute data written by acad-openings (rule 65). Applies the HOSPITAL-DEF or OFFICE-DEF TableStyle preset via ensure_table_style. Table anchored at the given position, layer A-ANNO-TBLS.",
        "schedules",
        Intent = new[]
        {
            "zestawienie stolarki drzwiowej",
            "generate door schedule",
            "tabela drzwi z atrybutami",
            "build doors schedule table",
            "drzwi lista z REI RC",
        },
        RequiresPlugin = true)]
    public static async Task<GenerateDoorScheduleResult> GenerateDoorSchedule(
        IPluginGateway gw, GenerateDoorScheduleArgs args, CancellationToken ct)
    {
        if (args.EnsureStyle) await EnsureTableStyleAsync(gw, args.StyleName, args.TextStyle, ct).ConfigureAwait(false);
        var openings = await FetchOpeningsAsync(gw, "doors", args.LayerFilter, ct).ConfigureAwait(false);

        var notes = new List<string>();
        var data = new List<IReadOnlyList<string>>
        {
            new[] { SchedulesPalette.TitleDoors },
            SchedulesPalette.DoorHeaders,
        };
        if (openings.Count == 0)
        {
            data.Add(new[] { args.EmptyPlaceholder, args.EmptyPlaceholder, args.EmptyPlaceholder, args.EmptyPlaceholder, args.EmptyPlaceholder,
                             args.EmptyPlaceholder, args.EmptyPlaceholder, args.EmptyPlaceholder, args.EmptyPlaceholder, args.EmptyPlaceholder });
            notes.Add("No door openings found in the model. Insert doors with acad-openings.insert_door first.");
        }
        else
        {
            foreach (var d in openings.OrderBy(o => o.Number, StringComparer.OrdinalIgnoreCase))
            {
                data.Add(new[]
                {
                    d.Number.OrDefault(args.EmptyPlaceholder),
                    d.Type.OrDefault(args.EmptyPlaceholder),
                    FormatMm(d.WidthMm, args.EmptyPlaceholder),
                    FormatMm(d.HeightMm, args.EmptyPlaceholder),
                    d.Rei > 0 ? d.Rei.ToString(CultureInfo.InvariantCulture) : args.EmptyPlaceholder,
                    d.FireClass.OrDefault(args.EmptyPlaceholder),
                    d.Rc > 0 ? $"RC{d.Rc}" : args.EmptyPlaceholder,
                    d.AcousticDb > 0 ? $"{d.AcousticDb}" : args.EmptyPlaceholder,
                    d.RoomFrom.OrDefault(args.EmptyPlaceholder),
                    d.RoomTo.OrDefault(args.EmptyPlaceholder),
                });
                if (d.LeadShielded) notes.Add($"Door {d.Number}: lead-shielded (RTG). Verify A-DOOR-LEAD layer.");
                if (d.Rei > 0 && d.Rei < 30) notes.Add($"Door {d.Number}: REI{d.Rei} below evacuation minimum 30 (WT §232).");
            }
        }

        var table = await InsertTableAsync(gw, args.Position, data, SchedulesPalette.DoorCols, SchedulesPalette.DoorScheduleRowHeight,
            args.TextStyle, args.Layer, args.StyleName, ct, args.LayoutName).ConfigureAwait(false);
        return new GenerateDoorScheduleResult(table, openings.Count, notes);
    }

    // ─────────────────────────────────────────────────────────────
    // generate_window_schedule
    // ─────────────────────────────────────────────────────────────

    [McpTool("generate_window_schedule",
        "Build a window schedule table (ZESTAWIENIE STOLARKI OKIENNEJ) from every opening of kind=window currently in model space. Columns: NR, TYP, SZER., WYS., PARAPET, SZYBA, RC, DB, POM. Values come from the acad-openings attribute contract (rule 65). Uses HOSPITAL-DEF/OFFICE-DEF TableStyle. Table anchored at the given position, layer A-ANNO-TBLS.",
        "schedules",
        Intent = new[]
        {
            "zestawienie stolarki okiennej",
            "generate window schedule",
            "tabela okien",
            "build windows schedule table",
            "lista okien z parapetami",
        },
        RequiresPlugin = true)]
    public static async Task<GenerateWindowScheduleResult> GenerateWindowSchedule(
        IPluginGateway gw, GenerateWindowScheduleArgs args, CancellationToken ct)
    {
        if (args.EnsureStyle) await EnsureTableStyleAsync(gw, args.StyleName, args.TextStyle, ct).ConfigureAwait(false);
        var openings = await FetchOpeningsAsync(gw, "windows", args.LayerFilter, ct).ConfigureAwait(false);

        var notes = new List<string>();
        var data = new List<IReadOnlyList<string>>
        {
            new[] { SchedulesPalette.TitleWindows },
            SchedulesPalette.WindowHeaders,
        };
        if (openings.Count == 0)
        {
            data.Add(new[] { args.EmptyPlaceholder, args.EmptyPlaceholder, args.EmptyPlaceholder, args.EmptyPlaceholder, args.EmptyPlaceholder,
                             args.EmptyPlaceholder, args.EmptyPlaceholder, args.EmptyPlaceholder, args.EmptyPlaceholder });
            notes.Add("No window openings found. Insert windows with acad-openings.insert_window first.");
        }
        else
        {
            foreach (var w in openings.OrderBy(o => o.Number, StringComparer.OrdinalIgnoreCase))
            {
                data.Add(new[]
                {
                    w.Number.OrDefault(args.EmptyPlaceholder),
                    w.Type.OrDefault(args.EmptyPlaceholder),
                    FormatMm(w.WidthMm, args.EmptyPlaceholder),
                    FormatMm(w.HeightMm, args.EmptyPlaceholder),
                    FormatMm(w.SillMm, args.EmptyPlaceholder),
                    w.FireClass.OrDefault(args.EmptyPlaceholder),
                    w.Rc > 0 ? $"RC{w.Rc}" : args.EmptyPlaceholder,
                    w.AcousticDb > 0 ? $"{w.AcousticDb}" : args.EmptyPlaceholder,
                    JoinRooms(w.RoomFrom, w.RoomTo, args.EmptyPlaceholder),
                });
            }
        }

        var table = await InsertTableAsync(gw, args.Position, data, SchedulesPalette.WindowCols, SchedulesPalette.WindowScheduleRowHeight,
            args.TextStyle, args.Layer, args.StyleName, ct, args.LayoutName).ConfigureAwait(false);
        return new GenerateWindowScheduleResult(table, openings.Count, notes);
    }

    // ─────────────────────────────────────────────────────────────
    // generate_room_schedule
    // ─────────────────────────────────────────────────────────────

    [McpTool("generate_room_schedule",
        "Build a room schedule table (ZESTAWIENIE POMIESZCZEŃ) from every DBText/MText label on the configured room-label layers (default A-ROOM-IDEN / A-ANNO-ROOM). If boundaryLayer is set, each room's area (m²) is computed from the closed polyline on that layer that contains the label. Auto-numbers rows 101, 102, … when autoNumber=true. cellMm overrides the flood-fill's automatic raster cell size, which scales with the extent of EVERY wall in the model - useful on a large or dense drawing where the default sizing may lose accuracy on an individual room.",
        "schedules",
        Intent = new[]
        {
            "zestawienie pomieszczen",
            "generate room schedule",
            "tabela pomieszczen",
            "list rooms with areas",
            "wykaz pomieszczen z powierzchniami",
        },
        RequiresPlugin = true)]
    public static async Task<GenerateRoomScheduleResult> GenerateRoomSchedule(
        IPluginGateway gw, GenerateRoomScheduleArgs args, CancellationToken ct)
    {
        if (args.EnsureStyle) await EnsureTableStyleAsync(gw, args.StyleName, args.TextStyle, ct).ConfigureAwait(false);
        var rooms = await FetchGroupedRoomsAsync(gw, args.LabelLayers, args.BoundaryLayer, ct,
            cellMm: args.CellMm).ConfigureAwait(false);

        var data = new List<IReadOnlyList<string>>
        {
            new[] { SchedulesPalette.TitleRooms },
            SchedulesPalette.RoomHeaders,
        };
        double totalArea = 0.0;
        if (rooms.Count == 0)
        {
            data.Add(new[] { args.EmptyPlaceholder, args.EmptyPlaceholder, args.EmptyPlaceholder, args.EmptyPlaceholder });
        }
        else
        {
            int autoNum = 101;
            foreach (var r in rooms)
            {
                var number = args.AutoNumber ? autoNum.ToString(CultureInfo.InvariantCulture) : args.EmptyPlaceholder;
                autoNum++;
                var area = r.MeasuredAreaM2 ?? r.LabelAreaM2;
                var areaStr = area.HasValue
                    ? area.Value.ToString("F1", CultureInfo.InvariantCulture)
                    : args.EmptyPlaceholder;
                if (area.HasValue) totalArea += area.Value;
                data.Add(new[]
                {
                    number,
                    r.Name,
                    areaStr,
                    "",
                });
            }
        }

        var table = await InsertTableAsync(gw, args.Position, data, SchedulesPalette.RoomCols, SchedulesPalette.RoomScheduleRowHeight,
            args.TextStyle, args.Layer, args.StyleName, ct, args.LayoutName).ConfigureAwait(false);
        return new GenerateRoomScheduleResult(table, rooms.Count, totalArea);
    }

    // ─────────────────────────────────────────────────────────────
    // get_room_data  (read-only)
    // ─────────────────────────────────────────────────────────────

    [McpTool("get_room_data",
        "READ-ONLY, universal. Locate ONE space (room, office, apartment room, classroom, yard/garden, hall, …) by " +
        "its number or name (substring, case-insensitive) and return a full dossier: number + name, MEASURED area " +
        "(m², computed from a wall-aware flood-fill of the actual boundary) plus the labelled area (labelAreaM2, " +
        "parsed from the label text) when present, bounding-box dimensions (width × depth in mm), the boundary " +
        "detection method, and every door, window and furniture/equipment item that lies inside the room (furniture " +
        "tested against the traced outline, openings against the perimeter). Scans labels on ALL layers by default " +
        "(not just A-ROOM-*). Use this to gather accurate data BEFORE drawing a schedule or generating a " +
        "visualization. Does NOT modify the drawing. cellMm overrides the flood-fill's automatic raster cell " +
        "size - see audit_all_rooms's cellMm for why a large or dense model may need a smaller explicit value.",
        "schedules",
        Intent = new[]
        {
            "dane pomieszczenia",
            "get room data",
            "znajdz sale po numerze",
            "find room by number A-304",
            "wymiary okna drzwi meble pomieszczenia",
            "room dimensions doors windows furniture",
        },
        RequiresPlugin = true)]
    public static async Task<GetRoomDataResult> GetRoomData(
        IPluginGateway gw, GetRoomDataArgs args, CancellationToken ct)
    {
        var query = (args.Query ?? string.Empty).Trim();
        if (query.Length == 0)
            return new GetRoomDataResult(false, null, null, Array.Empty<string>(), null, null, null,
                Array.Empty<RoomOpeningDto>(), Array.Empty<RoomOpeningDto>(), Array.Empty<RoomFurnitureDto>(),
                "Brak zapytania (query). Podaj numer lub nazwę sali, np. 'A-304'.");

        var rooms = await FetchRoomsAsync(gw, args.LabelLayers, args.BoundaryLayer, ct, args.AllLayers).ConfigureAwait(false);
        if (rooms.Count == 0)
            return new GetRoomDataResult(false, null, null, Array.Empty<string>(), null, null, null,
                Array.Empty<RoomOpeningDto>(), Array.Empty<RoomOpeningDto>(), Array.Empty<RoomFurnitureDto>(),
                "Nie znaleziono żadnych etykiet tekstowych w modelu.");

        // Find the best matching label. Prefer an exact-ish room-number hit, then any substring match.
        var match = rooms.FirstOrDefault(r =>
                        string.Equals(StripMtextCodes(r.Text).Trim(), query, StringComparison.OrdinalIgnoreCase))
                    ?? rooms.FirstOrDefault(r =>
                        StripMtextCodes(r.Text).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);

        if (match is null)
            return new GetRoomDataResult(false, null, null, Array.Empty<string>(), null, null, null,
                Array.Empty<RoomOpeningDto>(), Array.Empty<RoomOpeningDto>(), Array.Empty<RoomFurnitureDto>(),
                $"Nie znaleziono sali pasującej do '{query}'. Dostępne etykiety: " +
                string.Join(", ", rooms.Take(20).Select(r => StripMtextCodes(r.Text).Trim()).Where(t => t.Length > 0)));

        // Detect region first (uses label area to reject corridor leaks during flood-fill).
        double? matchLabelArea = ParseAreaM2(match.Text);
        var region = await FetchRoomRegionAsync(gw, match.Position.X, match.Position.Y, ct, matchLabelArea,
            cellMm: args.CellMm).ConfigureAwait(false);
        var bounds = region?.Bounds ?? match.Bounds;
        var outline = region?.Outline ?? Array.Empty<PointXY>();
        bool usePolygon = outline.Count >= 3;

        // Group labels whose insertion point lies inside the detected outline (not identical bbox).
        var siblings = usePolygon
            ? rooms.Where(r => RoomRegionSolver.PointInPolygon(outline, r.Position.X, r.Position.Y)).ToList()
            : new List<RoomRow> { match };
        if (siblings.Count == 0) siblings = new List<RoomRow> { match };

        var labels = siblings.Select(s => StripMtextCodes(s.Text).Trim())
                             .Where(t => t.Length > 0)
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .ToList();

        // Measured area comes from the detected region; the labelled area (e.g. "200 m²") is reported
        // separately so the agent can show both and never silently assumes the stated figure.
        double? labelAreaM2 = labels.Select(ParseAreaM2).FirstOrDefault(a => a.HasValue);
        double? measuredAreaM2 = region is { Found: true } ? region.AreaM2 : null;
        var areaM2 = measuredAreaM2
                     ?? labelAreaM2
                     ?? siblings.Select(s => s.AreaM2).FirstOrDefault(a => a.HasValue)
                     ?? (bounds is { } bb ? (bb.MaxX - bb.MinX) * (bb.MaxY - bb.MinY) / 1_000_000.0 : (double?)null);
        double? widthMm = bounds is { } w ? w.MaxX - w.MinX : null;
        double? depthMm = bounds is { } d ? d.MaxY - d.MinY : null;

        var (number, name) = SplitNumberName(labels, query);

        var doors = new List<RoomOpeningDto>();
        var windows = new List<RoomOpeningDto>();
        var furniture = new List<RoomFurnitureDto>();
        string? note = null;
        string method = region?.Method ?? "none";

        if (bounds is { } box)
        {
            foreach (var o in await FetchOpeningRefsAsync(gw, ct).ConfigureAwait(false))
            {
                bool onPerimeter = usePolygon
                    ? RoomRegionSolver.InsideOrNearBoundary(outline, o.Position.X, o.Position.Y, args.MarginMm)
                    : Inside(new RoomBounds(box.MinX - args.MarginMm, box.MinY - args.MarginMm,
                        box.MaxX + args.MarginMm, box.MaxY + args.MarginMm), o.Position);
                if (!onPerimeter) continue;
                var dto = new RoomOpeningDto(o.Handle, o.Kind, o.Number, o.BlockName, o.WidthMm, o.HeightMm,
                    WallOf(box, o.Position), o.Position);
                if (o.Kind == "window") windows.Add(dto); else doors.Add(dto);
            }
            // Furniture is strictly inside the room: test against the traced outline when available.
            foreach (var f in await FetchFurnitureRefsAsync(gw, ct).ConfigureAwait(false))
            {
                bool inside = usePolygon
                    ? RoomRegionSolver.PointInPolygon(outline, f.Position.X, f.Position.Y)
                    : Inside(box, f.Position);
                if (!inside) continue;
                furniture.Add(new RoomFurnitureDto(f.Handle, f.BlockName, f.Type, f.Layer, f.RotationDeg, f.Position));
            }

            if (region is { Found: false })
                note = "Obszar pomieszczenia wygląda na otwarty/nieszczelny (brak zamknięcia ścian lub brak bloków " +
                       "drzwi/okien do uszczelnienia) — użyto przybliżonego prostokąta z promieni do ścian. " +
                       "Powierzchnia i lista elementów mogą być przybliżone.";
        }
        else
        {
            note = "Nie udało się wyznaczyć obrysu pomieszczenia (brak ścian/obrysu wokół etykiety). " +
                   "Zwrócono tylko etykiety i (jeśli dostępna) powierzchnię z tekstu.";
        }

        return new GetRoomDataResult(true, number, name, labels, areaM2, widthMm, depthMm,
            doors, windows, furniture, note, labelAreaM2, method);
    }

    // ─────────────────────────────────────────────────────────────
    // correct_room_area  (write — self-correcting label)
    // ─────────────────────────────────────────────────────────────

    [McpTool("correct_room_area",
        "Verify and, if needed, CORRECT a space's stated area on its label. Measures the REAL area with the " +
        "wall-aware boundary detector (get_room_region) and compares it to the area written on the label " +
        "(e.g. '200 m²'). When they differ by more than tolerancePct (or when explicitAreaM2 is supplied), it " +
        "rewrites the 'N m²' token on the label text in place (reusing update_dbtext/update_mtext). Use apply=false " +
        "for a dry-run that only reports the discrepancy. Works on ALL layers and is space-agnostic (room, office, " +
        "garden, …). Modifies the drawing only when a correction is applied. syncBoundary=true (default false, " +
        "opt-in) ALSO replaces the room's boundary polygon (the closed polyline on boundaryLayer / A-ROOM-BNDY) " +
        "with the measured outline when the label is corrected — otherwise only the text changes and the drawn " +
        "outline is left exactly where it was, which after a wall edit means a numerically-correct label can sit " +
        "over a visually wrong (stale-shaped) outline. Confirmed live: define_room's boundary is a separate, " +
        "non-parametric entity that does not move when walls do. cellMm overrides the flood-fill's automatic " +
        "raster cell size (see audit_all_rooms) - pass a small explicit value on a large or dense model if " +
        "measured/label deltas seem larger than the room's own geometry would explain.",
        "schedules",
        Intent = new[]
        {
            "popraw powierzchnie sali",
            "correct room area label",
            "zaktualizuj metraz pomieszczenia na rysunku",
            "fix wrong area 200 m2 on label",
            "sprawdz i popraw powierzchnie pomieszczenia",
            "recalculate and update room area",
        },
        RequiresPlugin = true)]
    public static async Task<CorrectRoomAreaResult> CorrectRoomArea(
        IPluginGateway gw, CorrectRoomAreaArgs args, CancellationToken ct)
    {
        var query = (args.Query ?? string.Empty).Trim();
        if (query.Length == 0)
            return new CorrectRoomAreaResult(false, null, null, null, null, null, null, null, false,
                "Brak zapytania (query). Podaj numer lub nazwę sali, np. 'A-304'.");

        var rooms = await FetchRoomsAsync(gw, args.LabelLayers, args.BoundaryLayer, ct, args.AllLayers).ConfigureAwait(false);
        var match = rooms.FirstOrDefault(r =>
                        string.Equals(StripMtextCodes(r.Text).Trim(), query, StringComparison.OrdinalIgnoreCase))
                    ?? rooms.FirstOrDefault(r =>
                        StripMtextCodes(r.Text).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        if (match is null)
            return new CorrectRoomAreaResult(false, null, null, null, null, null, null, null, false,
                $"Nie znaleziono sali pasującej do '{query}'.");

        var regionPreview = await FetchRoomRegionAsync(gw, match.Position.X, match.Position.Y, ct, ParseAreaM2(match.Text),
            cellMm: args.CellMm).ConfigureAwait(false);
        var outlinePreview = regionPreview?.Outline ?? Array.Empty<PointXY>();
        bool usePolygon = outlinePreview.Count >= 3;

        // The label to edit is the one carrying an area token (prefer the match, then a sibling in outline).
        var siblings = usePolygon
            ? rooms.Where(r => RoomRegionSolver.PointInPolygon(outlinePreview, r.Position.X, r.Position.Y)).ToList()
            : match.Bounds is { } mb
                ? rooms.Where(r => r.Bounds is { } b && BoundsOverlap(b, mb)).ToList()
                : new List<RoomRow> { match };
        var editTarget = (ParseAreaM2(match.Text).HasValue ? match : null)
                         ?? siblings.FirstOrDefault(s => ParseAreaM2(s.Text).HasValue)
                         ?? match;

        var region = regionPreview ?? await FetchRoomRegionAsync(gw, match.Position.X, match.Position.Y, ct,
            ParseAreaM2(editTarget.Text), cellMm: args.CellMm).ConfigureAwait(false);
        double? measured = region is { Found: true } ? region.AreaM2 : null;
        double? labelArea = ParseAreaM2(editTarget.Text);
        double? target = args.ExplicitAreaM2 ?? measured;

        if (target is null)
            return new CorrectRoomAreaResult(true, editTarget.Handle, editTarget.Text, editTarget.Text,
                labelArea, measured, null, region?.Method, false,
                "Nie udało się zmierzyć powierzchni (obszar otwarty/brak ścian) i nie podano explicitAreaM2 — " +
                "nie zmieniono etykiety.");

        bool shouldChange = args.ExplicitAreaM2 is not null
                            || labelArea is null
                            || Math.Abs(measured!.Value - labelArea.Value) / Math.Max(0.001, labelArea.Value) * 100.0 > args.TolerancePct;

        string newText = ReplaceAreaToken(editTarget.Text, target.Value, args.Decimals);
        bool changed = false;
        string? note = null;

        if (!shouldChange)
        {
            note = $"Etykieta ({labelArea:F1} m²) zgadza się ze zmierzoną powierzchnią ({measured:F1} m²) " +
                   $"w tolerancji {args.TolerancePct:F1}% — bez zmian.";
        }
        else if (!args.Apply)
        {
            note = $"Tryb podglądu (apply=false): proponowana zmiana '{StripMtextCodes(editTarget.Text)}' → " +
                   $"'{StripMtextCodes(newText)}'. Nie zapisano.";
        }
        else if (string.Equals(newText, editTarget.Text, StringComparison.Ordinal))
        {
            note = "Tekst etykiety już zawiera docelową wartość — bez zmian.";
        }
        else
        {
            changed = await UpdateLabelTextAsync(gw, editTarget, newText, ct).ConfigureAwait(false);
            note = changed
                ? $"Poprawiono powierzchnię na etykiecie na {FormatArea(target.Value, args.Decimals)} m²."
                : "Nie udało się zaktualizować etykiety (nieobsługiwany typ encji).";
        }

        bool boundaryResynced = false;
        string? boundaryOldHandle = null, boundaryNewHandle = null, boundaryNote = null;
        if (changed && args.SyncBoundary)
        {
            var outline = region?.Outline;
            if (outline is null || outline.Count < 3)
            {
                boundaryNote = "syncBoundary requested but no measured outline (3+ points) was " +
                               "available - boundary polygon left untouched.";
            }
            else
            {
                var vertices = outline.Select(p => new Point2dDto(p.X, p.Y)).ToList();
                var resizeArgs = new JsonObject
                {
                    ["x"] = editTarget.Position.X,
                    ["y"] = editTarget.Position.Y,
                    ["vertices"] = JsonSerializer.SerializeToNode(vertices, Opts),
                };
                if (!string.IsNullOrWhiteSpace(args.BoundaryLayer)) resizeArgs["boundaryLayer"] = args.BoundaryLayer;

                var resp = await gw.InvokeAsync("acad.schedules.resize_room_boundary", resizeArgs, T_NORMAL, ct)
                    .ConfigureAwait(false);
                boundaryResynced = resp?["found"]?.GetValue<bool>() ?? false;
                boundaryOldHandle = resp?["oldHandle"]?.GetValue<string>();
                boundaryNewHandle = resp?["newHandle"]?.GetValue<string>();
                boundaryNote = resp?["note"]?.GetValue<string>();
            }
        }

        return new CorrectRoomAreaResult(true, editTarget.Handle, editTarget.Text,
            changed ? newText : editTarget.Text, labelArea, measured, target, region?.Method, changed, note,
            boundaryResynced, boundaryOldHandle, boundaryNewHandle, boundaryNote);
    }

    /// <summary>Update a room label's text via the matching annotation primitive (dbtext/mtext).</summary>
    private static async Task<bool> UpdateLabelTextAsync(IPluginGateway gw, RoomRow target, string newText, CancellationToken ct)
    {
        var uargs = new JsonObject { ["handle"] = target.Handle, ["contents"] = newText };
        string primary = target.Kind == "dbtext" ? "acad.annotations.update_dbtext" : "acad.annotations.update_mtext";
        string secondary = target.Kind == "dbtext" ? "acad.annotations.update_mtext" : "acad.annotations.update_dbtext";
        try
        {
            await gw.InvokeAsync(primary, uargs, T_NORMAL, ct).ConfigureAwait(false);
            return true;
        }
        catch
        {
            try
            {
                await gw.InvokeAsync(secondary, new JsonObject { ["handle"] = target.Handle, ["contents"] = newText }, T_NORMAL, ct).ConfigureAwait(false);
                return true;
            }
            catch { return false; }
        }
    }

    private static string FormatArea(double areaM2, int decimals)
        => areaM2.ToString("F" + Math.Max(0, decimals), CultureInfo.InvariantCulture);

    /// <summary>Replace the first "N m²" token in a label with the corrected value, or append one if absent.</summary>
    private static string ReplaceAreaToken(string text, double areaM2, int decimals)
    {
        string formatted = FormatArea(areaM2, decimals) + " m²";
        var rx = new System.Text.RegularExpressions.Regex(
            @"\d+(?:[.,]\d+)?\s*m(?:²|2|\^2)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (rx.IsMatch(text)) return rx.Replace(text, formatted, 1);
        string sep = text.Contains("\r\n") ? "\r\n" : (text.Contains("\n") ? "\n" : " ");
        return text.Length == 0 ? formatted : text + sep + formatted;
    }

    // ─────────────────────────────────────────────────────────────
    // audit_all_rooms  (read-only batch)
    // ─────────────────────────────────────────────────────────────

    [McpTool("audit_all_rooms",
        "READ-ONLY batch audit of every room label in the drawing. For each label, measures the REAL area " +
        "(get_room_region), compares to the stated m² on the label, counts doors/windows/furniture in the " +
        "detected region, and flags leaks, label mismatches, missing doors and furniture type conflicts. " +
        "Optional exportCsvPath (use 'auto' for %LOCALAPPDATA%\\AcadMcp\\reports\\). Does NOT modify the drawing. " +
        "cellMm overrides the flood-fill's automatic raster cell size, which scales with the extent of EVERY " +
        "wall in the model, not the room being measured - confirmed live on a 75-room, 5-floor drawing of " +
        "otherwise-identical rooms: automatic sizing measured the SAME room shape differently depending on " +
        "which floor it sat on, enough to false-flag 60 of 75 as labelMismatch. Pass a small explicit value " +
        "(e.g. 50-100mm) for tighter accuracy on a large or dense model.",
        "schedules",
        Intent = new[]
        {
            "audyt wszystkich pomieszczen",
            "audit all rooms",
            "sprawdz metraz wszystkich sal",
            "batch room area audit",
            "raport pomieszczen powierzchnia meble",
            "hospital room audit report",
        },
        RequiresPlugin = true)]
    public static async Task<AuditAllRoomsResult> AuditAllRooms(
        IPluginGateway gw, AuditAllRoomsArgs args, CancellationToken ct)
    {
        var candidates = await FetchGroupedRoomsAsync(gw, args.LabelLayers, null, ct, args.AllLayers,
            args.CellMm).ConfigureAwait(false);
        var openings = await FetchOpeningRefsAsync(gw, ct).ConfigureAwait(false);
        var furniture = await FetchFurnitureRefsAsync(gw, ct).ConfigureAwait(false);

        var rows = new List<RoomAuditRowDto>();
        int mismatches = 0, leaks = 0, emptyDoors = 0, furnitureIssues = 0;

        foreach (var room in candidates)
        {
            ct.ThrowIfCancellationRequested();
            var labelArea = room.LabelAreaM2;
            var outline = room.Outline;
            bool usePoly = outline.Count >= 3;
            var bounds = room.Bounds;
            double? measured = room.MeasuredAreaM2;
            double? delta = labelArea is { } la && la > 0 && measured is { } m
                ? Math.Abs(m - la) / la * 100.0 : null;

            int doors = 0, windows = 0, furn = 0;
            if (bounds is { } box)
            {
                foreach (var o in openings)
                {
                    bool hit = usePoly
                        ? RoomRegionSolver.InsideOrNearBoundary(outline, o.Position.X, o.Position.Y, args.MarginMm)
                        : Inside(box, o.Position);
                    if (!hit) continue;
                    if (o.Kind == "window") windows++; else doors++;
                }
                foreach (var f in furniture)
                {
                    bool inside = usePoly
                        ? RoomRegionSolver.PointInPolygon(outline, f.Position.X, f.Position.Y)
                        : Inside(box, f.Position);
                    if (inside) furn++;
                }
            }

            var flags = new List<string>();
            string method = room.RegionMethod;
            if (measured is { } mm && labelArea is { } ll && ll > 0 && mm > ll * 1.5) { flags.Add("leakSuspected"); leaks++; }
            else if (method is not "flood" || !usePoly) { flags.Add("leakSuspected"); leaks++; }
            if (delta is { } d && d > args.TolerancePct) { flags.Add("labelMismatch"); mismatches++; }
            if (doors == 0 && !IsCorridorLike(room.Name)) { flags.Add("emptyOpenings"); emptyDoors++; }
            if (DetectFurnitureMismatch(room.Name, furniture, outline, usePoly, bounds)) { flags.Add("furnitureMismatch"); furnitureIssues++; }

            string query = room.Number ?? room.Name;
            rows.Add(new RoomAuditRowDto(room.RepresentativeHandle, query, room.Layer, labelArea, measured, delta,
                method, doors, windows, furn, flags));
        }

        rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Query, b.Query));

        string? exportPath = null;
        if (!string.IsNullOrWhiteSpace(args.ExportCsvPath))
        {
            exportPath = args.ExportCsvPath.Equals("auto", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AcadMcp", "reports", $"room-audit-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv")
                : args.ExportCsvPath!;
            var dir = Path.GetDirectoryName(exportPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(exportPath, BuildAuditCsv(rows), Encoding.UTF8, ct).ConfigureAwait(false);
        }

        return new AuditAllRoomsResult(candidates.Count, mismatches, leaks, emptyDoors, furnitureIssues, rows,
            exportPath, $"Przeskanowano {candidates.Count} pomieszczeń.");
    }

    // ─────────────────────────────────────────────────────────────
    // correct_all_room_areas  (batch write)
    // ─────────────────────────────────────────────────────────────

    [McpTool("correct_all_room_areas",
        "Batch wrapper around correct_room_area: scans every room label and rewrites the m² token when the " +
        "measured area diverges from the label by more than tolerancePct. Use apply=false for dry-run. " +
        "Modifies the drawing only when apply=true. syncBoundary and cellMm are forwarded to each call - see " +
        "correct_room_area's own syncBoundary/cellMm for what they do.",
        "schedules",
        Intent = new[]
        {
            "popraw wszystkie powierzchnie sal",
            "correct all room areas",
            "batch update room labels m2",
            "masowa korekta metrazu",
            "napraw etykiety powierzchni",
            "fix all room area labels",
        },
        RequiresPlugin = true)]
    public static async Task<CorrectAllRoomAreasResult> CorrectAllRoomAreas(
        IPluginGateway gw, CorrectAllRoomAreasArgs args, CancellationToken ct)
    {
        var audit = await AuditAllRooms(gw,
            new AuditAllRoomsArgs(args.AllLayers, args.LabelLayers, args.TolerancePct, CellMm: args.CellMm), ct)
            .ConfigureAwait(false);
        var entries = new List<CorrectAllRoomAreasEntry>();
        int changed = 0, skipped = 0;

        foreach (var row in audit.Rows)
        {
            if (args.OnlyMismatches && !row.Flags.Contains("labelMismatch")) { skipped++; continue; }
            if (string.IsNullOrWhiteSpace(row.Query)) { skipped++; continue; }

            var result = await CorrectRoomArea(gw,
                new CorrectRoomAreaArgs(row.Query, null, args.TolerancePct, args.Apply, args.Decimals,
                    args.AllLayers, args.LabelLayers, null, args.SyncBoundary, args.CellMm), ct).ConfigureAwait(false);
            entries.Add(new CorrectAllRoomAreasEntry(row.Query, result.Changed, result.LabelAreaM2,
                result.MeasuredAreaM2, result.Note));
            if (result.Changed) changed++;
            else skipped++;
        }

        return new CorrectAllRoomAreasResult(audit.Rows.Count, changed, skipped, entries,
            args.Apply
                ? $"Zastosowano {changed} korekt etykiet powierzchni."
                : $"Tryb podglądu: {changed} sal wymagałoby korekty (apply=false).");
    }

    private static bool IsAuditCandidate(RoomRow r)
    {
        var layer = r.Layer ?? "";
        if (layer.IndexOf("GRID", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (layer.IndexOf("SBAR", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (layer.IndexOf("SYMB", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (layer.IndexOf("TTLB", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (layer.IndexOf("NOTE", StringComparison.OrdinalIgnoreCase) >= 0) return false;
        if (layer.IndexOf("DIMS", StringComparison.OrdinalIgnoreCase) >= 0) return false;

        var t = StripMtextCodes(r.Text).Trim();
        if (t.Length == 0) return false;
        if (t.Length <= 2 && t.All(char.IsDigit)) return false;

        if (layer.IndexOf("AREA", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (layer.IndexOf("ROOM", StringComparison.OrdinalIgnoreCase) >= 0) return true;

        // allLayers: require a room id token plus a name or stated area.
        var firstLine = t.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? t;
        if (!System.Text.RegularExpressions.Regex.IsMatch(firstLine, @"^[A-Za-z]?-?\d{2,4}[A-Za-z]?$")) return false;
        return t.Contains("m²", StringComparison.OrdinalIgnoreCase) ||
               t.Contains("m2", StringComparison.OrdinalIgnoreCase) ||
               t.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length >= 2;
    }

    // MEASURED 2026-08-12: define_room places THREE separate DBText entities per room (number,
    // name, area — all on A-ROOM-IDEN), and FetchRoomsAsync returns one RoomRow per text entity.
    // GenerateRoomSchedule and AuditAllRooms used to iterate that raw list directly, treating
    // each of the three lines as its own "room" — a drawing with 6 real rooms reported 18 rows
    // and summed the (correctly-measured) area of every room three times over (144.625 m² real,
    // 433.875 m² reported — exactly 3x). get_room_data/correct_room_area never had this bug:
    // they already group every label whose position falls inside the SAME detected boundary
    // polygon before extracting number/name/area. This is that grouping generalised to the
    // WHOLE drawing in one pass, so the batch tools use the identical, already-proven algorithm
    // instead of a second, buggy one. As a side effect it also cuts get_room_region round-trips
    // from one-per-label to one-per-room (3x fewer for a 3-line tag).
    private sealed record GroupedRoom(
        string? Number, string Name, string RepresentativeHandle, string Layer, Point2dDto Position,
        double? MeasuredAreaM2, double? LabelAreaM2, RoomBounds? Bounds, IReadOnlyList<PointXY> Outline,
        string RegionMethod, bool RegionFound);

    private static async Task<List<GroupedRoom>> FetchGroupedRoomsAsync(
        IPluginGateway gw, IReadOnlyList<string>? labelLayers, string? boundaryLayer, CancellationToken ct,
        bool allLayers = false, double? cellMm = null)
    {
        var raw = await FetchRoomsAsync(gw, labelLayers, boundaryLayer, ct, allLayers).ConfigureAwait(false);
        var candidates = raw.Where(IsAuditCandidate).ToList();
        var assigned = new bool[candidates.Count];
        var groups = new List<GroupedRoom>();

        for (int i = 0; i < candidates.Count; i++)
        {
            if (assigned[i]) continue;
            var seed = candidates[i];
            assigned[i] = true;

            var region = await FetchRoomRegionAsync(gw, seed.Position.X, seed.Position.Y, ct,
                ParseAreaM2(seed.Text), sealAllDoors: false, timeoutMs: T_AUDIT_REGION, cellMm: cellMm).ConfigureAwait(false);
            var outline = region?.Outline ?? Array.Empty<PointXY>();
            bool usePoly = outline.Count >= 3;

            var siblingIdx = new List<int> { i };
            if (usePoly)
            {
                for (int j = 0; j < candidates.Count; j++)
                {
                    if (assigned[j]) continue;
                    if (RoomRegionSolver.PointInPolygon(outline, candidates[j].Position.X, candidates[j].Position.Y))
                    {
                        siblingIdx.Add(j);
                        assigned[j] = true;
                    }
                }
            }

            var labels = siblingIdx.Select(k => StripMtextCodes(candidates[k].Text).Trim())
                                    .Where(t => t.Length > 0)
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .ToList();
            var query = ExtractRoomQuery(seed.Text);
            var (number, name) = SplitNumberName(labels, query);
            double? labelAreaM2 = labels.Select(ParseAreaM2).FirstOrDefault(a => a.HasValue);
            double? measured = region is { Found: true } ? region.AreaM2 : null;
            var bounds = region?.Bounds ?? seed.Bounds;

            groups.Add(new GroupedRoom(number, name ?? query, seed.Handle, seed.Layer, seed.Position,
                measured, labelAreaM2, bounds, outline, region?.Method ?? "none", region?.Found ?? false));
        }

        groups.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Number ?? a.Name, b.Number ?? b.Name));
        return groups;
    }

    private static bool IsCorridorLike(string text)
    {
        var u = StripMtextCodes(text).ToUpperInvariant();
        return u.Contains("KORYTARZ") || u.Contains("CORRIDOR") || u.Contains("HOL") ||
               u.Contains("WEJŚCIE") || u.Contains("LOBBY") || u.Contains("STREFA");
    }

    private static string ExtractRoomQuery(string text)
    {
        var lines = StripMtextCodes(text).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var t = line.Trim();
            if (System.Text.RegularExpressions.Regex.IsMatch(t, @"^[A-Za-z]?-?\d{2,4}[A-Za-z]?$")) return t;
        }
        return lines.Length > 0 ? lines[0].Trim() : text.Trim();
    }

    private static bool DetectFurnitureMismatch(string labelText, IReadOnlyList<FurnitureRef> all,
        IReadOnlyList<PointXY> outline, bool usePoly, RoomBounds? bounds)
    {
        var u = StripMtextCodes(labelText).ToUpperInvariant();
        bool officeLike = u.Contains("KONFERENC") || u.Contains("BIURO") || u.Contains("OFFICE") ||
                          u.Contains("SALA") && !u.Contains("SOR") && !u.Contains("CHIR");
        bool clinicalLike = u.Contains("SOR") || u.Contains("ICU") || u.Contains("POKÓJ") ||
                            u.Contains("GABINET") || u.Contains("LAB");
        foreach (var f in all)
        {
            bool inside = usePoly
                ? RoomRegionSolver.PointInPolygon(outline, f.Position.X, f.Position.Y)
                : bounds is { } b && Inside(b, f.Position);
            if (!inside) continue;
            var bn = (f.BlockName ?? "").ToUpperInvariant();
            if (officeLike && (bn.Contains("BED-ICU") || bn.Contains("BED-HOSP") || bn.Contains("HEADWALL")))
                return true;
            if (officeLike && f.Type is "icu" or "medical") return true;
            if (clinicalLike && bn.Contains("FURN-DESK-OFF") && u.Contains("SOR")) return true;
        }
        return false;
    }

    private static string BuildAuditCsv(IReadOnlyList<RoomAuditRowDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("query,layer,labelM2,measuredM2,deltaPct,method,doors,windows,furniture,flags");
        foreach (var r in rows)
        {
            sb.Append(Csv(r.Query)).Append(',')
              .Append(Csv(r.Layer)).Append(',')
              .Append(r.LabelAreaM2?.ToString(CultureInfo.InvariantCulture) ?? "").Append(',')
              .Append(r.MeasuredAreaM2?.ToString(CultureInfo.InvariantCulture) ?? "").Append(',')
              .Append(r.DeltaPct?.ToString("F1", CultureInfo.InvariantCulture) ?? "").Append(',')
              .Append(Csv(r.Method)).Append(',')
              .Append(r.DoorCount).Append(',').Append(r.WindowCount).Append(',').Append(r.FurnitureCount).Append(',')
              .Append(Csv(string.Join("|", r.Flags))).AppendLine();
        }
        return sb.ToString();
    }

    private static string Csv(string s)
    {
        if (s.Contains('"') || s.Contains(',') || s.Contains('\n')) return $"\"{s.Replace("\"", "\"\"")}\"";
        return s;
    }

    // ─────────────────────────────────────────────────────────────
    // generate_finish_legend
    // ─────────────────────────────────────────────────────────────

    [McpTool("generate_finish_legend",
        "Emit a finish legend (LEGENDA WYKOŃCZEŃ) mapping finish codes (F-xx floor, W-xx walls, C-xx ceiling) to descriptions, RAL colors and locations. The default rows cover typical hospital PVC/epoxy/HPL finishes; pass extraRows to append project-specific codes. Layer A-ANNO-LEGN by default.",
        "schedules",
        Intent = new[]
        {
            "legenda wykonczen",
            "generate finish legend",
            "lista wykonczen podlogi sciany",
            "finish code table",
            "legenda materialow",
        },
        RequiresPlugin = true)]
    public static async Task<GenerateFinishLegendResult> GenerateFinishLegend(
        IPluginGateway gw, GenerateFinishLegendArgs args, CancellationToken ct)
    {
        if (args.EnsureStyle) await EnsureTableStyleAsync(gw, args.StyleName, args.TextStyle, ct).ConfigureAwait(false);

        var rows = new List<IReadOnlyList<string>>
        {
            new[] { SchedulesPalette.TitleFinish },
            SchedulesPalette.FinishHeaders,
        };
        rows.AddRange(SchedulesPalette.DefaultFinishRows);
        if (args.ExtraRows is { Count: > 0 }) rows.AddRange(args.ExtraRows);

        var table = await InsertTableAsync(gw, args.Position, rows, SchedulesPalette.FinishCols, SchedulesPalette.FinishLegendRowHeight,
            args.TextStyle, args.Layer, args.StyleName, ct, args.LayoutName).ConfigureAwait(false);
        // Subtract the title + header rows from rowCount so callers get the real entry count.
        int dataRowCount = rows.Count - 2;
        return new GenerateFinishLegendResult(table, dataRowCount);
    }

    // ─────────────────────────────────────────────────────────────
    // update_schedules
    // ─────────────────────────────────────────────────────────────

    [McpTool("update_schedules",
        "Find existing schedule tables by their title cell (ZESTAWIENIE STOLARKI / POMIESZCZEŃ / LEGENDA WYKOŃCZEŃ) and rebuild each one from current drawing content. Old tables are erased and replaced at the same insertion point. Useful after inserting/removing openings or rooms; one call keeps every schedule in sync.",
        "schedules",
        Intent = new[]
        {
            "zaktualizuj zestawienia",
            "update schedules",
            "refresh door window schedules",
            "odswiez tabele stolarki",
            "regenerate schedules after changes",
        },
        RequiresPlugin = true)]
    public static async Task<UpdateSchedulesResult> UpdateSchedules(
        IPluginGateway gw, UpdateSchedulesArgs args, CancellationToken ct)
    {
        var findArgs = new JsonObject { ["titleContains"] = args.TitleContains, ["layerFilter"] = args.LayerFilter };
        var findResp = await gw.InvokeAsync("acad.schedules.find_schedule_tables", findArgs, T_NORMAL, ct).ConfigureAwait(false)
                       ?? throw new InvalidPluginShapeException("find_schedule_tables returned null");
        var tablesArr = findResp["tables"] as JsonArray ?? new JsonArray();

        var updated = new List<UpdateScheduleEntry>();
        var skipped = new List<string>();

        foreach (var node in tablesArr)
        {
            if (node is null) continue;
            var oldHandle = node["handle"]?.GetValue<string>() ?? "";
            var title = node["title"]?.GetValue<string>() ?? "";
            var rowsBefore = node["rows"]?.GetValue<int>() ?? 0;
            var posNode = node["position"];
            if (posNode is null || string.IsNullOrWhiteSpace(oldHandle))
            {
                skipped.Add($"{oldHandle}: missing position or handle"); continue;
            }
            var pos = posNode.Deserialize<Point2dDto>(Opts) ?? throw new InvalidPluginShapeException("position missing x/y");

            string kind = ClassifySchedule(title);
            if (kind == "unknown") { skipped.Add($"{oldHandle}: unknown title '{title}'"); continue; }

            ScheduleGenerationSummary newSummary;
            switch (kind)
            {
                case "doors":
                    var d = await GenerateDoorSchedule(gw, new GenerateDoorScheduleArgs(pos, args.StyleName, SchedulesPalette.LayerTables, args.TextStyle, null, false, "—"), ct).ConfigureAwait(false);
                    newSummary = d.Summary; break;
                case "windows":
                    var w = await GenerateWindowSchedule(gw, new GenerateWindowScheduleArgs(pos, args.StyleName, SchedulesPalette.LayerTables, args.TextStyle, null, false, "—"), ct).ConfigureAwait(false);
                    newSummary = w.Summary; break;
                case "rooms":
                    var r = await GenerateRoomSchedule(gw, new GenerateRoomScheduleArgs(pos, args.StyleName, SchedulesPalette.LayerTables, args.TextStyle, args.LabelLayers, args.BoundaryLayer, false, true, "—"), ct).ConfigureAwait(false);
                    newSummary = r.Summary; break;
                case "finish":
                    var f = await GenerateFinishLegend(gw, new GenerateFinishLegendArgs(pos, args.StyleName, SchedulesPalette.LayerLegend, args.TextStyle, null, false), ct).ConfigureAwait(false);
                    newSummary = f.Summary; break;
                default:
                    skipped.Add($"{oldHandle}: classified as '{kind}' which has no generator"); continue;
            }

            // Erase the old table only after the replacement has been created successfully, so
            // a mid-flight failure cannot leave the drawing with zero copies of the schedule.
            await EraseAsync(gw, oldHandle, ct).ConfigureAwait(false);

            updated.Add(new UpdateScheduleEntry(
                oldHandle, newSummary.TableHandle, title, kind, rowsBefore, newSummary.Rows));
        }

        return new UpdateSchedulesResult(updated, skipped, tablesArr.Count);
    }

    // ─────────────────────────────────────────────────────────────
    // helpers
    // ─────────────────────────────────────────────────────────────

    private static string ClassifySchedule(string title)
    {
        if (title.Contains("DRZWIOW", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("DOOR", StringComparison.OrdinalIgnoreCase)) return "doors";
        if (title.Contains("OKIENN", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("WINDOW", StringComparison.OrdinalIgnoreCase)) return "windows";
        if (title.Contains("POMIESZCZE", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("ROOM", StringComparison.OrdinalIgnoreCase)) return "rooms";
        if (title.Contains("WYKO", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("FINISH", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("LEGEND", StringComparison.OrdinalIgnoreCase)) return "finish";
        return "unknown";
    }

    private static async Task EnsureTableStyleAsync(IPluginGateway gw, string styleName, string textStyle, CancellationToken ct)
    {
        if (!SchedulesPalette.Presets.TryGetValue(styleName, out var preset))
        {
            // Unknown preset name — still register a bare TableStyle with defaults so the Table
            // object can point at a named style. Do not throw; custom names are allowed.
            preset = new SchedulesPalette.TableStylePreset(styleName, 5.0, 3.5, 2.5, 0, 0);
        }
        var args = new JsonObject
        {
            ["name"]             = preset.Name,
            ["titleTextHeight"]  = preset.TitleTextHeight,
            ["headerTextHeight"] = preset.HeaderTextHeight,
            ["bodyTextHeight"]   = preset.BodyTextHeight,
            ["textStyle"]        = textStyle,
            ["titleFillAci"]     = preset.TitleFillAci,
            ["headerFillAci"]    = preset.HeaderFillAci,
            ["makeCurrent"]      = false,
        };
        await gw.InvokeAsync("acad.schedules.ensure_table_style", args, T_NORMAL, ct).ConfigureAwait(false);
    }

    private sealed record OpeningRow(
        string? Number, string? Type, double WidthMm, double HeightMm,
        int Rei, int Rc, string? FireClass, int AcousticDb,
        bool LeadShielded, double SillMm, string? RoomFrom, string? RoomTo);

    private static async Task<List<OpeningRow>> FetchOpeningsAsync(IPluginGateway gw, string kind, string? layerFilter, CancellationToken ct)
    {
        var args = new JsonObject { ["kind"] = kind };
        if (!string.IsNullOrWhiteSpace(layerFilter)) args["layerFilter"] = layerFilter;
        var resp = await gw.InvokeAsync("acad.openings.list_openings_in_model", args, T_NORMAL, ct).ConfigureAwait(false)
                   ?? throw new InvalidPluginShapeException("list_openings_in_model returned null");
        var arr = resp["openings"] as JsonArray ?? new JsonArray();
        var list = new List<OpeningRow>(arr.Count);
        foreach (var n in arr)
        {
            if (n is null) continue;
            list.Add(new OpeningRow(
                n["number"]?.GetValue<string>(),
                n["type"]?.GetValue<string>(),
                GetDouble(n, "widthMm"),
                GetDouble(n, "heightMm"),
                GetInt(n, "rei"),
                GetInt(n, "rc"),
                n["fireClass"]?.GetValue<string>(),
                GetInt(n, "acousticDb"),
                n["leadShielded"]?.GetValue<bool>() ?? false,
                GetDouble(n, "sillMm"),
                n["roomFrom"]?.GetValue<string>(),
                n["roomTo"]?.GetValue<string>()));
        }
        return list;
    }

    private sealed record RoomBounds(double MinX, double MinY, double MaxX, double MaxY);

    private sealed record RoomRow(string Handle, string Text, string Layer, Point2dDto Position, double HeightMm, double? AreaM2, RoomBounds? Bounds, string? Kind = null);

    private static async Task<List<RoomRow>> FetchRoomsAsync(IPluginGateway gw, IReadOnlyList<string>? labelLayers, string? boundaryLayer, CancellationToken ct, bool allLayers = false)
    {
        var args = new JsonObject();
        if (labelLayers is { Count: > 0 }) args["labelLayers"] = JsonSerializer.SerializeToNode(labelLayers, Opts);
        if (!string.IsNullOrWhiteSpace(boundaryLayer)) args["boundaryLayer"] = boundaryLayer;
        if (allLayers) args["allLayers"] = true;
        var resp = await gw.InvokeAsync("acad.schedules.list_room_labels", args, T_SLOW, ct).ConfigureAwait(false)
                   ?? throw new InvalidPluginShapeException("list_room_labels returned null");
        var arr = resp["rooms"] as JsonArray ?? new JsonArray();
        var list = new List<RoomRow>(arr.Count);
        foreach (var n in arr)
        {
            if (n is null) continue;
            var posNode = n["position"];
            if (posNode is null) continue;
            var pos = posNode.Deserialize<Point2dDto>(Opts);
            if (pos is null) continue;
            RoomBounds? bounds = null;
            if (n["boundsMinX"] is not null && n["boundsMaxX"] is not null)
            {
                bounds = new RoomBounds(GetDouble(n, "boundsMinX"), GetDouble(n, "boundsMinY"),
                                        GetDouble(n, "boundsMaxX"), GetDouble(n, "boundsMaxY"));
            }
            list.Add(new RoomRow(
                n["handle"]?.GetValue<string>() ?? "",
                n["text"]?.GetValue<string>() ?? "",
                n["layer"]?.GetValue<string>() ?? "",
                pos,
                GetDouble(n, "heightMm"),
                n["areaM2"]?.GetValue<double>(),
                bounds,
                n["kind"]?.GetValue<string>()));
        }
        return list;
    }

    private sealed record RoomRegionData(bool Found, string Method, double? AreaM2, RoomBounds? Bounds, IReadOnlyList<PointXY> Outline);

    /// <summary>
    /// Ask the plugin to detect the enclosed region of the room containing (x,y): wall-aware flood-fill
    /// with raycast / closed-polyline fallbacks. Returns null only when the primitive is unavailable.
    /// </summary>
    private static async Task<RoomRegionData?> FetchRoomRegionAsync(
        IPluginGateway gw, double x, double y, CancellationToken ct, double? labelAreaM2 = null,
        bool sealAllDoors = true, int? timeoutMs = null, double? cellMm = null)
    {
        var args = new JsonObject { ["x"] = x, ["y"] = y, ["sealAllDoors"] = sealAllDoors };
        if (labelAreaM2 is { } la && la > 0) args["labelAreaM2"] = la;
        // MEASURED 2026-08-12: the flood-fill's automatic cell size scales with the extent of
        // EVERY wall in the model, not the room being measured - Clamp(wholeModelExtent / 600,
        // 50, 500)mm. A 75-room, 5-floor drawing (identical room geometry throughout) measured
        // the SAME room at 5.97-6.19 m2 depending only on which floor it sat on, against a true
        // 6.70 m2 - up to 11% off, enough to trip audit_all_rooms's default 10% tolerance on 60
        // of 75 rooms. Precision silently degrades as the building grows; a caller working on a
        // large model can now ask for a smaller cell explicitly instead.
        if (cellMm is { } cm && cm > 0) args["cellMm"] = cm;
        JsonNode? resp;
        try
        {
            resp = await gw.InvokeAsync("acad.schedules.get_room_region", args, timeoutMs ?? T_SLOW, ct).ConfigureAwait(false);
        }
        catch
        {
            return null; // older plugin without the primitive — caller falls back to label bounds
        }
        if (resp is null) return null;

        bool found = resp["found"]?.GetValue<bool>() ?? false;
        string method = resp["method"]?.GetValue<string>() ?? "none";
        double? area = resp["areaM2"] is { } an ? an.GetValue<double>() : (double?)null;

        RoomBounds? bounds = null;
        if (resp["boundsMinX"] is not null && resp["boundsMaxX"] is not null)
            bounds = new RoomBounds(GetDouble(resp, "boundsMinX"), GetDouble(resp, "boundsMinY"),
                                    GetDouble(resp, "boundsMaxX"), GetDouble(resp, "boundsMaxY"));

        var outline = new List<PointXY>();
        if (resp["outline"] is JsonArray oa)
        {
            foreach (var pt in oa)
            {
                if (pt is null) continue;
                outline.Add(new PointXY(GetDouble(pt, "x"), GetDouble(pt, "y")));
            }
        }
        return new RoomRegionData(found, method, area, bounds, outline);
    }

    private sealed record OpeningRef(string Handle, string Kind, string? Number, string? BlockName, double WidthMm, double HeightMm, Point2dDto Position);
    private sealed record FurnitureRef(string Handle, string BlockName, string? Type, string? Layer, double RotationDeg, Point2dDto Position);

    private static async Task<List<OpeningRef>> FetchOpeningRefsAsync(IPluginGateway gw, CancellationToken ct)
    {
        var resp = await gw.InvokeAsync("acad.openings.list_openings_in_model", new JsonObject { ["kind"] = "all" }, T_NORMAL, ct).ConfigureAwait(false);
        var arr = resp?["openings"] as JsonArray ?? new JsonArray();
        var list = new List<OpeningRef>(arr.Count);
        foreach (var n in arr)
        {
            if (n is null) continue;
            var pos = n["position"]?.Deserialize<Point2dDto>(Opts);
            if (pos is null) continue;
            list.Add(new OpeningRef(
                n["handle"]?.GetValue<string>() ?? "",
                (n["kind"]?.GetValue<string>() ?? "door").ToLowerInvariant(),
                n["number"]?.GetValue<string>(),
                n["blockName"]?.GetValue<string>(),
                GetDouble(n, "widthMm"), GetDouble(n, "heightMm"), pos));
        }
        return list;
    }

    private static async Task<List<FurnitureRef>> FetchFurnitureRefsAsync(IPluginGateway gw, CancellationToken ct)
    {
        var resp = await gw.InvokeAsync("acad.furniture.list_furniture_in_model", new JsonObject(), T_NORMAL, ct).ConfigureAwait(false);
        var arr = resp?["references"] as JsonArray ?? new JsonArray();
        var list = new List<FurnitureRef>(arr.Count);
        foreach (var n in arr)
        {
            if (n is null) continue;
            var pos = n["position"]?.Deserialize<Point2dDto>(Opts);
            if (pos is null) continue;
            list.Add(new FurnitureRef(
                n["handle"]?.GetValue<string>() ?? "",
                n["blockName"]?.GetValue<string>() ?? "",
                n["type"]?.GetValue<string>(),
                n["layer"]?.GetValue<string>(),
                GetDouble(n, "rotationDeg"), pos));
        }
        return list;
    }

    private static bool Inside(RoomBounds b, Point2dDto p)
        => p.X >= b.MinX && p.X <= b.MaxX && p.Y >= b.MinY && p.Y <= b.MaxY;

    /// <summary>Parse an area in m² from a label such as "200 m²" or "45,5 m2"; null if none.</summary>
    private static double? ParseAreaM2(string label)
    {
        // Require the m² unit (² / 2 / ^2) so plain dimensions like "3.2 m" are NOT read as an area.
        // No trailing \b: "²" is a non-word char so \b would fail right after it.
        var m = System.Text.RegularExpressions.Regex.Match(
            label, @"(\d+(?:[.,]\d+)?)\s*m(?:²|2|\^2)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var raw = m.Groups[1].Value.Replace(',', '.');
        return double.TryParse(raw, System.Globalization.NumberStyles.Float,
            CultureInfo.InvariantCulture, out var v) && v > 0 ? v : (double?)null;
    }

    private static bool BoundsOverlap(RoomBounds a, RoomBounds b)
        => Math.Abs(a.MinX - b.MinX) < 1.0 && Math.Abs(a.MinY - b.MinY) < 1.0 &&
           Math.Abs(a.MaxX - b.MaxX) < 1.0 && Math.Abs(a.MaxY - b.MaxY) < 1.0;

    /// <summary>Compass wall (N/S/E/W) of the room edge nearest the opening's insertion point.</summary>
    private static string WallOf(RoomBounds b, Point2dDto p)
    {
        double dN = Math.Abs(b.MaxY - p.Y), dS = Math.Abs(p.Y - b.MinY);
        double dE = Math.Abs(b.MaxX - p.X), dW = Math.Abs(p.X - b.MinX);
        double min = Math.Min(Math.Min(dN, dS), Math.Min(dE, dW));
        if (min == dN) return "N";
        if (min == dS) return "S";
        if (min == dE) return "E";
        return "W";
    }

    /// <summary>Split the room's labels into a number (matches a code pattern) and a human name.</summary>
    private static (string? Number, string? Name) SplitNumberName(IReadOnlyList<string> labels, string query)
    {
        // MEASURED 2026-08-12: a pure regex match is a strictly more reliable "room number" than the
        // query-substring fallback, so it must be tried across ALL labels FIRST. Combining both into
        // one FirstOrDefault(a || b) let the fallback win whenever the SEED label (order is arbitrary
        // in batch grouping, unlike a caller-supplied query) was itself an area token like "17,81 m²" -
        // every string trivially "contains" itself, so that label matched its own query and was
        // returned before the real number ("202") was ever reached later in the list.
        string? number = labels.FirstOrDefault(l => System.Text.RegularExpressions.Regex.IsMatch(
                              l, @"^[A-Za-z]?[-]?\d{1,4}[A-Za-z]?$"))
                          ?? labels.FirstOrDefault(l =>
                              l.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 && l.Length <= 8);
        string? name = labels.FirstOrDefault(l => !string.Equals(l, number, StringComparison.OrdinalIgnoreCase)
            && !System.Text.RegularExpressions.Regex.IsMatch(l, @"^[\d.,\s]*m²?$"));
        return (number, name);
    }

    private static async Task<ScheduleGenerationSummary> InsertTableAsync(
        IPluginGateway gw,
        Point2dDto? position,
        IReadOnlyList<IReadOnlyList<string>> data,
        IReadOnlyList<double> colWidths,
        double rowHeight,
        string textStyle,
        string layer,
        string styleName,
        CancellationToken ct,
        string? layoutName = null)
    {
        // 'position' is optional from the wire; missing/null means "anchor at origin" rather than NRE.
        var pos = position ?? new Point2dDto(0.0, 0.0);
        int rows = data.Count;
        int cols = colWidths.Count;
        // Normalise: promote the single-cell title row to a cols-wide row. add_table fills the
        // leftmost cell with the provided text and leaves the rest empty, which renders as a
        // continuous title bar once the TableStyle merges the title row cells.
        double avgColWidth = 0.0;
        foreach (var c in colWidths) avgColWidth += c;
        avgColWidth /= Math.Max(1, cols);

        var dataArg = JsonSerializer.SerializeToNode(data, Opts) ?? new JsonArray();
        var args = new JsonObject
        {
            ["position"]  = new JsonObject { ["x"] = pos.X, ["y"] = pos.Y, ["z"] = 0.0 },
            ["rows"]      = rows,
            ["cols"]      = cols,
            ["rowHeight"] = rowHeight,
            ["colWidth"]  = avgColWidth,
            ["data"]      = dataArg,
            ["textStyle"] = textStyle,
            ["layer"]     = layer,
        };
        if (!string.IsNullOrWhiteSpace(layoutName)) args["layoutName"] = layoutName;
        var resp = await gw.InvokeAsync("acad.annotations.add_table", args, T_SLOW, ct).ConfigureAwait(false)
                   ?? throw new InvalidPluginShapeException("add_table returned null");
        var entityNode = resp["entity"] ?? throw new InvalidPluginShapeException("add_table missing 'entity'");
        var entity = entityNode.Deserialize<EntityHandle>(Opts)
                     ?? throw new InvalidPluginShapeException("add_table returned non-EntityHandle");

        string title = data.Count > 0 && data[0].Count > 0 ? data[0][0] : "";
        return new ScheduleGenerationSummary(title, rows, cols, entity.Handle, styleName, pos);
    }

    private static async Task EraseAsync(IPluginGateway gw, string handle, CancellationToken ct)
    {
        var args = new JsonObject
        {
            ["handles"] = new JsonArray(new JsonNode?[] { JsonValue.Create(handle) }),
        };
        await gw.InvokeAsync("acad.modify.erase", args, T_NORMAL, ct).ConfigureAwait(false);
    }

    private static double GetDouble(JsonNode n, string key)
    {
        var v = n[key];
        if (v is null) return 0.0;
        try { return v.GetValue<double>(); }
        catch { return 0.0; }
    }

    private static int GetInt(JsonNode n, string key)
    {
        var v = n[key];
        if (v is null) return 0;
        try { return v.GetValue<int>(); }
        catch { return 0; }
    }

    private static string FormatMm(double v, string fallback) =>
        v > 0 ? v.ToString("F0", CultureInfo.InvariantCulture) : fallback;

    private static string JoinRooms(string? from, string? to, string fallback)
    {
        var f = string.IsNullOrWhiteSpace(from) ? null : from;
        var t = string.IsNullOrWhiteSpace(to)   ? null : to;
        if (f is null && t is null) return fallback;
        if (t is null) return f!;
        if (f is null) return t!;
        return $"{f} / {t}";
    }

    private static string StripMtextCodes(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        // MText inline formatting codes (\fFont;\H0.75x;\C1; etc.) and braces are not wanted
        // in a plain-text schedule cell. Strip the most common cases — this is a deliberately
        // shallow cleaner, good enough for room labels.
        var sb = new System.Text.StringBuilder(s.Length);
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length)
            {
                // Skip until the next ';' which terminates the code.
                int sc = s.IndexOf(';', i + 1);
                if (sc > 0) { i = sc + 1; continue; }
            }
            if (c == '{' || c == '}') { i++; continue; }
            sb.Append(c);
            i++;
        }
        return sb.ToString().Trim();
    }
}

internal static class SchedulesExtensions
{
    public static string OrDefault(this string? s, string fallback) =>
        string.IsNullOrWhiteSpace(s) ? fallback : s!;
}
