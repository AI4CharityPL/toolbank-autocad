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
using AcadColor = Autodesk.AutoCAD.Colors.Color;
using ColorMethod = Autodesk.AutoCAD.Colors.ColorMethod;

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
        host.Register("acad.sections3d.create_section_orthographic", CreateSectionOrthographic);
        host.Register("acad.sections3d.generate_section_block",      GenerateSectionBlock);
        host.Register("acad.sections3d.set_section_settings",        SetSectionSettings);
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

    /// Builds the Section and checks the plane it landed on is the plane that was asked for.
    ///
    /// The second constructor argument is `verticalDir` - which way is UP in the section - and NOT
    /// the plane's normal. The cut plane is the one CONTAINING the section line and this vector,
    /// so the normal falls out as line x up and cannot be given directly: Section.Normal is
    /// read-only.
    ///
    /// This is measured, not assumed, and getting it wrong is silent. Passing a horizontal vector
    /// here (the first version passed the intended normal) makes the plane contain the plan line
    /// AND a horizontal direction - that is the XY plane, so every "vertical" section came back as
    /// a horizontal cut at z=0, and the numbers still looked right on a cube because a cube's cut
    /// and its silhouette are the same square. See rule 26 §14.
    private static Section MakeSection(Point3dCollection pts, Vector3d up, out Vector3d along)
    {
        along = pts[1] - pts[0];
        if (along.Length < 1e-12)
            throw new ArgumentException(
                "The first two vertices are the same point, so they give no direction to cut along.");
        if (up.Length < 1e-12)
            throw new ArgumentException("verticalDirection cannot be the zero vector.");
        up = up.GetNormal();
        if (along.GetNormal().CrossProduct(up).Length < 1e-9)
            throw new ArgumentException(
                "verticalDirection is parallel to the section line, so the two do not define a " +
                "plane. Up has to point across the line, not along it - the default (0,0,1) is " +
                "what a vertical building section wants.");
        try
        {
            // The CONSTRUCTOR, not a factory: there is no Section.CreateSectionPlane, and
            // Boundary is read-only, so the cut line can only go in here.
            return new Section(pts, up);
        }
        catch (AcadRt.Exception ex)
        {
            throw new ArgumentException(
                "AutoCAD refused the section plane with " + ex.ErrorStatus + ".");
        }
    }

    /// The plane the object ended up with has to be the plane that was asked for, and the only way
    /// to know is the normal it worked out for itself: it must stand at right angles BOTH to the
    /// section line and to up. When the vertical direction was wrong this came back along Z for a
    /// line drawn in plan, and nothing else in the result showed it.
    private static Vector3d AssertPlaneAsAsked(Section sec, Vector3d along, Vector3d up)
    {
        var n = sec.Normal;
        if (n.Length < 1e-12 || Math.Abs(n.GetNormal().DotProduct(along.GetNormal())) > 1e-6
                             || Math.Abs(n.GetNormal().DotProduct(up.GetNormal())) > 1e-6)
            throw new InvalidOperationException(
                "The section plane came back with normal (" + n.X + ", " + n.Y + ", " + n.Z +
                "), which does not stand square to both the section line and the vertical " +
                "direction - so the plane it will cut on is not the one that was asked for.");
        return n;
    }

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

            Vector3d up = a.VerticalDirection is not null
                ? AcadEnv.ToVector3d(a.VerticalDirection)
                : Vector3d.ZAxis;
            var sec = MakeSection(pts, up, out var along);
            up = up.GetNormal();

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

            var n = AssertPlaneAsAsked(sec, along, up);

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
            var sectionType = ParseKind(a.Kind);
            // Section.Settings is an ObjectId, not the settings object - it has to be opened
            // in the transaction like any other DBObject.
            var settings = (SectionSettings)tr.GetObject(sec.Settings, OpenMode.ForWrite);
            settings.CurrentSectionType = sectionType;

            var ms = (BlockTableRecord)tr.GetObject(
                ((BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead))[BlockTableRecord.ModelSpace],
                OpenMode.ForWrite);

            var g = Generate(db, tr, sec, ms, a.SourceHandles, a.Layer, a.IncludeBackground == true,
                             a.IncludeForeground == true, a.IncludeTangency == true);

            return Wrap(new
            {
                entities = g.Made,
                count = g.Made.Count,
                kind,
                cutCurves = g.Cut,
                backgroundCurves = g.Background,
                foregroundCurves = g.Foreground,
                tangencyCurves = g.Tangency,
                totalCurveLength = g.TotalLength,
                note = "The CUT geometry is what the plane passes through - " + g.Cut + " entities " +
                       "here, total length " + g.TotalLength + ". A plane through the middle of a " +
                       "100 cube cuts a 100 by 100 square, so that length comes to 400, which " +
                       "makes the result checkable on paper. Background, foreground and tangency " +
                       "curves are what lies beyond, in front of and along the silhouette; they " +
                       "are off by default because a section drawing usually wants only the cut. " +
                       "generate_section_block puts the same geometry into a block instead of " +
                       "leaving it loose in the drawing.",
            });
        });

    // ─────────── orthographic placement, done by arithmetic ───────────

    /// The six standard views, each as (which way the section LOOKS, which way is UP in it).
    /// The plane then contains the line and up, and the line is laid across the model's extents.
    private static (Vector3d view, Vector3d up) Orthographic(string name) =>
        name.Trim().ToLowerInvariant() switch
        {
            // A FRONT elevation is drawn standing in front of the model looking towards the back,
            // so the section looks along +Y. Back is its opposite, and so on for the other pairs.
            "front" => (Vector3d.YAxis, Vector3d.ZAxis),
            "back" => (-Vector3d.YAxis, Vector3d.ZAxis),
            "left" => (Vector3d.XAxis, Vector3d.ZAxis),
            "right" => (-Vector3d.XAxis, Vector3d.ZAxis),
            // Looking DOWN is the plan cut, and its plane is horizontal - so up in the section is
            // a horizontal direction, which is exactly what verticalDirection is for.
            "top" => (-Vector3d.ZAxis, Vector3d.YAxis),
            "bottom" => (Vector3d.ZAxis, Vector3d.YAxis),
            _ => throw new ArgumentException(
                "orientation must be front, back, left, right, top or bottom. Front and back look " +
                "along Y and cut the model's width; left and right look along X; top and bottom " +
                "are the horizontal cut a plan is made from."),
        };

    private static Task<ToolDispatchResult> CreateSectionOrthographic(JsonObject args, CancellationToken ct) =>
        Run("acad.sections3d.create_section_orthographic", args, ct, (doc, db, tr) =>
        {
            var a = Read<SectionOrthographicArgsDto>(args);
            if (string.IsNullOrWhiteSpace(a.Orientation))
                throw new ArgumentException(
                    "orientation is required: front, back, left, right, top or bottom.");
            var (view, up) = Orthographic(a.Orientation!);

            // AutoCAD has NO API for this - neither Section.CreateOrthographic nor SetOrthographic
            // exists in either form - so the plane is placed by arithmetic over the model's own
            // extents. That is honest work rather than a wrapper, and it makes the result exactly
            // predictable, which is what the verification checks against.
            var (min, max, counted) = ExtentsOf(db, tr, a.SourceHandles);
            if (counted == 0)
                throw new ArgumentException(
                    "Nothing to section: no source solid was named and model space holds no " +
                    "entity with extents, so there is no model to place a plane through.");

            var centre = new Point3d((min.X + max.X) / 2, (min.Y + max.Y) / 2, (min.Z + max.Z) / 2);
            var offset = a.Offset ?? 0.0;
            // The plane must sit ACROSS the view direction, so the offset moves along it.
            var at = centre + view.GetNormal() * offset;

            // Run the section line the long way across the model so nothing escapes the cut, with
            // a margin either end - a line that merely reaches the extents can miss on rounding.
            var span = Math.Max(max.X - min.X, Math.Max(max.Y - min.Y, max.Z - min.Z));
            var margin = span > 1e-9 ? span : 1.0;
            // MEASURED, not derived: AutoCAD works the normal out as up x along, not along x up.
            // The two differ only in sign, which is exactly the kind of thing that survives a test
            // asserting |y| = 1 - and it did, until the post-condition below caught it. Since
            // up x (view x up) = view for perpendicular vectors, the line to lay down is view x up.
            var along = view.CrossProduct(up).GetNormal();
            if (along.Length < 1e-12)
                throw new InvalidOperationException("The orientation table is inconsistent.");

            var pts = new Point3dCollection { at - along * margin, at + along * margin };
            var sec = MakeSection(pts, up, out var alongOut);
            sec.State = ParseState(a.State);
            if (a.LiveSection == true) sec.IsLiveSectionEnabled = true;

            var handle = AcadEnv.Persist(db, tr, sec, a.Layer);
            var n = AssertPlaneAsAsked(sec, alongOut, up);
            // And the whole point of "orthographic": the plane must LOOK the way the name says.
            if (n.GetNormal().DotProduct(view.GetNormal()) < 1 - 1e-6)
                throw new InvalidOperationException(
                    "A " + a.Orientation + " section should look along (" + view.X + ", " + view.Y +
                    ", " + view.Z + ") but the plane came back looking along (" + n.X + ", " + n.Y +
                    ", " + n.Z + ").");

            return Wrap(new
            {
                entity = handle,
                orientation = a.Orientation!.Trim().ToLowerInvariant(),
                vertices = sec.Boundary?.Count ?? 0,
                state = StateName(sec.State),
                liveSection = sec.IsLiveSectionEnabled,
                normal = AcadEnv.FromPoint3d(new Point3d(n.X, n.Y, n.Z)),
                verticalDirection = AcadEnv.FromPoint3d(new Point3d(up.X, up.Y, up.Z)),
                center = AcadEnv.FromPoint3d(at),
                extentsMin = AcadEnv.FromPoint3d(min),
                extentsMax = AcadEnv.FromPoint3d(max),
                sourcesMeasured = counted,
                note = "Placed through the middle of the model's extents, looking the way the name " +
                       "says: FRONT and BACK cut across the width, LEFT and RIGHT across the depth, " +
                       "TOP and BOTTOM are the horizontal cut a plan is made from. offset shifts it " +
                       "off centre along the direction it looks, which is how you get a cut at a " +
                       "particular floor rather than half way up. AutoCAD has no API for this, so " +
                       "the plane is placed by arithmetic here - the extents it used are reported " +
                       "above so the position can be checked. It still cuts nothing: use " +
                       "generate_section to draw the result.",
            });
        });

    /// Combined extents of the named entities, or of everything in model space that has any.
    private static (Point3d Min, Point3d Max, int Counted) ExtentsOf(
        Database db, Transaction tr, List<string>? handles)
    {
        double mnx = double.MaxValue, mny = double.MaxValue, mnz = double.MaxValue;
        double mxx = double.MinValue, mxy = double.MinValue, mxz = double.MinValue;
        int counted = 0;

        void Add(Entity e)
        {
            Extents3d ext;
            try { ext = e.GeometricExtents; }
            catch { return; }   // a point, an empty block: no extents, and that is not an error
            counted++;
            mnx = Math.Min(mnx, ext.MinPoint.X); mny = Math.Min(mny, ext.MinPoint.Y);
            mnz = Math.Min(mnz, ext.MinPoint.Z); mxx = Math.Max(mxx, ext.MaxPoint.X);
            mxy = Math.Max(mxy, ext.MaxPoint.Y); mxz = Math.Max(mxz, ext.MaxPoint.Z);
        }

        if (handles is not null && handles.Count > 0)
        {
            foreach (var h in handles)
                Add((Entity)tr.GetObject(AcadEnv.ResolveHandle(db, h), OpenMode.ForRead));
        }
        else
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            foreach (ObjectId id in ms)
            {
                if (id.IsErased) continue;                      // rule 26 §8
                var o = tr.GetObject(id, OpenMode.ForRead);
                // Section planes are excluded on purpose: they are indicator geometry that can
                // reach far outside the model, and letting them into the extents would move the
                // next orthographic plane away from the thing being sectioned.
                if (o is Section || o is not Entity e) continue;
                Add(e);
            }
        }

        return counted == 0
            ? (Point3d.Origin, Point3d.Origin, 0)
            : (new Point3d(mnx, mny, mnz), new Point3d(mxx, mxy, mxz), counted);
    }

    // ─────────── the same geometry, packed into a block ───────────

    private static Task<ToolDispatchResult> GenerateSectionBlock(JsonObject args, CancellationToken ct) =>
        Run("acad.sections3d.generate_section_block", args, ct, (doc, db, tr) =>
        {
            var a = Read<SectionBlockArgsDto>(args);
            var sec = RequireSection(db, tr, a.Handle, OpenMode.ForWrite);
            if (a.SourceHandles is null || a.SourceHandles.Count == 0)
                throw new ArgumentException(
                    "sourceHandles is required: which solids to cut. A section plane does not " +
                    "know what it crosses - it is a plane, not a query - so the things to be cut " +
                    "have to be named.");

            var settings = (SectionSettings)tr.GetObject(sec.Settings, OpenMode.ForWrite);
            settings.CurrentSectionType = ParseKind(a.Kind);

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForWrite);
            var name = string.IsNullOrWhiteSpace(a.BlockName)
                ? "SECTION_" + a.Handle
                : a.BlockName!.Trim();
            if (bt.Has(name))
                throw new ArgumentException(
                    "A block called '" + name + "' already exists. Give a different blockName - " +
                    "overwriting a block definition would silently change every insert of it.");

            // SectionGeneration has DestinationNewBlock, but GenerateSectionGeometry hands back
            // loose entities whatever it is set to - the destination flags are what the
            // SECTIONPLANETOBLOCK command reads. So the block is built here: a definition holding
            // the generated curves, and one reference to it in model space.
            var btr = new BlockTableRecord { Name = name, Origin = Point3d.Origin };
            bt.Add(btr);
            tr.AddNewlyCreatedDBObject(btr, true);

            var g = Generate(db, tr, sec, btr, a.SourceHandles, a.Layer,
                             a.IncludeBackground == true, a.IncludeForeground == true,
                             a.IncludeTangency == true);

            var at = a.InsertionPoint is not null ? AcadEnv.ToPoint3d(a.InsertionPoint) : Point3d.Origin;
            var reference = new BlockReference(at, btr.ObjectId);
            var refHandle = AcadEnv.Persist(db, tr, reference, a.Layer);

            // A block definition that came out empty would still insert, and the reference would
            // look like a success while drawing nothing at all.
            int inBlock = 0;
            foreach (ObjectId _ in btr) inBlock++;
            if (inBlock != g.Made.Count)
                throw new InvalidOperationException(
                    "The block definition holds " + inBlock + " entities but the section " +
                    "generated " + g.Made.Count + ".");

            return Wrap(new
            {
                entity = refHandle,
                blockName = name,
                entitiesInBlock = inBlock,
                insertionPoint = AcadEnv.FromPoint3d(at),
                cutCurves = g.Cut,
                backgroundCurves = g.Background,
                foregroundCurves = g.Foreground,
                tangencyCurves = g.Tangency,
                totalCurveLength = g.TotalLength,
                note = "The same geometry generate_section would leave loose in the drawing, put " +
                       "into a block definition with one reference to it - which is what " +
                       "SECTIONPLANETOBLOCK does, and what you want when the section is to be moved, " +
                       "copied onto a sheet or scaled as one thing. The curves are inside the block, " +
                       "so they will not show up in a model-space selection. The source solids are " +
                       "untouched, exactly as with generate_section.",
            });
        });

    // ─────────── how the section is drawn ───────────

    private static SectionType ParseKind(string? kind) =>
        (kind ?? "2d").Trim().ToLowerInvariant() switch
        {
            "2d" => SectionType.Section2d,
            "3d" => SectionType.Section3d,
            "live" => SectionType.LiveSection,
            _ => throw new ArgumentException(
                "kind must be 2d, 3d or live. 2D gives flat curves you can dimension and plot; " +
                "3D gives the cut model as solids; live gives what the live section display shows."),
        };

    private static SectionGeometry ParsePart(string? part) =>
        (part ?? "").Trim().ToLowerInvariant() switch
        {
            "intersectionboundary" or "boundary" or "cut" => SectionGeometry.IntersectionBoundary,
            "intersectionfill" or "fill" => SectionGeometry.IntersectionFill,
            "background" => SectionGeometry.BackgroundGeometry,
            "foreground" => SectionGeometry.ForegroundGeometry,
            _ => throw new ArgumentException(
                "part must be cut (the outline of the cut face), fill (the poche inside it), " +
                "background (what lies beyond the plane) or foreground (what lies in front of it). " +
                "There is no tangency part: SectionGeometry has no member for it, even though " +
                "GenerateSectionGeometry returns tangency curves."),
        };

    /// Which property can be set on which PART of which KIND of section.
    ///
    /// MEASURED, every cell of it - AutoCAD answers a combination it does not support with a bare
    /// eInvalidInput that names neither the field nor the reason, so without this the tool would
    /// pass an unexplained error to the caller for a request that was never going to work. The
    /// pattern turns out to make sense: the cut outline IS the 2D section and so cannot be hidden,
    /// division lines belong only to the 2D cut face, and hidden-line treatment only applies to
    /// what lies beyond or in front of the plane.
    private static void RequireApplies(string field, SectionType kind, SectionGeometry part)
    {
        // Nothing at all can be set on the live section: every field, every part, refused.
        if (kind == SectionType.LiveSection)
            throw new ArgumentException(
                "Nothing can be styled on the LIVE section - AutoCAD refuses every property on it, " +
                "which is measured rather than assumed. Use kind 2d or 3d; live sectioning is " +
                "turned on and off with set_live_section and takes its appearance from the model.");

        bool ok = field switch
        {
            "color" or "layer" or "linetypeScale" => true,          // every part of 2d and 3d
            "visible" => kind == SectionType.Section2d
                ? part != SectionGeometry.IntersectionBoundary       // the cut IS the 2D section
                : part != SectionGeometry.BackgroundGeometry,
            "divisionLines" => kind == SectionType.Section2d
                              && part == SectionGeometry.IntersectionBoundary,
            "hiddenLine" => kind == SectionType.Section2d
                            && (part == SectionGeometry.BackgroundGeometry
                                || part == SectionGeometry.ForegroundGeometry),
            _ => true,
        };
        if (ok) return;

        var where = field switch
        {
            "visible" => kind == SectionType.Section2d
                ? "In a 2D section the cut outline cannot be hidden - it IS the section. " +
                  "fill, background and foreground can."
                : "In a 3D section the background cannot be hidden. cut, fill and foreground can.",
            "divisionLines" => "Division lines exist only on the CUT of a 2D section.",
            "hiddenLine" => "Hidden-line treatment applies only to the BACKGROUND and FOREGROUND " +
                            "of a 2D section - what lies beyond and in front of the plane.",
            _ => "That combination is not supported.",
        };
        throw new ArgumentException(
            field + " cannot be set on the " + PartName(part) + " of a " +
            (kind == SectionType.Section2d ? "2d" : "3d") + " section. " + where);
    }

    private static string PartName(SectionGeometry g) => g switch
    {
        SectionGeometry.IntersectionBoundary => "cut",
        SectionGeometry.IntersectionFill => "fill",
        SectionGeometry.BackgroundGeometry => "background",
        SectionGeometry.ForegroundGeometry => "foreground",
        _ => g.ToString(),
    };

    private static Task<ToolDispatchResult> SetSectionSettings(JsonObject args, CancellationToken ct) =>
        Run("acad.sections3d.set_section_settings", args, ct, (doc, db, tr) =>
        {
            var a = Read<SectionSettingsArgsDto>(args);
            var sec = RequireSection(db, tr, a.Handle, OpenMode.ForWrite);
            var st = (SectionSettings)tr.GetObject(sec.Settings, OpenMode.ForWrite);
            var kind = ParseKind(a.Kind);
            var part = ParsePart(a.Part);

            if (a.Color is null && a.Layer is null && a.Visible is null && a.DivisionLines is null
                && a.HiddenLine is null && a.LinetypeScale is null && a.SourceObjects is null)
                throw new ArgumentException(
                    "Nothing to set. Give at least one of color, layer, visible, divisionLines, " +
                    "hiddenLine, linetypeScale or sourceObjects.");

            var changed = new List<string>();

            if (a.Color is not null)
            {
                RequireApplies("color", kind, part);
                st.SetColor(kind, part, AcadColor.FromColorIndex(ColorMethod.ByAci, (short)a.Color.Value));
                changed.Add("color");
            }
            if (a.Layer is not null)
            {
                RequireApplies("layer", kind, part);
                st.SetLayer(kind, part, a.Layer);
                changed.Add("layer");
            }
            if (a.Visible is not null)
            {
                RequireApplies("visible", kind, part);
                st.SetVisibility(kind, part, a.Visible.Value);
                changed.Add("visible");
            }
            if (a.DivisionLines is not null)
            {
                RequireApplies("divisionLines", kind, part);
                st.SetDivisionLines(kind, part, a.DivisionLines.Value);
                changed.Add("divisionLines");
            }
            if (a.HiddenLine is not null)
            {
                RequireApplies("hiddenLine", kind, part);
                st.SetHiddenLine(kind, part, a.HiddenLine.Value);
                changed.Add("hiddenLine");
            }
            if (a.LinetypeScale is not null)
            {
                RequireApplies("linetypeScale", kind, part);
                st.SetLinetypeScale(kind, part, a.LinetypeScale.Value);
                changed.Add("linetypeScale");
            }
            if (a.SourceObjects is not null)
            {
                RequireApplies("sourceObjects", kind, part);
                var ids = new ObjectIdCollection();
                foreach (var h in a.SourceObjects) ids.Add(AcadEnv.ResolveHandle(db, h));
                st.SetSourceObjects(kind, ids);
                changed.Add("sourceObjects");
            }

            // Everything is read back through the getters, which are named after the thing rather
            // than Get-something: Color(kind, part), not GetColor(kind, part). Without this a
            // setting the object quietly declined to keep would look identical to one it took.
            var readColor = st.Color(kind, part);
            var readIds = new ObjectIdCollection();
            st.GetSourceObjects(kind, readIds);

            return Wrap(new
            {
                handle = a.Handle,
                kind = (a.Kind ?? "2d").Trim().ToLowerInvariant(),
                part = (a.Part ?? "").Trim().ToLowerInvariant(),
                changed,
                color = readColor?.ColorIndex,
                layer = st.Layer(kind, part),
                visible = st.Visibility(kind, part),
                divisionLines = st.DivisionLines(kind, part),
                hiddenLine = st.HiddenLine(kind, part),
                linetypeScale = st.LinetypeScale(kind, part),
                // Reported but NOT settable: SetFaceTransparency and SetEdgeTransparency exist
                // and refuse every value, on every part, in every kind - measured across 0..255.
                // The getters work, so the current values are still worth handing back.
                faceTransparency = st.FaceTransparency(kind, part),
                edgeTransparency = st.EdgeTransparency(kind, part),
                sourceObjectCount = readIds.Count,
                note = "Every value above was READ BACK from the section after being written, not " +
                       "echoed from the request. Settings are per section TYPE and per PART, so 2d " +
                       "and 3d carry their own, and the cut outline, its fill, the background and " +
                       "the foreground are coloured separately - which is how a section reads as a " +
                       "drawing rather than a wireframe. These change what generate_section and " +
                       "generate_section_block produce next time; geometry already drawn keeps the " +
                       "settings it was drawn with.",
            });
        });

    // ─────────── the generation itself, shared by the loose and the block form ───────────

    private sealed class GenTally
    {
        public List<EntityHandle> Made { get; } = new();
        public double TotalLength;
        public int Cut, Background, Foreground, Tangency;
    }

    /// Runs GenerateSectionGeometry over every source and files the results into `target`, which
    /// is model space for generate_section and a fresh block definition for generate_section_block.
    private static GenTally Generate(Database db, Transaction tr, Section sec,
                                     BlockTableRecord target, IEnumerable<string> sourceHandles,
                                     string? layer, bool wantBackground, bool wantForeground,
                                     bool wantTangency)
    {
        var t = new GenTally();

        foreach (var h in sourceHandles)
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
                    target.AppendEntity(e);
                    tr.AddNewlyCreatedDBObject(e, true);
                    if (!string.IsNullOrWhiteSpace(layer)) e.Layer = layer!;
                    t.Made.Add(AcadEnv.ToHandle(e));
                    if (e is Curve c)
                    {
                        try
                        {
                            t.TotalLength += Math.Abs(
                                c.GetDistanceAtParameter(c.EndParam) -
                                c.GetDistanceAtParameter(c.StartParam));
                        }
                        catch { /* a degenerate curve should not sink the whole result */ }
                    }
                }
            }

            Take(intersect, ref t.Cut);
            if (wantBackground) Take(background, ref t.Background);
            if (wantForeground) Take(foreground, ref t.Foreground);
            if (wantTangency) Take(curveTangency, ref t.Tangency);
        }

        if (t.Made.Count == 0)
            throw new InvalidOperationException(
                "The section generated no geometry, though AutoCAD reported no error. That " +
                "means the plane did not cross any of the solids named - a plane placed clear " +
                "of the model produces an empty result rather than a complaint.");

        return t;
    }
}
