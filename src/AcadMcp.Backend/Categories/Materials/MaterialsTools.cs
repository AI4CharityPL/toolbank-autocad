// MCP tool surface for the acad-materials category (roadmap 6.1, first tranche).
// See rule 19 (impl pattern), 20 ([McpTool]), 21 (naming), 22 (args/results).
//
// MEASURED: a Material lives in DatabaseServices but every CHANNEL of it is a
// GraphicsInterface type, and Material.Shininess does not exist - shininess is the Gloss of the
// specular component. Each channel is a struct read and written WHOLE, so a change means
// rebuilding the component rather than setting a field on it.
//
// set_material_map, set_material_mapping and load_material_library are not in this tranche: the
// first two need a texture file to be verifiable and the third an .adsklib library, so they wait
// until one is available rather than shipping unverified.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Materials;

public static class MaterialsTools
{
    private const int T_NORMAL = 15_000;

    [McpTool("create_material", "Create a render material: a diffuse colour (the base colour), a specular colour with a gloss from 0 (matt) to 1 (mirror-sharp), and an opacity from 0 (clear) to 1 (solid). A material is not attached to anything until assign_material puts it on an entity. NOTE the property most people reach for first does not exist: there is no Material.Shininess in the AutoCAD API - shininess is the GLOSS of the specular component, which is what this tool's `gloss` sets. Refuses a name already in use, because replacing a material silently would restyle every object already using it. Read back through a fresh dictionary lookup after creation.", "materials",
        Intent = new[] { "create a material", "make a red shiny material",
                         "utworz material", "define a new render material",
                         "stworz material dla rysunku", "add a material to the drawing",
                         "make a glass material" },
        RequiresPlugin = true)]
    public static Task<MaterialResult> CreateMaterial(IPluginGateway gw, MaterialCreateArgs args, CancellationToken ct)
        => MaterialsProxy.CallAsync<MaterialCreateArgs, MaterialResult>(gw, "acad.materials.create_material", args, T_NORMAL, ct);

    [McpTool("modify_material", "Change a material's colours, gloss, opacity or description. Only the channels named are touched; the rest are rebuilt from what was already there, because each channel is a struct that has to be written back whole. IMPORTANT: every object already using this material changes appearance with it - that is what a material is for, and worth knowing before editing a shared one. The previous values are reported so a change can be undone, and the new ones are read back rather than echoed.", "materials",
        Intent = new[] { "change this material", "make the material shinier",
                         "zmien material", "edit a material's colour",
                         "modyfikuj material w rysunku", "adjust material opacity",
                         "update material properties" },
        RequiresPlugin = true)]
    public static Task<MaterialModifyResult> ModifyMaterial(IPluginGateway gw, MaterialModifyArgs args, CancellationToken ct)
        => MaterialsProxy.CallAsync<MaterialModifyArgs, MaterialModifyResult>(gw, "acad.materials.modify_material", args, T_NORMAL, ct);

    [McpTool("list_materials", "List the materials in the drawing with their diffuse and specular colours, gloss and opacity. Read-only. Every drawing has a material called Global, plus ByLayer and ByBlock, which are what an entity uses when nothing else is assigned - so a small count is an empty drawing rather than a broken tool. gloss comes from the specular component, there being no Material.Shininess in the API.", "materials",
        Intent = new[] { "list the materials", "what materials are in this drawing",
                         "lista materialow", "show all render materials",
                         "jakie materialy sa w rysunku", "find a material by name",
                         "which materials exist" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<MaterialListResult> ListMaterials(IPluginGateway gw, MaterialsNoArgs args, CancellationToken ct)
        => MaterialsProxy.CallAsync<MaterialsNoArgs, MaterialListResult>(gw, "acad.materials.list_materials", args, T_NORMAL, ct);

    [McpTool("assign_material", "Put a material onto one or more entities. Confirmed by reading Entity.Material - the NAME - back afterwards, which is a DIFFERENT property from the MaterialId that was written, so an assignment that did not take cannot look like one that did. Each entity's previous material is reported, so the change can be undone; unassign_material puts them back to ByLayer.", "materials",
        Intent = new[] { "assign this material to these objects", "apply a material to an entity",
                         "przypisz material do obiektow", "give these solids a material",
                         "nadaj material encjom rysunku", "set the material on this object",
                         "paint these with a material" },
        RequiresPlugin = true)]
    public static Task<MaterialAssignResult> AssignMaterial(IPluginGateway gw, MaterialAssignArgs args, CancellationToken ct)
        => MaterialsProxy.CallAsync<MaterialAssignArgs, MaterialAssignResult>(gw, "acad.materials.assign_material", args, T_NORMAL, ct);

    [McpTool("unassign_material", "Clear the material from entities, setting them back to ByLayer - which is what 'no material' means in AutoCAD. There is no null state, and clearing the id outright would leave the entity in a condition AutoCAD does not expect, which is why this sets ByLayer rather than nothing. The material itself is untouched and still in the drawing; delete_material is what removes it.", "materials",
        Intent = new[] { "remove the material from these objects", "clear the material assignment",
                         "usun material z obiektow", "set these back to no material",
                         "odlacz material od encji", "reset material to bylayer",
                         "unassign a material" },
        RequiresPlugin = true)]
    public static Task<MaterialUnassignResult> UnassignMaterial(IPluginGateway gw, MaterialHandlesArgs args, CancellationToken ct)
        => MaterialsProxy.CallAsync<MaterialHandlesArgs, MaterialUnassignResult>(gw, "acad.materials.unassign_material", args, T_NORMAL, ct);

    [McpTool("delete_material", "Delete a material from the drawing. REFUSES when entities are still using it, listing their handles, because deleting would leave them pointing at a material that is gone - unassign them first, or pass `force` if that is really what you mean. AutoCAD's own Global, ByLayer and ByBlock materials are refused outright: they are what an entity falls back to when nothing else is assigned. Removed from the dictionary AND erased, since removing the name alone would leave an orphan in the drawing.", "materials",
        Intent = new[] { "delete this material", "remove a material from the drawing",
                         "usun material z rysunku", "get rid of an unused material",
                         "skasuj material", "clean up materials",
                         "purge a material" },
        RequiresPlugin = true)]
    public static Task<MaterialDeleteResult> DeleteMaterial(IPluginGateway gw, MaterialDeleteArgs args, CancellationToken ct)
        => MaterialsProxy.CallAsync<MaterialDeleteArgs, MaterialDeleteResult>(gw, "acad.materials.delete_material", args, T_NORMAL, ct);
}
