// MCP tool surface for the acad-images category (roadmap 3.5, raster half).
// See rule 19 (impl pattern), 20 ([McpTool]), 21 (naming), 22 (args/results), 26 (traps).
//
// MEASURED against the compiler before anything here was written:
//
//   RasterImage.Width/Height/ImageWidth/ImageHeight are all READ-ONLY, derived from
//   Orientation (a CoordinateSystem3d: origin + two vectors) crossed with the source
//   pixel size. There is no direct "set width" - attach_image measures the image at a
//   unit-scale orientation first, then computes and applies the vectors that give the
//   requested drawing-space size, then reads Width/Height back and asserts they match
//   what was asked for. That assertion is the tool's own proof, not an inspection.
//
//   Background transparency (the toggle for bitonal images) is NOT exposed anywhere in
//   the managed API - nine candidate names were tried (IsTransparent, ImageTransparency,
//   IsBackgroundTransparent, Transparent, ShowTransparent, RasterImageDef.IsTransparent,
//   IsBitonal, ImageIsTransparent, DisplayOpaqueBackground) and all nine failed to
//   compile. set_image_transparency is not in this tranche for that reason - matching
//   the project's precedent for arc-aligned text, spell check and mtext frames.
//
//   Frame visibility is IMAGEFRAME, a drawing-wide system variable (0/1/2) - not a
//   per-entity property. RasterImage.ShowImageBorder does not exist. Same shape as
//   XCLIPFRAME in KNOWN-GAPS A3: set_image_frame takes no handle and says so.
//
//   set_draworder in acad-geometry-2d already reorders any entity by handle, images
//   included, so there is no separate reorder tool here - a second name for the same
//   operation would only give a router two ways to spell one action.

using System.Threading;
using System.Threading.Tasks;
using AcadMcp.Backend.Pipe;
using AcadMcp.Shared.Mcp;

namespace AcadMcp.Backend.Categories.Images;

public static class ImagesTools
{
    private const int T_NORMAL = 15_000;
    private const int T_READ = 5_000;

    [McpTool("attach_image", "Attach a raster image (PNG/JPG/BMP/TIFF) at a point with a given width in drawing units - height is computed to preserve the source aspect ratio unless given explicitly. Refuses a missing file. A name already in use is refused UNLESS it points at the exact same file, in which case this is a second placement sharing the existing definition (reusedDefinition: true) - matching how AutoCAD itself lets one image be inserted more than once. The requested width (and height, if given) are READ BACK from the placed entity and the tool refuses if they do not match, rather than trusting the placement math silently. Optional rotationDegrees and layer.", "images",
        Intent = new[] { "attach an image", "insert a png into the drawing",
                         "dolacz obraz do rysunku", "place a raster image",
                         "wstaw zdjecie do autocada", "insert a jpg as a reference",
                         "add a photo to the drawing" },
        RequiresPlugin = true)]
    public static Task<ImageAttachResult> AttachImage(IPluginGateway gw, ImageAttachArgs args, CancellationToken ct)
        => ImagesProxy.CallAsync<ImageAttachArgs, ImageAttachResult>(gw, "acad.images.attach_image", args, T_NORMAL, ct);

    [McpTool("list_images", "List raster images attached to the drawing: name, source path, insertion point, size, rotation, whether clipped, and the brightness/contrast/fade adjustment. Read-only. An empty count with no image dictionary yet is reported as zero rather than an error.", "images",
        Intent = new[] { "list the images", "what images are in this drawing",
                         "lista obrazow w rysunku", "show attached raster images",
                         "jakie obrazy sa dolaczone", "find an image by name",
                         "which pictures are placed" },
        ReadOnly = true, RequiresPlugin = true)]
    public static Task<ImageListResult> ListImages(IPluginGateway gw, ImagesNoArgs args, CancellationToken ct)
        => ImagesProxy.CallAsync<ImagesNoArgs, ImageListResult>(gw, "acad.images.list_images", args, T_READ, ct);

    [McpTool("detach_image", "Remove one image reference from the drawing. If no other image entity still uses the same source definition, the definition is removed too and defRemoved is true; if another placement of the same image remains, only this entity goes and the source stays available for it. Confirmed by re-resolving the handle afterwards rather than assumed from the erase call.", "images",
        Intent = new[] { "detach this image", "remove an image from the drawing",
                         "usun obraz z rysunku", "delete a raster reference",
                         "odlacz zdjecie od rysunku", "remove a placed picture",
                         "get rid of an image" },
        RequiresPlugin = true)]
    public static Task<ImageDetachResult> DetachImage(IPluginGateway gw, ImageHandleArgs args, CancellationToken ct)
        => ImagesProxy.CallAsync<ImageHandleArgs, ImageDetachResult>(gw, "acad.images.detach_image", args, T_NORMAL, ct);

    [McpTool("clip_image", "Clip an image to a boundary given in IMAGE PIXEL SPACE - (0,0) to (imageWidthPx, imageHeightPx) as reported by list_images or by this tool's own result, NOT drawing (WCS) coordinates. Exactly two points clip to the rectangle between them (expanded to the four corners internally); three or more clip to that polygon, closed automatically if not already. Omitting points (or passing an empty list) REMOVES the clip instead, leaving the old boundary stored but inactive rather than erasing it. Reports the entity's drawing-space extents before and after, so a clip that changed nothing shows up as unchanged extents rather than a bare success.", "images",
        Intent = new[] { "clip this image", "crop the raster image",
                         "przytnij obraz", "clip the picture to a rectangle",
                         "obetnij zdjecie do wielokata", "remove the image clip",
                         "unclip this image" },
        RequiresPlugin = true)]
    public static Task<ImageClipResult> ClipImage(IPluginGateway gw, ImageClipArgs args, CancellationToken ct)
        => ImagesProxy.CallAsync<ImageClipArgs, ImageClipResult>(gw, "acad.images.clip_image", args, T_NORMAL, ct);

    [McpTool("set_image_adjust", "Set an image's brightness, contrast and/or fade, each 0-100. Only the ones given are changed; the others are read and reported unchanged. AutoCAD's own defaults are 50/50/0. Read back after setting rather than echoed.", "images",
        Intent = new[] { "adjust image brightness", "change the image contrast",
                         "zmien jasnosc obrazu", "fade this raster image",
                         "ustaw kontrast zdjecia", "make the image lighter",
                         "increase image fade" },
        RequiresPlugin = true)]
    public static Task<ImageAdjustResult> SetImageAdjust(IPluginGateway gw, ImageAdjustArgs args, CancellationToken ct)
        => ImagesProxy.CallAsync<ImageAdjustArgs, ImageAdjustResult>(gw, "acad.images.set_image_adjust", args, T_NORMAL, ct);

    [McpTool("set_image_frame", "Show or hide the border AutoCAD draws around every raster image. IMPORTANT: this is the IMAGEFRAME system variable, which is DRAWING-WIDE like XCLIPFRAME - it takes no handle and affects every image in the drawing at once, not one entity. frame is 0 (no frame, and the image cannot be selected by its edge), 1 (frame shown and plotted) or 2 (frame shown but not plotted). Read back from the system variable after setting, since a rejected value would otherwise look like success.", "images",
        Intent = new[] { "hide image frames", "show the image border",
                         "ukryj ramki obrazow", "turn off image frame display",
                         "pokaz ramke obrazu", "toggle image borders",
                         "set imageframe" },
        RequiresPlugin = true)]
    public static Task<ImageFrameResult> SetImageFrame(IPluginGateway gw, ImageFrameArgs args, CancellationToken ct)
        => ImagesProxy.CallAsync<ImageFrameArgs, ImageFrameResult>(gw, "acad.images.set_image_frame", args, T_NORMAL, ct);

    [McpTool("set_image_path", "Repoint an image's source file to a new path - for a moved or renamed file. Every entity that shares the same source definition is affected, because the definition (not the placement) is what this changes; their handles are listed in the result. Refuses a path that does not exist. loaded reports whether AutoCAD could actually read the new file, since a repath to a bad file 'succeeds' at changing the string and nothing else.", "images",
        Intent = new[] { "repath this image", "fix a broken image link",
                         "napraw sciezke obrazu", "point the image at a new file",
                         "zaktualizuj sciezke do zdjecia", "relink a moved image",
                         "update image source path" },
        RequiresPlugin = true)]
    public static Task<ImagePathResult> SetImagePath(IPluginGateway gw, ImagePathArgs args, CancellationToken ct)
        => ImagesProxy.CallAsync<ImagePathArgs, ImagePathResult>(gw, "acad.images.set_image_path", args, T_NORMAL, ct);
}
