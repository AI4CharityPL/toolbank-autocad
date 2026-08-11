// AutoCAD plugin handlers for the acad-lights category (roadmap 6.1, second tranche).
// Registered under "acad.lights.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern), 26 (traps).
//
// MEASURED, and three of these cost a probe round each:
//
//   Light.LightType is a GraphicsInterface.DrawableType - the right name in the wrong namespace
//   if you look for it beside Light. Its light members are PointLight, SpotLight, DistantLight
//   and WebLight.
//
//   It is Light.IsOn, not Light.On, which does not exist.
//
//   HotspotAngle and FalloffAngle are READ-ONLY. They are set together through
//   SetHotspotAndFalloff(hotspot, falloff) and there is no other route - which also means the
//   pair is always consistent, since AutoCAD will not let the cone invert.
//
//   ShadowMapSize, ShadowSoftness and GlyphDisplay do NOT exist on Light.
//
//   HasTarget does NOT stick when set before the light is in the database - it reads
//   back false, while plain fields like Position and the cone angles survive. So a
//   spot or distant light is persisted FIRST and aimed afterwards.
//
// create_web_light is not here: a web light is defined by an .ies photometric file, and without
// one there is nothing to verify - the same reason texture maps waited in acad-materials.

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
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using AcadRt = Autodesk.AutoCAD.Runtime;
using GI = Autodesk.AutoCAD.GraphicsInterface;

namespace AcadMcp.Plugin.Tools;

internal static class LightsPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.lights.create_point_light",   CreatePointLight);
        host.Register("acad.lights.create_spot_light",    CreateSpotLight);
        host.Register("acad.lights.create_distant_light", CreateDistantLight);
        host.Register("acad.lights.list_lights",          ListLights);
        host.Register("acad.lights.set_light_properties", SetLightProperties);
        host.Register("acad.lights.delete_light",         DeleteLight);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    private static IEnumerable<Light> AllLights(Database db, Transaction tr, OpenMode mode)
    {
        var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
        var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        foreach (ObjectId id in ms)
        {
            if (id.IsErased) continue;                      // rule 26 §8
            if (tr.GetObject(id, mode) is Light l) yield return l;
        }
    }

    private static Light RequireLight(Database db, Transaction tr, string? name, OpenMode mode)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("name is required: which light.");
        foreach (var l in AllLights(db, tr, mode))
            if (string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)) return l;
        throw new ArgumentException(
            "No light called '" + name + "' in model space. list_lights shows what is there.");
    }

    private static object Describe(Light l) => new
    {
        name = l.Name,
        // MEASURED: LightType is a GraphicsInterface.DrawableType, not a DatabaseServices enum.
        type = l.LightType.ToString(),
        // MEASURED: it is IsOn, not On.
        on = l.IsOn,
        position = AcadEnv.FromPoint3d(l.Position),
        target = l.HasTarget ? AcadEnv.FromPoint3d(l.TargetLocation) : null,
        hasTarget = l.HasTarget,
        intensity = l.Intensity,
        // MEASURED: read-only, and set only as a pair through SetHotspotAndFalloff.
        hotspotAngle = l.HotspotAngle,
        falloffAngle = l.FalloffAngle,
        color = new { r = (int)l.LightColor.Red, g = (int)l.LightColor.Green, b = (int)l.LightColor.Blue },
        handle = l.Handle.ToString(),
        layer = l.Layer,
    };

    private static void RequireUniqueName(Database db, Transaction tr, string name)
    {
        foreach (var l in AllLights(db, tr, OpenMode.ForRead))
            if (string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    "A light called '" + name + "' already exists. Lights are addressed by name " +
                    "here, so two with the same one could not be told apart.");
    }

    private static Light NewLight(Database db, Transaction tr, LightArgsDto a, GI.DrawableType type)
    {
        if (string.IsNullOrWhiteSpace(a.Name))
            throw new ArgumentException("name is required: what to call the light.");
        if (a.Intensity is not null && a.Intensity < 0)
            throw new ArgumentException("intensity cannot be negative.");
        RequireUniqueName(db, tr, a.Name!);

        var l = new Light { Name = a.Name!, LightType = type };
        if (a.Position is not null) l.Position = AcadEnv.ToPoint3d(a.Position);
        if (a.Intensity is not null) l.Intensity = a.Intensity.Value;
        if (a.On is not null) l.IsOn = a.On.Value;
        if (a.Color is not null)
            l.LightColor = Color.FromRgb((byte)a.Color.R, (byte)a.Color.G, (byte)a.Color.B);
        return l;
    }

    // ─────────── the three kinds ───────────

    private static Task<ToolDispatchResult> CreatePointLight(JsonObject args, CancellationToken ct) =>
        Run("acad.lights.create_point_light", args, ct, (doc, db, tr) =>
        {
            var a = Read<LightArgsDto>(args);
            if (a.Position is null)
                throw new ArgumentException("position is required: where the lamp sits.");
            var l = NewLight(db, tr, a, GI.DrawableType.PointLight);
            var handle = AcadEnv.Persist(db, tr, l, a.Layer);

            var back = RequireLight(db, tr, a.Name, OpenMode.ForRead);
            return Wrap(new
            {
                entity = handle,
                light = Describe(back),
                note = "A point light throws light in EVERY direction from one spot, like a bare " +
                       "bulb, so it has no target and no cone - create_spot_light is the one that " +
                       "aims. Read back by name after creation, through a search of model space " +
                       "rather than the object just written.",
            });
        });

    private static Task<ToolDispatchResult> CreateSpotLight(JsonObject args, CancellationToken ct) =>
        Run("acad.lights.create_spot_light", args, ct, (doc, db, tr) =>
        {
            var a = Read<LightArgsDto>(args);
            if (a.Position is null || a.Target is null)
                throw new ArgumentException(
                    "position and target are both required: a spot light is defined by where it " +
                    "is and what it points at.");
            double hot = a.HotspotAngle ?? 0.35;      // ~20 degrees
            double fall = a.FalloffAngle ?? 0.79;     // ~45 degrees
            if (hot <= 0 || fall <= 0)
                throw new ArgumentException("hotspotAngle and falloffAngle must be greater than zero.");
            if (hot > fall)
                throw new ArgumentException(
                    "hotspotAngle must not exceed falloffAngle: the hotspot is the bright inner " +
                    "cone and the falloff the dimmer outer one, so an inner cone wider than the " +
                    "outer is not a shape a light can have.");

            var l = NewLight(db, tr, a, GI.DrawableType.SpotLight);

            // MEASURED: HasTarget does NOT stick when set before the light is in the database -
            // it reads back false, while plain fields like Position and the cone angles survive.
            // So the light is persisted FIRST and aimed afterwards. Same family as
            // GeoLocationData.CoordinateSystem needing PostToDb (rule 26 §16).
            var handle = AcadEnv.Persist(db, tr, l, a.Layer);
            l.HasTarget = true;
            l.TargetLocation = AcadEnv.ToPoint3d(a.Target);
            // HotspotAngle and FalloffAngle are READ-ONLY; this is the only route, and taking them
            // as a pair is why the cone can never end up inverted.
            l.SetHotspotAndFalloff(hot, fall);
            if (!l.HasTarget)
                throw new InvalidOperationException(
                    "The spot light does not read back as having a target after being aimed.");
            var back = RequireLight(db, tr, a.Name, OpenMode.ForRead);
            if (Math.Abs(back.HotspotAngle - hot) > 1e-6 || Math.Abs(back.FalloffAngle - fall) > 1e-6)
                throw new InvalidOperationException(
                    "The cone reads back as hotspot " + back.HotspotAngle + " and falloff " +
                    back.FalloffAngle + " rather than " + hot + " and " + fall + ".");

            return Wrap(new
            {
                entity = handle,
                light = Describe(back),
                note = "Angles are in RADIANS and describe two cones: the HOTSPOT is the bright " +
                       "inner one and the FALLOFF the dimmer outer edge, so the hotspot is always " +
                       "the smaller. They are set together through SetHotspotAndFalloff because " +
                       "both properties are read-only on their own, which is also what stops the " +
                       "cone being inverted. Defaults are about 20 and 45 degrees.",
            });
        });

    private static Task<ToolDispatchResult> CreateDistantLight(JsonObject args, CancellationToken ct) =>
        Run("acad.lights.create_distant_light", args, ct, (doc, db, tr) =>
        {
            var a = Read<LightArgsDto>(args);
            if (a.Direction is null && (a.Position is null || a.Target is null))
                throw new ArgumentException(
                    "Give either direction, or position and target - a distant light is defined " +
                    "by the DIRECTION its rays travel, and the two points are just another way of " +
                    "expressing that.");

            var l = NewLight(db, tr, a, GI.DrawableType.DistantLight);
            Point3d from, to;
            if (a.Direction is not null)
            {
                var d = AcadEnv.ToVector3d(a.Direction);
                if (d.Length < 1e-12)
                    throw new ArgumentException("direction cannot be the zero vector.");
                d = d.GetNormal();
                // A distant light has no real position - only a direction - so a point and a
                // target are synthesised from it, which is how AutoCAD itself stores one.
                from = Point3d.Origin - d * 100.0;
                to = Point3d.Origin;
            }
            else
            {
                from = AcadEnv.ToPoint3d(a.Position!);
                to = AcadEnv.ToPoint3d(a.Target!);
                if (from.DistanceTo(to) < 1e-12)
                    throw new ArgumentException(
                        "position and target are the same point, so they give no direction.");
            }
            l.Position = from;

            // Persisted before aiming, for the same reason as the spot light above.
            var handle = AcadEnv.Persist(db, tr, l, a.Layer);
            l.HasTarget = true;
            l.TargetLocation = to;
            if (!l.HasTarget)
                throw new InvalidOperationException(
                    "The distant light does not read back as having a target after being aimed.");
            var back = RequireLight(db, tr, a.Name, OpenMode.ForRead);
            return Wrap(new
            {
                entity = handle,
                light = Describe(back),
                direction = AcadEnv.FromPoint3d(new Point3d(
                    (to - from).GetNormal().X, (to - from).GetNormal().Y, (to - from).GetNormal().Z)),
                note = "A distant light is the sun-like one: parallel rays of the same strength " +
                       "everywhere, so only their DIRECTION matters and its position is a " +
                       "convention rather than a place. Give a direction, or two points to define " +
                       "one. Because the rays never weaken with distance, moving a distant light " +
                       "changes nothing - only turning it changes the picture.",
            });
        });

    // ─────────── reading and changing ───────────

    private static Task<ToolDispatchResult> ListLights(JsonObject args, CancellationToken ct) =>
        Run("acad.lights.list_lights", args, ct, (doc, db, tr) =>
        {
            var found = new List<object>();
            int on = 0;
            foreach (var l in AllLights(db, tr, OpenMode.ForRead))
            {
                found.Add(Describe(l));
                if (l.IsOn) on++;
            }
            return Wrap(new
            {
                count = found.Count,
                onCount = on,
                lights = found,
                note = "Model space only. `on` is read from Light.IsOn - there is no Light.On in " +
                       "the API - and a light that is off is still listed, because it is still in " +
                       "the drawing and is usually what you were looking for. hotspotAngle and " +
                       "falloffAngle are in radians and mean nothing for a point or distant light.",
            });
        });

    private static Task<ToolDispatchResult> SetLightProperties(JsonObject args, CancellationToken ct) =>
        Run("acad.lights.set_light_properties", args, ct, (doc, db, tr) =>
        {
            var a = Read<LightArgsDto>(args);
            if (a.On is null && a.Intensity is null && a.Color is null && a.Position is null
                && a.Target is null && a.HotspotAngle is null && a.FalloffAngle is null)
                throw new ArgumentException(
                    "Nothing to change. Give at least one of on, intensity, color, position, " +
                    "target, hotspotAngle or falloffAngle.");
            if (a.Intensity is not null && a.Intensity < 0)
                throw new ArgumentException("intensity cannot be negative.");

            var l = RequireLight(db, tr, a.Name, OpenMode.ForWrite);
            var before = Describe(l);
            var changed = new List<string>();

            if (a.On is not null) { l.IsOn = a.On.Value; changed.Add("on"); }
            if (a.Intensity is not null) { l.Intensity = a.Intensity.Value; changed.Add("intensity"); }
            if (a.Color is not null)
            {
                l.LightColor = Color.FromRgb((byte)a.Color.R, (byte)a.Color.G, (byte)a.Color.B);
                changed.Add("color");
            }
            if (a.Position is not null) { l.Position = AcadEnv.ToPoint3d(a.Position); changed.Add("position"); }
            if (a.Target is not null)
            {
                if (!l.HasTarget)
                    throw new ArgumentException(
                        "A " + l.LightType + " has no target to move - only a spot or distant " +
                        "light is aimed at anything.");
                l.TargetLocation = AcadEnv.ToPoint3d(a.Target);
                changed.Add("target");
            }
            if (a.HotspotAngle is not null || a.FalloffAngle is not null)
            {
                // The pair is read-only individually, so BOTH have to be supplied to the setter -
                // whichever was not given is taken from what is already there.
                double hot = a.HotspotAngle ?? l.HotspotAngle;
                double fall = a.FalloffAngle ?? l.FalloffAngle;
                if (hot > fall)
                    throw new ArgumentException(
                        "hotspotAngle " + hot + " exceeds falloffAngle " + fall + ". The hotspot " +
                        "is the inner cone, so it cannot be the wider of the two - if you are " +
                        "changing only one, check it against the value already set.");
                l.SetHotspotAndFalloff(hot, fall);
                changed.Add("cone");
            }

            var back = RequireLight(db, tr, a.Name, OpenMode.ForRead);
            return Wrap(new
            {
                changed,
                before,
                light = Describe(back),
                note = "Read back by name afterwards rather than echoed. Only the properties named " +
                       "are touched, and the previous values are reported so a change can be " +
                       "undone. Changing one cone angle carries the other over from what was " +
                       "already set, because AutoCAD only accepts the pair together.",
            });
        });

    private static Task<ToolDispatchResult> DeleteLight(JsonObject args, CancellationToken ct) =>
        Run("acad.lights.delete_light", args, ct, (doc, db, tr) =>
        {
            var a = Read<LightArgsDto>(args);
            var l = RequireLight(db, tr, a.Name, OpenMode.ForWrite);
            var name = l.Name;
            var was = Describe(l);
            l.Erase();

            int left = 0;
            foreach (var other in AllLights(db, tr, OpenMode.ForRead))
                if (string.Equals(other.Name, name, StringComparison.OrdinalIgnoreCase)) left++;
            if (left > 0)
                throw new InvalidOperationException(
                    "A light called '" + name + "' still reads back after being erased.");

            return Wrap(new
            {
                name,
                deleted = true,
                previous = was,
                note = "Erased from the drawing. The previous settings are reported so the light " +
                       "can be recreated if this was a mistake. Deleting a light changes what a " +
                       "render looks like but nothing about the geometry.",
            });
        });
}
