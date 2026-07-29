using System;
using System.Windows;
using System.Windows.Media;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace AcadMcp.Companion.Host.UI;

/// <summary>
/// Builds the brush set for the chat palette so it stays readable in BOTH AutoCAD themes.
/// Brushes are exposed as DynamicResource keys ("Brush.*") consumed by ChatView.xaml.
/// </summary>
internal static class ThemePalette
{
    /// <summary>Reads AutoCAD's COLORTHEME (0 = dark, 1 = light) and returns matching brushes.</summary>
    public static ResourceDictionary ForCurrentAutoCad()
    {
        bool dark = true;
        try
        {
            // COLORTHEME: 0 = dark, 1 = light. Missing on very old releases -> assume dark.
            var v = AcadApp.GetSystemVariable("COLORTHEME");
            dark = Convert.ToInt32(v) == 0;
        }
        catch
        {
            // Not inside AutoCAD (designer / unit test) -> dark default.
        }
        return dark ? Dark() : Light();
    }

    private static SolidColorBrush B(string hex)
    {
        var c = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(c);
        brush.Freeze();
        return brush;
    }

    private static ResourceDictionary Dark()
    {
        var d = new ResourceDictionary();
        d["Brush.Window"] = B("#FF2B2B2B");
        d["Brush.Surface"] = B("#FF333436");
        d["Brush.SurfaceAlt"] = B("#FF222222");
        d["Brush.Input"] = B("#FF1E1E1E");
        d["Brush.Border"] = B("#FF555759");
        d["Brush.Text"] = B("#FFECECEC");
        d["Brush.SubtleText"] = B("#FFB4B4B4");
        d["Brush.Accent"] = B("#FF2D7DD2");
        d["Brush.AccentText"] = B("#FFFFFFFF");
        d["Brush.Button"] = B("#FF3C3F41");
        d["Brush.ButtonText"] = B("#FFECECEC");
        d["Brush.UserBubble"] = B("#FF2D7DD2");
        d["Brush.UserBubbleText"] = B("#FFFFFFFF");
        d["Brush.AssistantBubble"] = B("#FF3C3F41");
        d["Brush.AssistantBubbleText"] = B("#FFECECEC");
        d["Brush.ToolBubble"] = B("#FF2E3B2E");
        d["Brush.ToolBubbleText"] = B("#FFD6E8D6");
        d["Brush.ErrorBubble"] = B("#FF5A2A2A");
        d["Brush.ErrorBubbleText"] = B("#FFFFE0E0");
        d["Brush.Chip"] = B("#FF444A4F");
        return d;
    }

    private static ResourceDictionary Light()
    {
        var d = new ResourceDictionary();
        d["Brush.Window"] = B("#FFF4F5F7");
        d["Brush.Surface"] = B("#FFFFFFFF");
        d["Brush.SurfaceAlt"] = B("#FFE9ECF0");
        d["Brush.Input"] = B("#FFFFFFFF");
        d["Brush.Border"] = B("#FFC4C9D0");
        d["Brush.Text"] = B("#FF1A1A1A");
        d["Brush.SubtleText"] = B("#FF5A6068");
        d["Brush.Accent"] = B("#FF1565C0");
        d["Brush.AccentText"] = B("#FFFFFFFF");
        d["Brush.Button"] = B("#FFE2E6EB");
        d["Brush.ButtonText"] = B("#FF1A1A1A");
        d["Brush.UserBubble"] = B("#FF1565C0");
        d["Brush.UserBubbleText"] = B("#FFFFFFFF");
        d["Brush.AssistantBubble"] = B("#FFE9ECF0");
        d["Brush.AssistantBubbleText"] = B("#FF1A1A1A");
        d["Brush.ToolBubble"] = B("#FFDDEAD9");
        d["Brush.ToolBubbleText"] = B("#FF234023");
        d["Brush.ErrorBubble"] = B("#FFF7D7D7");
        d["Brush.ErrorBubbleText"] = B("#FF7A1F1F");
        d["Brush.Chip"] = B("#FFD4DAE1");
        return d;
    }
}
