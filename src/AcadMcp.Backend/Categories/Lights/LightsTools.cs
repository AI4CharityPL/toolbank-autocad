// MCP tool surface for the acad-lights category (roadmap 6.1, second tranche).
// See rule 19 (impl pattern), 20 ([McpTool]), 21 (naming), 22 (args/results).
//
// MEASURED: Light.LightType is a GraphicsInterface.DrawableType - the right name in the wrong
// namespace if you look beside Light. It is Light.IsOn, not On. HotspotAngle and FalloffAngle are
// READ-ONLY and set together through SetHotspotAndFalloff, which is also what stops the cone being
// inverted. ShadowMapSize, ShadowSoftness and GlyphDisplay do not exist.
//
// create_web_light is absent: a web light is defined by an .ies photometric file, and without one
// there is nothing to verify.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Lights;

public static class LightsTools
{
    private const int T_NORMAL = 15_000;

    [McpTool("create_point_light", "Create a point light - a bare bulb that throws light in EVERY direction from one spot. It has no target and no cone, so there is nothing to aim: create_spot_light is the one that points somewhere. Set the position, and optionally the intensity, colour and whether it starts on. Lights are addressed by NAME throughout this category, so a name already in use is refused - two lights with one name could not be told apart. Read back by searching model space after creation rather than from the object just written.", "lights",
        Intent = new[] { "create a point light", "add a light bulb at this position",
                         "utworz swiatlo punktowe", "put a lamp here",
                         "dodaj zrodlo swiatla do rysunku", "add an omnidirectional light",
                         "place a light in the model" },
        RequiresPlugin = true)]
    public static Task<LightCreateResult> CreatePointLight(IPluginGateway gw, PointLightArgs args, CancellationToken ct)
        => LightsProxy.CallAsync<PointLightArgs, LightCreateResult>(gw, "acad.lights.create_point_light", args, T_NORMAL, ct);

    [McpTool("create_spot_light", "Create a spot light - aimed from a position at a target, with two cones. The HOTSPOT is the bright inner cone and the FALLOFF the dimmer outer edge, both in RADIANS, so the hotspot is always the smaller of the two and a hotspot wider than the falloff is refused as a shape a light cannot have. Defaults are about 20 and 45 degrees. The two angles are set together because both are read-only individually in the API - which is also what keeps the cone from inverting. The result is read back and the angles checked against what was asked for.", "lights",
        Intent = new[] { "create a spot light", "add a spotlight aimed at this point",
                         "utworz reflektor", "put a directional light pointing here",
                         "dodaj swiatlo kierunkowe na rysunku", "add a cone light",
                         "light this object from above" },
        RequiresPlugin = true)]
    public static Task<LightCreateResult> CreateSpotLight(IPluginGateway gw, SpotLightArgs args, CancellationToken ct)
        => LightsProxy.CallAsync<SpotLightArgs, LightCreateResult>(gw, "acad.lights.create_spot_light", args, T_NORMAL, ct);

    [McpTool("create_distant_light", "Create a distant light - the sun-like one: parallel rays of the same strength everywhere, so only their DIRECTION matters. Give a direction vector, or a position and target to define one. Because the rays never weaken with distance, MOVING a distant light changes nothing; only turning it changes the picture, which is why its position is a convention rather than a place. Use this for sunlight and a point or spot light for lamps.", "lights",
        Intent = new[] { "create a distant light", "add sunlight to the model",
                         "utworz swiatlo odlegle", "add a directional sun light",
                         "dodaj swiatlo sloneczne do rysunku", "parallel light rays",
                         "light everything from one direction" },
        RequiresPlugin = true)]
    public static Task<LightCreateResult> CreateDistantLight(IPluginGateway gw, DistantLightArgs args, CancellationToken ct)
        => LightsProxy.CallAsync<DistantLightArgs, LightCreateResult>(gw, "acad.lights.create_distant_light", args, T_NORMAL, ct);

    [McpTool("list_lights", "List the lights in model space with their type, position, target, intensity, colour, cone angles and whether each is on. Read-only. A light that is OFF is still listed, because it is still in the drawing and is usually the one you were looking for; `onCount` says how many are actually lit. The cone angles are in radians and mean nothing for a point or distant light. `on` comes from Light.IsOn - there is no Light.On in the API.", "lights",
        Intent = new[] { "list the lights", "what lights are in this drawing",
                         "lista swiatel", "show all the lights",
                         "jakie zrodla swiatla sa na rysunku", "find a light by name",
                         "how many lights are on" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<LightListResult> ListLights(IPluginGateway gw, LightsNoArgs args, CancellationToken ct)
        => LightsProxy.CallAsync<LightsNoArgs, LightListResult>(gw, "acad.lights.list_lights", args, T_NORMAL, ct);

    [McpTool("set_light_properties", "Change a light: turn it on or off, set its intensity, colour, position, target, or cone angles. Only the properties named are touched and the previous values are reported, so a change can be undone. Two things worth knowing: moving the TARGET of a point light is refused, because a point light has nothing to aim; and changing ONE cone angle carries the other over from what is already set, since AutoCAD only accepts the pair together - so a new hotspot is checked against the existing falloff and refused if it would be the wider of the two.", "lights",
        Intent = new[] { "turn this light off", "change the light intensity",
                         "zmien wlasciwosci swiatla", "make the light brighter",
                         "ustaw kolor swiatla na rysunku", "move a light",
                         "adjust the spotlight cone" },
        RequiresPlugin = true)]
    public static Task<LightModifyResult> SetLightProperties(IPluginGateway gw, LightModifyArgs args, CancellationToken ct)
        => LightsProxy.CallAsync<LightModifyArgs, LightModifyResult>(gw, "acad.lights.set_light_properties", args, T_NORMAL, ct);

    [McpTool("delete_light", "Delete a light from the drawing. Its previous settings are reported in full, so it can be recreated if the deletion was a mistake. Deleting a light changes what a render looks like and nothing about the geometry. Confirmed gone by searching model space again rather than assumed.", "lights",
        Intent = new[] { "delete this light", "remove a light from the drawing",
                         "usun swiatlo", "get rid of that lamp",
                         "skasuj zrodlo swiatla z rysunku", "clean up unused lights",
                         "delete a spotlight" },
        RequiresPlugin = true)]
    public static Task<LightDeleteResult> DeleteLight(IPluginGateway gw, LightNameArgs args, CancellationToken ct)
        => LightsProxy.CallAsync<LightNameArgs, LightDeleteResult>(gw, "acad.lights.delete_light", args, T_NORMAL, ct);

    [McpTool("get_sun_properties", "Read the sun of the current viewport: whether it is on, its intensity, the date and time it is set to, the sky illumination and haze, and the shadow settings. Read-only. IMPORTANT: a sun belongs to a VIEWPORT, not to the drawing - Database.SunId does not exist and ViewportTableRecord.SunId does - so this reports the sun of the current viewport configuration and another configuration can have its own. A drawing has NO sun until something attaches one, and that absence is reported as hasSun=false rather than answered with defaults that were never set.", "lights",
        Intent = new[] { "get the sun settings", "is the sun on in this drawing",
                         "odczytaj ustawienia slonca", "what date and time is the sun set to",
                         "jakie sa ustawienia slonca na rysunku", "read the sun properties",
                         "check the sunlight settings" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<SunGetResult> GetSunProperties(IPluginGateway gw, LightsNoArgs args, CancellationToken ct)
        => LightsProxy.CallAsync<LightsNoArgs, SunGetResult>(gw, "acad.lights.get_sun_properties", args, T_NORMAL, ct);

    [McpTool("set_sun_properties", "Turn the sun on or off and set its intensity, date and time, sky illumination and haze. Creates the sun if the viewport has none, and says which it did. `dateTime` is 'yyyy-MM-dd HH:mm' and the DATE matters as much as the time, since the sun's height depends on the season. `haze` runs 0 (perfectly clear) to 15 (heavy); `skyIllumination` is a switch, not a level. WHERE THE SUN ACTUALLY SITS also depends on geo.set_geographic_location - without a geographic location the angle is computed for a default place, so a sun that looks wrong is usually a drawing that has not been located. Read back off the viewport afterwards rather than echoed.", "lights",
        Intent = new[] { "turn the sun on", "set the sun date and time",
                         "ustaw slonce", "set the sun for midday in june",
                         "wlacz slonce na rysunku", "change the sunlight intensity",
                         "set the haze for the sky" },
        RequiresPlugin = true)]
    public static Task<SunSetResult> SetSunProperties(IPluginGateway gw, SunSetArgs args, CancellationToken ct)
        => LightsProxy.CallAsync<SunSetArgs, SunSetResult>(gw, "acad.lights.set_sun_properties", args, T_NORMAL, ct);
}
