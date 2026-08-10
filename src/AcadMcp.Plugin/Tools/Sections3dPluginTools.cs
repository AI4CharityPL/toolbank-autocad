// AutoCAD plugin handlers for the acad-sections-3d category.
// Registered under "acad.sections3d.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern), 26 (traps).
//
// A Section is a CUTTING PLANE that lives in the drawing: it does not modify the solids it cuts,
// it reports what the cut would look like. That is the whole difference from
// geometry_3d.slice_solid, which really does cut and leaves you with two solids.
//
// The construction route is the CONSTRUCTOR, not a factory: Section.CreateSectionPlane does not
// exist in either form, and Section.Boundary is read-only with no SetBoundary, so the cut line
// goes in when the object is made - see rule 26 and the 4.4 reconnaissance.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcadRt = Autodesk.AutoCAD.Runtime;

namespace AcadMcp.Plugin.Tools;

internal static class Sections3dPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.sections3d.create_section_plane", CreateSectionPlane);
        host.Register("acad.sections3d.list_section_planes",  ListSectionPlanes);
        host.Register("acad.sections3d.set_section_state",    SetSectionState);
        host.Register("acad.sections3d.set_live_section",     SetLiveSection);
        host.Register("acad.sections3d.set_section_height",   SetSectionHeight);
        host.Register("acad.sections3d.generate_section",     GenerateSection);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    private static Section RequireSection(Database db, Transaction tr, string? handle, OpenMode mode)
    {
        if (string.IsNullOrWhiteSpace(handle))
            throw new ArgumentException("handle is required.");
        var ent = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, handle!), mode);
        if (ent is Section s) return s;
        throw new ArgumentException(
            "Entity " + handle + " is a " + ent.GetRXClass().Name + ", not a section plane. " +
            "Use list_section_planes to find the ones in this drawing.");
    }

    private static SectionState ParseState(string? s) =>
        (s ?? "plane").Trim().ToLowerInvariant() switch
        {
            "plane" => SectionState.Plane,
            "boundary" => SectionState.Boundary,
            "volume" => SectionState.Volume,
            _ => throw new ArgumentException(
                "state must be plane, boundary or volume. PLANE is an unbounded cut, which is " +
                "what a building section wants; BOUNDARY clips it to the outline you gave; " +
                "VOLUME clips it to a box, which is what isolates one room out of a model."),
        };

    private static string StateName(SectionState s) => s switch
    {
        SectionState.Plane => "plane",
        SectionState.Boundary => "boundary",
        SectionState.Volume => "volume",
        _ => s.ToString(),
    };

    // ─────────── making one ───────────

    private static Task<ToolDispatchResult> CreateSectionPlane(JsonObject args, CancellationToken ct) =>
        Run("acad.sections3d.create_section_plane", args, ct, (doc, db, tr) =>
        {
            var a = Read<SectionCreateArgsDto>(args);
            if (a.Vertices is null || a.Vertices.Count < 2)
                throw new ArgumentException(
                    "vertices needs at least two points: the line the section is cut along, seen " +
                    "in plan. More than two gives a jogged section, which is how a plan cuts " +
                    "through different parts of a building at different places.");

            var pts = new Point3dCollection();
            foreach (var v in a.Vertices) pts.Add(AcadEnv.ToPoint3d(v));

            var along = pts[1] - pts[0];
            if (along.Length < 1e-12)
                throw new ArgumentException(
                    "The first two vertices are the same point, so they give no direction to " +
                    "cut along.");

            // The second constructor argument is `verticalDir` - which way is UP in the section -
            // and NOT the plane's normal. The cut plane is the one CONTAINING the section line and
            // this vector, so the normal falls out as line x up and cannot be given directly:
            // Section.Normal is read-only.
            //
            // This is measured, not assumed, and getting it wrong is silent. Passing a horizontal
            // vector here (the old code passed the intended normal) makes the plane contain the
            // plan line AND a horizontal direction - that is the XY plane, so every "vertical"
            // section came back as a horizontal cut at z=0, and the numbers still looked right on
            // a cube because a cube's cut and its silhouette are the same square. See rule 26 §14.
            Vector3d up = a.VerticalDirection is not null
                ? AcadEnv.ToVector3d(a.VerticalDirection)
                : Vector3d.ZAxis;
            if (up.Length < 1e-12)
                throw new ArgumentException("verticalDirection cannot be the zero vector.");
            up = up.GetNormal();
            if (along.GetNormal().CrossProduct(up).Length < 1e-9)
                throw new ArgumentException(
                    "verticalDirection is parallel to the section line, so the two do not define " +
                    "a plane. Up has to point across the line, not along it - the default (0,0,1) " +
                    "is what a vertical building section wants.");

            Section sec;
            try
            {
                // The CONSTRUCTOR, not a factory: there is no Section.CreateSectionPlane, and
                // Boundary is read-only, so the cut line can only go in here.
                sec = new Section(pts, up);
            }
            catch (AcadRt.Exception ex)
            {
                throw new ArgumentException(
                    "AutoCAD refused the section plane with " + ex.ErrorStatus + ".");
            }

            sec.State = ParseState(a.State);
            if (a.Elevation is not null) sec.Elevation = a.Elevation.Value;
            if (a.Height is not null)
                sec.SetHeight(SectionHeight.HeightAboveSectionLine, a.Height.Value);
            if (a.Depth is not null)
                sec.SetHeight(SectionHeight.HeightBelowSectionLine, a.Depth.Value);
            if (a.LiveSection == true) sec.IsLiveSectionEnabled = true;

            var handle = AcadEnv.Persist(db, tr, sec, a.Layer);
            var boundary = sec.Boundary;
            if (boundary is null || boundary.Count < 2)
                throw new InvalidOperationException(
                    "The section plane was created but its boundary came back with " +
                    (boundary?.Count ?? 0) + " points, so the line it was given did not take.");

            // The plane the object ended up with has to be the plane that was asked for, and the
            // only way to know is to check the normal it worked out for itself: it must stand at
            // right angles BOTH to the section line and to up. When the vertical direction was
            // wrong this came back along Z for a line drawn in plan, and nothing else in the
            // result showed it - the cut was simply taken somewhere else.
            var n = sec.Normal;
            if (n.Length < 1e-12 || Math.Abs(n.GetNormal().DotProduct(along.GetNormal())) > 1e-6
                                 || Math.Abs(n.GetNormal().DotProduct(up)) > 1e-6)
                throw new InvalidOperationException(
                    "The section plane came back with normal (" + n.X + ", " + n.Y + ", " + n.Z +
                    "), which does not stand square to both the section line and the vertical " +
                    "direction - so the plane it will cut on is not the one that was asked for.");

            return Wrap(new
            {
                entity = handle,
                vertices = boundary.Count,
                state = StateName(sec.State),
                liveSection = sec.IsLiveSectionEnabled,
                normal = AcadEnv.FromPoint3d(new Point3d(n.X, n.Y, n.Z)),
                verticalDirection = AcadEnv.FromPoint3d(new Point3d(up.X, up.Y, up.Z)),
                note = "A section plane CUTS NOTHING: it is an object in the drawing that reports " +
                       "what a cut would look like, and the solids it crosses are untouched. That " +
                       "is the whole difference from geometry_3d.slice_solid, which really does " +
                       "cut and hands back two solids. Use generate_section to draw the result, " +
                       "and set_live_section to see it on screen without drawing anything. The " +
                       "cut plane CONTAINS the section line and the vertical direction, so the " +
                       "normal above is worked out from those two rather than given - it cannot " +
                       "be set. Default up (0,0,1) gives the vertical section a plan line means; " +
                       "a horizontal up vector gives a horizontal cut through the model instead.",
            });
        });

    private static Task<ToolDispatchResult> ListSectionPlanes(JsonObject args, CancellationToken ct) =>
        Run("acad.sections3d.list_section_planes", args, ct, (doc, db, tr) =>
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            var found = new List<object>();
            foreach (ObjectId id in ms)
            {
                if (id.IsErased) continue;                 // rule 26 §8
                if (tr.GetObject(id, OpenMode.ForRead) is not Section sec) continue;
                var b = sec.Boundary;
                found.Add(new
                {
                    handle = AcadEnv.ToHandle(sec).Handle,
                    state = StateName(sec.State),
                    liveSection = sec.IsLiveSectionEnabled,
                    vertices = b?.Count ?? 0,
                    elevation = sec.Elevation,
                    normal = AcadEnv.FromPoint3d(new Point3d(sec.Normal.X, sec.Normal.Y, sec.Normal.Z)),
                    layer = sec.Layer,
                });
            }

            return Wrap(new
            {
                count = found.Count,
                sections = found,
                note = found.Count == 0
                    ? "No section planes in this drawing. create_section_plane makes one; note " +
                      "that a section plane is an OBJECT, so it stays in the drawing until erased."
                    : found.Count + " section plane(s). At most one can be the LIVE section at a " +
                      "time - turning live on for one turns it off for the others, which is " +
                      "AutoCAD behaviour rather than a choice made here.",
            });
        });

    // ─────────── changing one ───────────

    private static Task<ToolDispatchResult> SetSectionState(JsonObject args, CancellationToken ct) =>
        Run("acad.sections3d.set_section_state", args, ct, (doc, db, tr) =>
        {
            var a = Read<SectionStateArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.State))
                throw new ArgumentException("state is required: plane, boundary or volume.");
            var sec = RequireSection(db, tr, a.Handle, OpenMode.ForWrite);
            var before = sec.State;
            var target = ParseState(a.State);
            if (before == target)
                throw new ArgumentException(
                    "The section is already in the " + StateName(before) + " state, so nothing " +
                    "would change.");

            sec.State = target;
            var landed = sec.State;
            if (landed != target)
                throw new InvalidOperationException(
                    "The state was set to " + StateName(target) + " but reads back as " +
                    StateName(landed) + ".");

            return Wrap(new
            {
                handle = a.Handle,
                stateBefore = StateName(before),
                state = StateName(landed),
                note = "PLANE is an unbounded cut, which is what a building section wants. " +
                       "BOUNDARY clips it to the outline the plane was given. VOLUME clips it to " +
                       "a box as well, which is what isolates one room or one bay out of a whole " +
                       "model. The state changes what generate_section produces and what a live " +
                       "section shows; it does not change the cut line itself.",
            });
        });

    private static Task<ToolDispatchResult> SetLiveSection(JsonObject args, CancellationToken ct) =>
        Run("acad.sections3d.set_live_section", args, ct, (doc, db, tr) =>
        {
            var a = Read<SectionLiveArgsDto>(args);
            if (a.Enabled is null)
                throw new ArgumentException("enabled is required: true or false.");
            var sec = RequireSection(db, tr, a.Handle, OpenMode.ForWrite);
            var before = sec.IsLiveSectionEnabled;
            if (before == a.Enabled.Value)
                throw new ArgumentException(
                    "The live section is already " + (before ? "on" : "off") + " for this plane, " +
                    "so nothing would change.");

            sec.IsLiveSectionEnabled = a.Enabled.Value;
            var landed = sec.IsLiveSectionEnabled;
            if (landed != a.Enabled.Value)
                throw new InvalidOperationException(
                    "Live section was set to " + a.Enabled.Value + " but reads back as " + landed +
                    ".");

            return Wrap(new
            {
                handle = a.Handle,
                liveSectionBefore = before,
                liveSection = landed,
                note = "A LIVE section shows the cut on screen without drawing anything - the " +
                       "model in front of the plane is hidden and the cut face is shaded, and " +
                       "nothing is added to the drawing. generate_section is the other half: it " +
                       "draws real geometry you can dimension and plot. Only one section can be " +
                       "live at a time, so turning this on turns the others off.",
            });
        });

    private static Task<ToolDispatchResult> SetSectionHeight(JsonObject args, CancellationToken ct) =>
        Run("acad.sections3d.set_section_height", args, ct, (doc, db, tr) =>
        {
            var a = Read<SectionHeightArgsDto>(args);
            if (a.Above is null && a.Below is null && a.Elevation is null)
                throw new ArgumentException(
                    "Give at least one of above, below or elevation. above and below are how far " +
                    "the cutting plane reaches up and down from its line; elevation is where that " +
                    "line sits in Z.");
            var sec = RequireSection(db, tr, a.Handle, OpenMode.ForWrite);

            var beforeAbove = sec.Height(SectionHeight.HeightAboveSectionLine);
            var beforeBelow = sec.Height(SectionHeight.HeightBelowSectionLine);
            var beforeElev = sec.Elevation;

            if (a.Above is not null) sec.SetHeight(SectionHeight.HeightAboveSectionLine, a.Above.Value);
            if (a.Below is not null) sec.SetHeight(SectionHeight.HeightBelowSectionLine, a.Below.Value);
            if (a.Elevation is not null) sec.Elevation = a.Elevation.Value;

            var above = sec.Height(SectionHeight.HeightAboveSectionLine);
            var below = sec.Height(SectionHeight.HeightBelowSectionLine);
            var elev = sec.Elevation;

            // Every one of these is a plain property, and every one can be assigned a value the
            // object then declines to keep. Read back rather than assume.
            if (a.Above is not null && Math.Abs(above - a.Above.Value) > 1e-9)
                throw new InvalidOperationException(
                    "above was set to " + a.Above.Value + " but reads back as " + above + ".");
            if (a.Below is not null && Math.Abs(below - a.Below.Value) > 1e-9)
                throw new InvalidOperationException(
                    "below was set to " + a.Below.Value + " but reads back as " + below + ".");
            if (a.Elevation is not null && Math.Abs(elev - a.Elevation.Value) > 1e-9)
                throw new InvalidOperationException(
                    "elevation was set to " + a.Elevation.Value + " but reads back as " + elev + ".");

            return Wrap(new
            {
                handle = a.Handle,
                aboveBefore = beforeAbove,
                above,
                belowBefore = beforeBelow,
                below,
                elevationBefore = beforeElev,
                elevation = elev,
                note = "The cutting plane reaches " + above + " above its line and " + below +
                       " below, sitting at elevation " + elev + ". These only bite in the BOUNDARY " +
                       "and VOLUME states - a plane-state section is unbounded and ignores them, " +
                       "which is why a height that appears to do nothing usually means the state " +
                       "is still plane.",
            });
        });

    // ─────────── drawing the result ───────────

    private static Task<ToolDispatchResult> GenerateSection(JsonObject args, CancellationToken ct) =>
        Run("acad.sections3d.generate_section", args, ct, (doc, db, tr) =>
        {
            var a = Read<SectionGenerateArgsDto>(args);
            var sec = RequireSection(db, tr, a.Handle, OpenMode.ForWrite);
            if (a.SourceHandles is null || a.SourceHandles.Count == 0)
                throw new ArgumentException(
                    "sourceHandles is required: which solids to cut. A section plane does not " +
                    "know what it crosses - it is a plane, not a query - so the things to be cut " +
                    "have to be named.");

            var kind = (a.Kind ?? "2d").Trim().ToLowerInvariant();
            var sectionType = kind switch
            {
                "2d" => SectionType.Section2d,
                "3d" => SectionType.Section3d,
                "live" => SectionType.LiveSection,
                _ => throw new ArgumentException(
                    "kind must be 2d, 3d or live. 2D gives flat curves you can dimension and " +
                    "plot; 3D gives the cut model as solids; live gives what the live section " +
                    "display shows."),
            };
            // Section.Settings is an ObjectId, not the settings object - it has to be opened
            // in the transaction like any other DBObject.
            var settings = (SectionSettings)tr.GetObject(sec.Settings, OpenMode.ForWrite);
            settings.CurrentSectionType = sectionType;

            var ms = (BlockTableRecord)tr.GetObject(
                ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite);

            var made = new List<EntityHandle>();
            double totalLength = 0;
            int cut = 0, back = 0, fore = 0, tang = 0, curveCount = 0;

            foreach (var h in a.SourceHandles)
            {
                var src = (Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForRead);
                Array intersect, background, foreground, curveTangency, unused;
                try
                {
                    // Five output arrays, which is why 2D, 3D and block generation are ONE call
                    // driven by CurrentSectionType rather than three separate methods.
                    sec.GenerateSectionGeometry(src, out intersect, out background,
                                                out foreground, out curveTangency, out unused);
                }
                catch (AcadRt.Exception ex)
                {
                    throw new ArgumentException(
                        "AutoCAD refused to generate the section from " + h + " with " +
                        ex.ErrorStatus + ". A section plane that MISSES the solid entirely has " +
                        "nothing to cut - check that the plane crosses it, and that the state is " +
                        "not clipping it away.");
                }

                void Take(Array arr, ref int counter)
                {
                    if (arr is null) return;
                    foreach (var o in arr)
                    {
                        if (o is not Entity e) continue;
                        counter++;
                        ms.AppendEntity(e);
                        tr.AddNewlyCreatedDBObject(e, true);
                        if (!string.IsNullOrWhiteSpace(a.Layer)) e.Layer = a.Layer!;
                        made.Add(AcadEnv.ToHandle(e));
                        if (e is Curve c)
                        {
                            curveCount++;
                            try
                            {
                                totalLength += Math.Abs(
                                    c.GetDistanceAtParameter(c.EndParam) -
                                    c.GetDistanceAtParameter(c.StartParam));
                            }
                            catch { /* a degenerate curve should not sink the whole result */ }
                        }
                    }
                }

                Take(intersect, ref cut);
                if (a.IncludeBackground == true) Take(background, ref back);
                if (a.IncludeForeground == true) Take(foreground, ref fore);
                if (a.IncludeTangency == true) Take(curveTangency, ref tang);
            }

            if (made.Count == 0)
                throw new InvalidOperationException(
                    "The section generated no geometry, though AutoCAD reported no error. That " +
                    "means the plane did not cross any of the solids named - a plane placed clear " +
                    "of the model produces an empty result rather than a complaint.");

            return Wrap(new
            {
                entities = made,
                count = made.Count,
                kind,
                cutCurves = cut,
                backgroundCurves = back,
                foregroundCurves = fore,
                tangencyCurves = tang,
                totalCurveLength = totalLength,
                note = "The CUT geometry is what the plane passes through - " + cut + " entities " +
                       "here, total length " + totalLength + ". A plane through the middle of a " +
                       "100 cube cuts a 100 by 100 square, so that length comes to 400, which " +
                       "makes the result checkable on paper. Background, foreground and tangency " +
                       "curves are what lies beyond, in front of and along the silhouette; they " +
                       "are off by default because a section drawing usually wants only the cut.",
            });
        });
}
