// AutoCAD acad-structural domain category. Steel column/beam profiles from a real,
// representative EN 10365 catalog subset, and span-based lintel sizing with an explicit
// engineering-heuristic disclaimer. See rule 72 (structural-domain-traps) for the full
// design rationale, and rule 72 §2 for why this category has (deliberately) almost no
// plugin-side code: insert_steel_column/insert_beam/ensure_structural_layers compose
// existing acad.geometry2d.*/acad.layers.* primitives via the public ArchitectureProxy
// (rule 35 §2), and list_steel_profiles makes no plugin call at all.
//
// Not to be confused with acad-grids: this category sizes/draws structural MEMBERS
// (columns, beams, lintels); acad-grids draws structural axis GRIDLINES. Different
// concerns, see rule 72 §3.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Categories.Architecture;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared;
using AcadMcp.Shared.Catalogs;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Structural;

public static class StructuralTools
{
    private const int T_NORMAL = 15_000;

    private const string LintelDisclaimer =
        "Heuristic span/depth sizing only. This is NOT a substitute for a structural " +
        "engineer's calculation against actual loads, material properties, and the " +
        "applicable Eurocode/PN-EN. Verify before construction.";

    // ─────────── catalog ───────────

    [McpTool("list_steel_profiles",
        "List the built-in steel I/H-section catalog (HEA/HEB/IPE, a representative EN 10365 subset - not the full standard range). Read-only, no AutoCAD document required. Returns designation, series, height/width/web/flange thickness in mm, weight per metre, nominal cross-sectional area (computed without root radius - see insert_steel_column), and the cited standard.",
        "structural",
        Intent = new[]
        {
            "lista profili stalowych", "katalog HEB HEA IPE", "list steel profiles",
            "show steel section catalog", "co mamy w katalogu stali", "wypisz przekroje stalowe"
        },
        ReadOnly = true,
        RequiresPlugin = false)]
    public static ListSteelProfilesResult ListSteelProfiles(ListSteelProfilesArgs args)
    {
        var profiles = SteelProfileCatalog.Filtered(args.SeriesFilter)
            .Select(e => new SteelProfileDto(e.Designation, e.Series, e.HeightMm, e.WidthMm,
                e.WebThicknessMm, e.FlangeThicknessMm, e.WeightKgPerM, e.AreaCm2, e.Standard, e.Description))
            .ToList();
        return new ListSteelProfilesResult(profiles, profiles.Count);
    }

    // ─────────── columns / beams ───────────

    [McpTool("insert_steel_column",
        "Insert a real hot-rolled steel I/H-section column profile (e.g. 'HEB200') on layer S-COLS plus a crosshair centre-mark on S-COLS-CTRL - same layers insert_rect_column/insert_round_column already use, just with a real profile outline (12-vertex, flange+web, no root radius) instead of a rectangle. Names accepted are exactly those returned by list_steel_profiles.",
        "structural",
        Intent = new[]
        {
            "wstaw slup stalowy", "insert steel column", "kolumna HEB200",
            "structural steel H-section column", "wstaw dwuteownik jako slup", "draw HEA column"
        },
        RequiresPlugin = true)]
    public static async Task<InsertSteelColumnResult> InsertSteelColumn(
        IPluginGateway gw, InsertSteelColumnArgs args, CancellationToken ct)
    {
        var resolution = SteelProfileCatalog.Resolve(args.Designation);
        var p = resolution.Entry;

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.ColumnLayer, 1, "Continuous", ct).ConfigureAwait(false))
            created.Add(args.ColumnLayer);
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.CenterMarkLayer, 8, "CENTER", ct).ConfigureAwait(false))
            created.Add(args.CenterMarkLayer);

        double rad = args.RotationDeg * Math.PI / 180.0;
        double cosA = Math.Cos(rad), sinA = Math.Sin(rad);
        double hw = p.WidthMm / 2.0, hh = p.HeightMm / 2.0;
        double webHalf = p.WebThicknessMm / 2.0, innerY = hh - p.FlangeThicknessMm;

        Point2dDto Pt(double x, double y) =>
            new(args.Center.X + x * cosA - y * sinA, args.Center.Y + x * sinA + y * cosA);

        // 12-vertex I/H outline, no root radius (documented simplification - rule 72 §4):
        // bottom flange -> up the inner right web face -> top flange -> down the inner
        // left web face -> back to start.
        var outline = new[]
        {
            Pt(-hw, -hh),           Pt(hw, -hh),            Pt(hw, -innerY),
            Pt(webHalf, -innerY),   Pt(webHalf, innerY),    Pt(hw, innerY),
            Pt(hw, hh),             Pt(-hw, hh),             Pt(-hw, innerY),
            Pt(-webHalf, innerY),   Pt(-webHalf, -innerY),  Pt(-hw, -innerY),
        };
        var profile = await ArchitectureProxy.DrawPolylineAsync(gw, outline, true, args.ColumnLayer, ct).ConfigureAwait(false);

        double m = args.CenterMarkSizeMm / 2.0;
        var ch = await ArchitectureProxy.DrawLineAsync(
            gw, new(args.Center.X - m, args.Center.Y), new(args.Center.X + m, args.Center.Y), args.CenterMarkLayer, ct).ConfigureAwait(false);
        var cv = await ArchitectureProxy.DrawLineAsync(
            gw, new(args.Center.X, args.Center.Y - m), new(args.Center.X, args.Center.Y + m), args.CenterMarkLayer, ct).ConfigureAwait(false);

        return new InsertSteelColumnResult(profile, ch, cv, p.Designation, p.WeightKgPerM, p.AreaCm2, created);
    }

    [McpTool("insert_beam",
        "Insert a beam PLAN-PROJECTION symbol (dashed outline + centreline) between two points - this bank is 2D-plan-symbolic only (no wall-height/elevation datum anywhere), so this draws what a beam looks like from above, not a real 3D member. Give either 'designation' (a steel profile from list_steel_profiles, whose width sets the plan width) or an explicit 'widthMm' (for RC/timber beams not in the steel catalog - keeps this typology-agnostic). Layer S-BEAM (outline) / S-BEAM-CTRL (centreline).",
        "structural",
        Intent = new[]
        {
            "wstaw belke", "insert beam", "narysuj belke stalowa",
            "beam plan symbol", "draw RC beam outline", "belka IPE200"
        },
        RequiresPlugin = true)]
    public static async Task<InsertBeamResult> InsertBeam(IPluginGateway gw, InsertBeamArgs args, CancellationToken ct)
    {
        double widthMm;
        if (args.Designation is not null)
            widthMm = SteelProfileCatalog.Resolve(args.Designation).Entry.WidthMm;
        else if (args.WidthMm is { } w && w > 0)
            widthMm = w;
        else
            throw new ArgumentException("Either 'designation' (a steel profile) or a positive 'widthMm' is required.");

        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.Layer, 1, "DASHED", ct).ConfigureAwait(false))
            created.Add(args.Layer);
        if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.CenterlineLayer, 8, "CENTER", ct).ConfigureAwait(false))
            created.Add(args.CenterlineLayer);

        double dx = args.End.X - args.Start.X, dy = args.End.Y - args.Start.Y;
        double lengthMm = Math.Sqrt(dx * dx + dy * dy);
        if (lengthMm <= 0) throw new ArgumentException("start and end must not coincide");
        double ux = dx / lengthMm, uy = dy / lengthMm; // unit along axis
        double nx = -uy, ny = ux;                       // unit normal
        double hw = widthMm / 2.0;

        var corners = new[]
        {
            new Point2dDto(args.Start.X + nx * hw, args.Start.Y + ny * hw),
            new Point2dDto(args.End.X   + nx * hw, args.End.Y   + ny * hw),
            new Point2dDto(args.End.X   - nx * hw, args.End.Y   - ny * hw),
            new Point2dDto(args.Start.X - nx * hw, args.Start.Y - ny * hw),
        };
        var outline = await ArchitectureProxy.DrawPolylineAsync(gw, corners, true, args.Layer, ct).ConfigureAwait(false);
        var centerline = await ArchitectureProxy.DrawLineAsync(gw, args.Start, args.End, args.CenterlineLayer, ct).ConfigureAwait(false);

        EntityHandle? labelHandle = null;
        if (!string.IsNullOrWhiteSpace(args.Label))
        {
            double midX = (args.Start.X + args.End.X) / 2.0, midY = (args.Start.Y + args.End.Y) / 2.0;
            double rotDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            labelHandle = await ArchitectureProxy.AddDBTextAsync(
                gw, new Point2dDto(midX, midY + hw + 50), args.Label!, 150.0, args.CenterlineLayer, rotDeg, ct).ConfigureAwait(false);
        }

        return new InsertBeamResult(outline, centerline, labelHandle, lengthMm, widthMm, created);
    }

    // ─────────── lintel ───────────

    [McpTool("insert_lintel",
        "Compute a span-based lintel (nadproze) size and optionally draw its plan-projection symbol over a wall opening. HEURISTIC SIZING ONLY - not a structural calculation; the result carries an explicit 'disclaimer' field, and a qualified engineer must verify before construction. Give either ('position'+'rotationDeg'+'spanMm') or a jamb pair ('jamb1'+'jamb2', matching cut_wall_for_opening's own jamb contract) to derive them. materialHint='rc' sizes a reinforced-concrete lintel by depth only; materialHint='steel' additionally suggests the shallowest catalog profile (list_steel_profiles) tall enough. Never cuts or otherwise modifies the wall - purely additive schedule data plus an optional dashed plan symbol on layer S-LINTEL.",
        "structural",
        Intent = new[]
        {
            "wstaw nadproze", "insert lintel", "policz nadproze nad otworem",
            "lintel over door opening", "belka nadprozowa", "size a lintel for this span"
        },
        RequiresPlugin = true)]
    public static async Task<InsertLintelResult> InsertLintel(IPluginGateway gw, InsertLintelArgs args, CancellationToken ct)
    {
        Point2dDto position;
        double rotationDeg;
        double spanMm;

        if (args.Jamb1 is { } j1 && args.Jamb2 is { } j2)
        {
            double dx = j2.X - j1.X, dy = j2.Y - j1.Y;
            spanMm = Math.Sqrt(dx * dx + dy * dy);
            position = new Point2dDto((j1.X + j2.X) / 2.0, (j1.Y + j2.Y) / 2.0);
            rotationDeg = Math.Atan2(dy, dx) * 180.0 / Math.PI;
        }
        else if (args.Position is { } pos && args.SpanMm is { } span && span > 0)
        {
            position = pos;
            rotationDeg = args.RotationDeg;
            spanMm = span;
        }
        else
        {
            throw new ArgumentException("Either ('jamb1' + 'jamb2') or ('position' + 'spanMm') is required.");
        }

        double totalLengthMm = spanMm + 2.0 * args.BearingMm;

        // Rough, explicitly-caveated rule of thumb: span/10, rounded up to the next 10mm,
        // floored at a practical minimum. NOT an engineering calculation - see disclaimer.
        double computedDepthMm = Math.Max(120.0, Math.Ceiling(spanMm / 100.0) * 10.0);

        string materialHint = (args.MaterialHint ?? "rc").ToLowerInvariant();
        string lintelTypeTag;
        string? suggestedProfile = null;
        if (materialHint == "steel")
        {
            var candidate = SteelProfileCatalog.All
                .Where(e => e.HeightMm >= computedDepthMm)
                .OrderBy(e => e.HeightMm)
                .FirstOrDefault();
            if (candidate is null)
                throw new ArgumentException(
                    $"No catalog steel profile is tall enough for a computed depth of {computedDepthMm}mm " +
                    "(span too large for this bank's representative subset) - use materialHint='rc' or a manual designation.");
            suggestedProfile = candidate.Designation;
            lintelTypeTag = candidate.Designation;
        }
        else
        {
            lintelTypeTag = $"RC-{computedDepthMm:F0}x{args.WallThicknessMm:F0}";
        }

        var created = new List<string>();
        EntityHandle? planSymbol = null;
        EntityHandle? markText = null;
        if (args.DrawPlanSymbol)
        {
            var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
            if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, args.Layer, 1, "DASHED", ct).ConfigureAwait(false))
                created.Add(args.Layer);

            double rad = rotationDeg * Math.PI / 180.0;
            double cosA = Math.Cos(rad), sinA = Math.Sin(rad);
            double hl = totalLengthMm / 2.0, hw = args.WallThicknessMm / 2.0;

            Point2dDto Pt(double x, double y) =>
                new(position.X + x * cosA - y * sinA, position.Y + x * sinA + y * cosA);

            var corners = new[] { Pt(-hl, -hw), Pt(hl, -hw), Pt(hl, hw), Pt(-hl, hw) };
            planSymbol = await ArchitectureProxy.DrawPolylineAsync(gw, corners, true, args.Layer, ct).ConfigureAwait(false);

            var tag = args.Mark is { Length: > 0 } m ? $"{m} {lintelTypeTag}" : lintelTypeTag;
            markText = await ArchitectureProxy.AddDBTextAsync(
                gw, new Point2dDto(position.X, position.Y + hw + 60), tag, 120.0, args.Layer, rotationDeg, ct).ConfigureAwait(false);
        }

        return new InsertLintelResult(lintelTypeTag, computedDepthMm, totalLengthMm, suggestedProfile,
            planSymbol, markText, LintelDisclaimer, created);
    }

    // ─────────── layers ───────────

    [McpTool("ensure_structural_layers",
        "Idempotent: create every S-* structural layer (S-COLS, S-COLS-CTRL, S-SLAB, S-SLAB-HATCH, S-BEAM, S-BEAM-CTRL, S-LINTEL) that does not already exist in the drawing. Same shared layer key acad-architecture's ensure_architectural_layers already creates - this exists purely so an agent working only in acad-structural does not need to know to call the architecture tool first.",
        "structural",
        Intent = new[]
        {
            "utworz warstwy strukturalne", "ensure structural layers", "setup S-COLS S-BEAM layers",
            "create beam and lintel layers", "przygotuj warstwy konstrukcyjne"
        },
        RequiresPlugin = true)]
    public static async Task<EnsureStructuralLayersResult> EnsureStructuralLayers(
        IPluginGateway gw, EnsureStructuralLayersArgs args, CancellationToken ct)
    {
        var existing = await ArchitectureProxy.ListLayerNamesAsync(gw, ct).ConfigureAwait(false);
        var created = new List<string>();
        foreach (var spec in ArchitecturePalette.All.Where(s => s.Structural))
        {
            if (await ArchitectureProxy.EnsureLayerAsync(gw, existing, spec.Name, spec.AciColor, spec.Linetype, ct).ConfigureAwait(false))
                created.Add(spec.Name);
        }
        return new EnsureStructuralLayersResult(created);
    }
}
