// Sun handlers, added to the acad-lights category (roadmap 6.1, third tranche).
// Registered under "acad.lights.<verb>"; everything runs on the UI thread.
//
// Rules: 10 (UI thread), 11 (transactions), 12 (error mapping), 19 (impl pattern), 26 (traps).
//
// MEASURED:
//
//   A SUN BELONGS TO A VIEWPORT, not to the drawing. Database.SunId and Database.GetSun() do not
//   exist; ViewportTableRecord.SunId does, and ViewportTableRecord.SetSun(sun) is how one is
//   attached. So there is one sun per viewport configuration, and the current one is reached
//   through Database.CurrentViewportTableRecordId.
//
//   Sun.IsOn, not Sun.On. Absent: Sun.Color, Sun.DaylightSavingsTime, Sun.SetDatabaseDefaults.
//
//   SkyParameters is a GraphicsInterface type with Illumination and Haze; it has no
//   Intensity, and Illumination is a BOOL (sky illumination on/off) rather than a level.
//   ShadowParameters has ShadowType and ShadowMapSize but no ShadowSoftness or ShadowsEnabled.
//
//   The sun is attached AFTER being configured for the same reason lights are aimed after being
//   persisted - see rule 26 §18.
//
// There is no set_render_environment tool: RenderEnvironment exposes only FogEnabled, FogColor
// and FogBackgroundEnabled, and has neither PostToDb nor GetRenderEnvironment, so there is no way
// to attach one to a drawing or read it back. A tool that could not be verified is not shipped.

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Plugin.Threading;
using AcadMcp.Shared;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcadRt = Autodesk.AutoCAD.Runtime;

namespace AcadMcp.Plugin.Tools;

internal static class SunPluginTools
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public static void Register(ToolHost host)
    {
        host.Register("acad.lights.get_sun_properties", GetSunProperties);
        host.Register("acad.lights.set_sun_properties", SetSunProperties);
    }

    private static T Read<T>(JsonObject args) => JsonSerializer.Deserialize<T>(args, Opts)
        ?? throw new ArgumentException($"Cannot deserialize args as {typeof(T).Name}");

    private static JsonObject Wrap(object dto) =>
        JsonSerializer.SerializeToNode(dto, Opts) as JsonObject ?? new JsonObject();

    private static Task<ToolDispatchResult> Run(string toolKey, JsonObject args, CancellationToken ct,
        Func<Document, Database, Transaction, JsonObject> work)
        => PluginToolRunner.RunWriteAsync(toolKey, ct, work);

    /// The sun of the CURRENT viewport configuration, or null when none has been made yet.
    /// A drawing does not have a sun until something attaches one.
    private static Sun? CurrentSun(Database db, Transaction tr, OpenMode mode)
    {
        var vtr = (ViewportTableRecord)tr.GetObject(db.CurrentViewportTableRecordId, OpenMode.ForRead);
        var id = vtr.SunId;
        return id.IsNull ? null : (Sun)tr.GetObject(id, mode);
    }

    private static object Describe(Sun s) => new
    {
        // MEASURED: it is IsOn, not On.
        on = s.IsOn,
        intensity = s.Intensity,
        dateTime = s.DateTime.ToString("yyyy-MM-dd HH:mm"),
        skyIllumination = s.SkyParameters.Illumination,
        haze = s.SkyParameters.Haze,
        shadowType = s.ShadowParameters.ShadowType.ToString(),
        shadowMapSize = s.ShadowParameters.ShadowMapSize,
        handle = s.Handle.ToString(),
    };

    private static Task<ToolDispatchResult> GetSunProperties(JsonObject args, CancellationToken ct) =>
        Run("acad.lights.get_sun_properties", args, ct, (doc, db, tr) =>
        {
            var s = CurrentSun(db, tr, OpenMode.ForRead);
            if (s is null)
                return Wrap(new
                {
                    hasSun = false,
                    note = "This viewport has no sun yet. set_sun_properties makes one - a drawing " +
                           "does not have a sun until something attaches it, and the absence is " +
                           "reported rather than answered with defaults that were never set.",
                });

            return Wrap(new
            {
                hasSun = true,
                sun = Describe(s),
                note = "A sun belongs to a VIEWPORT, not to the drawing - Database.SunId does not " +
                       "exist and ViewportTableRecord.SunId does - so this is the sun of the " +
                       "current viewport configuration and another configuration can have its own. " +
                       "The date and time drive where the sun sits in the sky; geo.set_geographic_" +
                       "location is what tells AutoCAD where on Earth that is, and without one the " +
                       "angle is computed for a default place.",
            });
        });

    private static Task<ToolDispatchResult> SetSunProperties(JsonObject args, CancellationToken ct) =>
        Run("acad.lights.set_sun_properties", args, ct, (doc, db, tr) =>
        {
            var a = Read<SunArgsDto>(args);
            if (a.On is null && a.Intensity is null && a.DateTime is null
                && a.SkyIllumination is null && a.Haze is null)
                throw new ArgumentException(
                    "Nothing to set. Give at least one of on, intensity, dateTime, " +
                    "skyIllumination or haze.");
            if (a.Intensity is not null && a.Intensity < 0)
                throw new ArgumentException("intensity cannot be negative.");
            if (a.Haze is not null && (a.Haze < 0 || a.Haze > 15))
                throw new ArgumentException(
                    "haze runs from 0 (perfectly clear) to 15 (heavy). AutoCAD refuses anything " +
                    "outside that.");

            DateTime? when = null;
            if (a.DateTime is not null)
            {
                if (!DateTime.TryParse(a.DateTime, System.Globalization.CultureInfo.InvariantCulture,
                                       System.Globalization.DateTimeStyles.None, out var parsed))
                    throw new ArgumentException(
                        "dateTime could not be read. Give it as 'yyyy-MM-dd HH:mm', for example " +
                        "'2026-06-21 12:00' - the date matters as much as the time, since the " +
                        "sun's height depends on the season.");
                when = parsed;
            }

            var vtr = (ViewportTableRecord)tr.GetObject(db.CurrentViewportTableRecordId,
                                                        OpenMode.ForWrite);
            var existing = CurrentSun(db, tr, OpenMode.ForWrite);
            var created = existing is null;
            var s = existing ?? new Sun();
            var before = created ? null : Describe(s);

            if (a.On is not null) s.IsOn = a.On.Value;
            if (a.Intensity is not null) s.Intensity = a.Intensity.Value;
            if (when is not null) s.DateTime = when.Value;
            if (a.SkyIllumination is not null || a.Haze is not null)
            {
                // SkyParameters is a struct read and written WHOLE, the same shape as a material
                // channel: mutating what the property hands back changes nothing.
                var sky = s.SkyParameters;
                // MEASURED: Illumination is a BOOL - sky illumination on or off - not an amount.
                if (a.SkyIllumination is not null) sky.Illumination = a.SkyIllumination.Value;
                if (a.Haze is not null) sky.Haze = a.Haze.Value;
                s.SkyParameters = sky;
            }

            if (created)
            {
                // Attached AFTER being configured, and the id it hands back is what proves it
                // took - see rule 26 §18, where Light.HasTarget silently reverted for want of
                // exactly this ordering.
                var id = vtr.SetSun(s);
                if (id.IsNull)
                    throw new InvalidOperationException(
                        "SetSun returned a null id, so the sun was not attached to the viewport.");
                tr.AddNewlyCreatedDBObject(s, true);
            }

            var back = CurrentSun(db, tr, OpenMode.ForRead)
                       ?? throw new InvalidOperationException(
                           "The viewport reports no sun after one was set.");

            return Wrap(new
            {
                created,
                before,
                sun = Describe(back),
                note = "Read back off the VIEWPORT afterwards, not echoed - a sun that failed to " +
                       "attach would otherwise look identical to one that took. The sun belongs to " +
                       "the current viewport configuration; another can have its own. Where the " +
                       "sun actually sits in the sky depends on the date and time here AND on " +
                       "geo.set_geographic_location: without a geographic location the angle is " +
                       "computed for a default place, so a sun that looks wrong is usually a " +
                       "drawing that has not been located.",
            });
        });
}
