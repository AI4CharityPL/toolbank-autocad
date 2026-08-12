// Single source of truth for the AIA-style architectural layer key shipped with
// acad-architecture. Mirrors rule 36 §11 (architecture-domain-traps.md).
//
// If you add a new layer here, also:
//   1. Update rule 36 §11 (the table).
//   2. Add a paired validator under validators/architectural/ if the layer
//      implies a "must be on this layer" rule.

using System.Collections.Generic;

namespace AcadMcp.Backend.Categories.Architecture;

/// <summary>
/// Canonical layer names + colours + linetypes for the architectural plan
/// view. Used by <c>ensure_architectural_layers</c> and as default values for
/// every drawing tool in the category.
/// </summary>
internal static class ArchitecturePalette
{
    // Architectural layers (A-*).
    public const string LayerWall            = "A-WALL";
    public const string LayerWallCtrl        = "A-WALL-CTRL";
    public const string LayerDoor            = "A-DOOR";
    public const string LayerDoorSwing       = "A-DOOR-SWING";
    public const string LayerGlazing         = "A-GLAZ";
    public const string LayerRoomBoundary    = "A-ROOM-BNDY";
    public const string LayerRoomIdentification = "A-ROOM-IDEN";
    public const string LayerCeiling         = "A-CLNG";
    public const string LayerRoof            = "A-ROOF";
    public const string LayerStairs          = "A-STRS";
    public const string LayerAnnoDims        = "A-ANNO-DIMS";
    public const string LayerAnnoNotes       = "A-ANNO-NOTE";

    // Structural layers (S-*) referenced from architectural plans. Also consumed by
    // acad-structural (columns/beams/lintels) - this stays the single S-* key for both
    // categories rather than forking a second one; see rule 72 §1 for why.
    public const string LayerColumns         = "S-COLS";
    public const string LayerColumnsCtrl     = "S-COLS-CTRL";
    public const string LayerSlab            = "S-SLAB";
    public const string LayerSlabHatch       = "S-SLAB-HATCH";
    public const string LayerBeam            = "S-BEAM";
    public const string LayerBeamCtrl        = "S-BEAM-CTRL";
    public const string LayerLintel          = "S-LINTEL";

    /// <summary>One descriptor per layer in the AIA-style architectural key.</summary>
    public sealed record Spec(string Name, int AciColor, string Linetype, string Purpose, bool Structural);

    public static IReadOnlyList<Spec> All { get; } = new List<Spec>
    {
        new(LayerWall,            7, "Continuous", "wall faces (visible)",                false),
        new(LayerWallCtrl,        8, "CENTER",     "wall centrelines",                    false),
        new(LayerDoor,           30, "Continuous", "door leaves and frames",              false),
        new(LayerDoorSwing,      30, "DASHED",     "door swing arcs",                     false),
        new(LayerGlazing,         4, "Continuous", "glazing, sills, headers",             false),
        new(LayerRoomBoundary,    8, "DASHED",     "room boundary polylines",             false),
        new(LayerRoomIdentification, 7, "Continuous", "room tags",                        false),
        new(LayerCeiling,         5, "Continuous", "ceiling outlines",                    false),
        new(LayerRoof,            5, "Continuous", "roof outlines",                       false),
        new(LayerStairs,          6, "Continuous", "stair outlines and treads",           false),
        new(LayerAnnoDims,        2, "Continuous", "dimensions",                          false),
        new(LayerAnnoNotes,       2, "Continuous", "text notes, leaders",                 false),
        new(LayerColumns,         1, "Continuous", "structural columns",                  true),
        new(LayerColumnsCtrl,     8, "CENTER",     "column centre-marks",                 true),
        new(LayerSlab,            7, "Continuous", "floor slab outlines",                 true),
        new(LayerSlabHatch,       8, "Continuous", "slab fill hatch",                     true),
        new(LayerBeam,            1, "DASHED",     "beam plan-projection outline",        true),
        new(LayerBeamCtrl,        8, "CENTER",     "beam centrelines",                    true),
        new(LayerLintel,          1, "DASHED",     "lintel plan-projection over openings (heuristic sizing, not a structural calculation - rule 72)", true),
    };

    /// <summary>
    /// Block files we plan to ship under <c>blocks/architectural/</c>. Documented
    /// here so introspection tools can list them even before the binary blocks
    /// are committed (rule 36 §12).
    /// </summary>
    public static IReadOnlyList<string> PlannedBlocks { get; } = new[]
    {
        "DOOR_SINGLE_900.dwg",
        "DOOR_DOUBLE_1800.dwg",
        "WINDOW_1200x600.dwg",
        "ROOM_TAG.dwg",
        "TITLEBLOCK_A1.dwg",
        "TITLEBLOCK_A3.dwg",
    };
}
