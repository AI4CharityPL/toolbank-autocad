// AutoCAD acad-modify category. 18 tools: transforms, copy/array/mirror,
// property updates, grouping, erase. Each method is a thin proxy through
// IPluginGateway to "acad.modify.<verb>".
//
// Rules: 19-tool-implementation-pattern.md, 20..25.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Modify;

public static class ModifyTools
{
    private const int T_FAST = 5_000;
    private const int T_NORMAL = 15_000;
    private const int T_SLOW = 30_000;

    // ─────────────── transforms ───────────────

    [McpTool("move", "Translate one or more entities by the vector from→to (WCS).", "modify",
        Intent = new[] { "przesun obiekty", "move entities", "translate selection", "shift entities by vector", "displace selected" },
        RequiresPlugin = true)]
    public static Task<AffectedCount> Move(IPluginGateway gw, MoveArgs args, CancellationToken ct)
        => ModifyProxy.CallAsync<MoveArgs, AffectedCount>(gw, "acad.modify.move", args, T_NORMAL, ct);

    [McpTool("rotate", "Rotate entities around a center by angle (degrees, CCW). Optional axis vector for 3D rotations (default Z).", "modify",
        Intent = new[] { "obroc obiekty", "rotate entities", "spin selection by degrees", "rotate around point", "rotate selection" },
        RequiresPlugin = true)]
    public static Task<AffectedCount> Rotate(IPluginGateway gw, RotateArgs args, CancellationToken ct)
        => ModifyProxy.CallAsync<RotateArgs, AffectedCount>(gw, "acad.modify.rotate", args, T_NORMAL, ct);

    [McpTool("scale", "Uniformly scale entities about a center point by a positive factor.", "modify",
        Intent = new[] { "skaluj obiekty", "zmien skale obiektow", "scale entities", "uniform scale selection", "resize selection" },
        RequiresPlugin = true)]
    public static Task<AffectedCount> Scale(IPluginGateway gw, ScaleArgs args, CancellationToken ct)
        => ModifyProxy.CallAsync<ScaleArgs, AffectedCount>(gw, "acad.modify.scale", args, T_NORMAL, ct);

    [McpTool("mirror", "Mirror entities through a plane defined by point + normal (3D); optionally erase the source entities.", "modify",
        Intent = new[] { "lustro obiektow", "mirror entities", "mirror across plane", "reflect selection", "lustro 3d" },
        RequiresPlugin = true)]
    public static Task<EntitiesAffected> Mirror(IPluginGateway gw, MirrorArgs args, CancellationToken ct)
        => ModifyProxy.CallAsync<MirrorArgs, EntitiesAffected>(gw, "acad.modify.mirror", args, T_NORMAL, ct);

    [McpTool("copy", "Copy entities by translation from→to. Set count > 1 for an evenly stepped chain of copies.", "modify",
        Intent = new[] { "kopiuj obiekty", "copy entities", "duplicate selection", "make copies", "kopia z przesunieciem" },
        RequiresPlugin = true)]
    public static Task<CopiedEntities> Copy(IPluginGateway gw, CopyArgs args, CancellationToken ct)
        => ModifyProxy.CallAsync<CopyArgs, CopiedEntities>(gw, "acad.modify.copy", args, T_NORMAL, ct);

    [McpTool("array_rectangular", "Rectangular array (rows × cols × levels) by row, column and optional Z level spacing.", "modify",
        Intent = new[] { "macierz prostokatna", "siatka kopii", "rectangular array", "build grid array", "array entities by rows and cols" },
        RequiresPlugin = true)]
    public static Task<CopiedEntities> ArrayRectangular(IPluginGateway gw, ArrayRectArgs args, CancellationToken ct)
        => ModifyProxy.CallAsync<ArrayRectArgs, CopiedEntities>(gw, "acad.modify.array_rectangular", args, T_SLOW, ct);

    [McpTool("array_polar", "Polar (circular) array around a center, distributing N items over the given total angle. Optionally rotate items along the path.", "modify",
        Intent = new[] { "macierz biegunowa", "macierz kolowa", "polar array", "circular array", "distribute around point" },
        RequiresPlugin = true)]
    public static Task<CopiedEntities> ArrayPolar(IPluginGateway gw, ArrayPolarArgs args, CancellationToken ct)
        => ModifyProxy.CallAsync<ArrayPolarArgs, CopiedEntities>(gw, "acad.modify.array_polar", args, T_SLOW, ct);

    [McpTool("align", "Align entities so that source point pair (A,B) maps onto target point pair (A,B). Optional uniform scale to make distances match.", "modify",
        Intent = new[] { "dopasuj obiekty", "align entities", "two point alignment", "fit entities to two points", "wyrownanie do dwoch punktow" },
        RequiresPlugin = true)]
    public static Task<AffectedCount> Align(IPluginGateway gw, AlignArgs args, CancellationToken ct)
        => ModifyProxy.CallAsync<AlignArgs, AffectedCount>(gw, "acad.modify.align", args, T_NORMAL, ct);

    // ─────────────── properties ───────────────

    [McpTool("set_layer", "Move entities to the given layer (creates the layer if missing).", "modify",
        Intent = new[] { "przenies na warstwe", "zmien warstwe obiektow", "set layer of entities", "move to layer", "change entity layer" },
        RequiresPlugin = true)]
    public static Task<AffectedCount> SetLayer(IPluginGateway gw, SetLayerArgs args, CancellationToken ct)
        => ModifyProxy.CallAsync<SetLayerArgs, AffectedCount>(gw, "acad.modify.set_layer", args, T_NORMAL, ct);

    [McpTool("set_color", "Set the entity color to a true RGB color or an ACI index (1..255).", "modify",
        Intent = new[] { "zmien kolor obiektow", "ustaw kolor", "set entity color", "change color of entities", "color rgb or aci" },
        RequiresPlugin = true)]
    public static Task<AffectedCount> SetColor(IPluginGateway gw, SetColorArgs args, CancellationToken ct)
        => ModifyProxy.CallAsync<SetColorArgs, AffectedCount>(gw, "acad.modify.set_color", args, T_NORMAL, ct);

    [McpTool("set_linetype", "Set the linetype (by name) and optional linetype scale on entities. The linetype must already be loaded.", "modify",
        Intent = new[] { "zmien typ linii", "ustaw linetype", "set linetype on entities", "change line type", "set ltscale on entities" },
        RequiresPlugin = true)]
    public static Task<AffectedCount> SetLinetype(IPluginGateway gw, SetLinetypeArgs args, CancellationToken ct)
        => ModifyProxy.CallAsync<SetLinetypeArgs, AffectedCount>(gw, "acad.modify.set_linetype", args, T_NORMAL, ct);

    [McpTool("set_lineweight", "Set entity lineweight in millimeters. Common values: 0.13, 0.18, 0.25, 0.5, 0.7, 1.0 mm.", "modify",
        Intent = new[] { "zmien grubosc linii", "ustaw lineweight", "set entity lineweight", "change line thickness", "linia w mm" },
        RequiresPlugin = true)]
    public static Task<AffectedCount> SetLineweight(IPluginGateway gw, SetLineweightArgs args, CancellationToken ct)
        => ModifyProxy.CallAsync<SetLineweightArgs, AffectedCount>(gw, "acad.modify.set_lineweight", args, T_NORMAL, ct);

    [McpTool("match_properties", "Copy generic properties (layer, color, linetype, lineweight, ltscale) from source entity onto target entities.", "modify",
        Intent = new[] { "kopiuj wlasciwosci", "match properties", "matchprop entities", "copy props from source", "apply properties to targets" },
        RequiresPlugin = true)]
    public static Task<AffectedCount> MatchProperties(IPluginGateway gw, MatchPropertiesArgs args, CancellationToken ct)
        => ModifyProxy.CallAsync<MatchPropertiesArgs, AffectedCount>(gw, "acad.modify.match_properties", args, T_NORMAL, ct);

    // ─────────────── lifecycle ───────────────

    [McpTool("erase", "Erase entities (soft delete – AutoCAD keeps them in the undo stack until purged).", "modify",
        Intent = new[] { "usun obiekty", "skasuj zaznaczone", "erase entities", "delete entities", "remove from drawing" },
        RequiresPlugin = true)]
    public static Task<AffectedCount> Erase(IPluginGateway gw, HandlesArgs args, CancellationToken ct)
        => ModifyProxy.CallAsync<HandlesArgs, AffectedCount>(gw, "acad.modify.erase", args, T_NORMAL, ct);

    // ─────────────── undo / redo: WITHDRAWN 2026-08-04 ───────────────
    //
    // Measured, not assumed. Draw a circle, call undo, then look for the entity:
    //
    //     undo -> {"queued": true, "note": "...runs after this call returns..."}
    //     after 0.0s -> still there
    //     after 0.5s -> still there
    //     after 1.5s -> still there
    //     after 3.0s -> still there
    //
    // The queued UNDO never runs in this dispatch context. The tool was honest about being
    // unable to confirm anything, which was an improvement on the fabricated `affected: 1` it
    // used to return - but "honest about doing nothing" is still a tool named `undo` that does
    // not undo. An agent reading the name will reach for it at exactly the moment it most needs
    // the drawing put back.
    //
    // And it is redundant. acad_undo_checkpoint / acad_restore_checkpoint work, verified the
    // same afternoon: checkpoint, draw a circle, restore, the circle is gone
    // (strategy=reopened_snapshot). A working rollback already ships.
    //
    // Withdrawn the same way as the parametric constraint tools: the [McpTool] attributes are
    // removed so the tools stop being advertised, while the plugin handlers stay registered and
    // the proxy methods below stay compilable. Restoring them is re-adding two attributes, if
    // the command channel ever becomes reliable.
    //
    // See docs/KNOWN-GAPS.md A2.

    public static Task<QueuedCommandResult> Undo(IPluginGateway gw, HandlesArgs? _ = null, CancellationToken ct = default)
        => ModifyProxy.CallAsync<HandlesArgs, QueuedCommandResult>(gw, "acad.modify.undo", _ ?? new HandlesArgs(System.Array.Empty<string>()), T_FAST, ct);

    public static Task<QueuedCommandResult> Redo(IPluginGateway gw, HandlesArgs? _ = null, CancellationToken ct = default)
        => ModifyProxy.CallAsync<HandlesArgs, QueuedCommandResult>(gw, "acad.modify.redo", _ ?? new HandlesArgs(System.Array.Empty<string>()), T_FAST, ct);

    // ─────────────── grouping ───────────────

    [McpTool("create_group", "Create a named AutoCAD Group containing the given entities. Selectable=true makes the group pickable as a unit.", "modify",
        Intent = new[] { "stworz grupe", "create group", "make group from entities", "group selection", "named group" },
        RequiresPlugin = true)]
    public static Task<GroupNameResult> CreateGroup(IPluginGateway gw, GroupCreateArgs args, CancellationToken ct)
        => ModifyProxy.CallAsync<GroupCreateArgs, GroupNameResult>(gw, "acad.modify.create_group", args, T_NORMAL, ct);

    [McpTool("ungroup", "Delete a named Group (the underlying entities remain in the drawing).", "modify",
        Intent = new[] { "rozgrupuj", "usun grupe", "ungroup", "delete group", "remove named group" },
        RequiresPlugin = true)]
    public static Task<AffectedCount> Ungroup(IPluginGateway gw, GroupNameArgs args, CancellationToken ct)
        => ModifyProxy.CallAsync<GroupNameArgs, AffectedCount>(gw, "acad.modify.ungroup", args, T_NORMAL, ct);

    // ─────────── transforming by reference (roadmap 3.1) ───────────
    //
    // `scale` and `rotate` take a factor and an angle. These take a MEASUREMENT, which is how
    // the operation is actually reached: nobody knows a scanned plan is out by 1.0473, they know
    // a door that should be 900 measures 859.

    [McpTool("scale_by_reference", "Scale entities so that a REFERENCE LENGTH becomes a new length, computing the factor rather than being given it - AutoCAD's SCALE Reference. Give the reference either as referenceLength or as two points (referenceStart, referenceEnd) to measure between, which is usually what you have: a scanned plan whose 900 mm door measures 859 is scaled by giving those two numbers, not by working out 1.0477. The base point does not move. Use modify.scale when the factor is already known.", "modify",
        Intent = new[] { "przeskaluj wedlug wymiaru", "dopasuj skale rysunku do wymiaru",
                         "scale by reference", "scale so this measures that",
                         "skalowanie referencyjne", "fix the scale of a scanned drawing" },
        RequiresPlugin = true)]
    public static Task<ReferenceScaleResult> ScaleByReference(IPluginGateway gw, ReferenceScaleArgs args, CancellationToken ct)
        => ModifyProxy.CallAsync<ReferenceScaleArgs, ReferenceScaleResult>(gw, "acad.modify.scale_by_reference", args, T_NORMAL, ct);

    [McpTool("rotate_by_reference", "Rotate entities so that a REFERENCE DIRECTION ends up pointing at a new angle, computing the turn rather than being given it - AutoCAD's ROTATE Reference. Give the reference either as referenceAngleDeg or as two points whose direction is the reference, which is what you have when straightening something drawn at an unknown skew. Angles are degrees CCW from the X axis and the base point does not move. Use modify.rotate when the turn is already known.", "modify",
        Intent = new[] { "obroc wedlug referencji", "wyprostuj obiekt narysowany krzywo",
                         "rotate by reference", "rotate so this edge becomes horizontal",
                         "obrot referencyjny", "straighten a skewed drawing" },
        RequiresPlugin = true)]
    public static Task<ReferenceRotateResult> RotateByReference(IPluginGateway gw, ReferenceRotateArgs args, CancellationToken ct)
        => ModifyProxy.CallAsync<ReferenceRotateArgs, ReferenceRotateResult>(gw, "acad.modify.rotate_by_reference", args, T_NORMAL, ct);
}
