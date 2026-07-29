using System;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using AcadMcp.Companion.Host.UI;
using Autodesk.AutoCAD.Windows;

namespace AcadMcp.Companion.Host;

/// <summary>
/// Owns the singleton modeless palette that hosts the WPF chat view. AutoCAD PaletteSet
/// requires a WinForms Control — WPF is embedded via ElementHost (not raw AddVisual).
/// </summary>
internal static class ChatPalette
{
    // NOTE: AutoCAD persists palette position/state keyed by this GUID in the user profile.
    // Bump this GUID whenever the default layout/dock changes, so AutoCAD doesn't restore a
    // stale (off-screen / wrong-edge) position from a previous build.
    private static readonly Guid PaletteId = new("B2C3D4E5-66FF-4011-AB22-33445566BBCC");
    private static PaletteSet? _paletteSet;
    private static ChatView? _view;

    public static ChatView? View => _view;

    public static void Show()
    {
        bool firstCreate = _paletteSet is null;
        if (_paletteSet is null)
        {
            CompanionLog.Info("Creating PaletteSet (bottom bar)");
            // No auto-hide: it can collapse the palette to an invisible edge sliver.
            _paletteSet = new PaletteSet("Asystent AI", PaletteId)
            {
                Style = PaletteSetStyles.ShowCloseButton
                        | PaletteSetStyles.ShowPropertiesMenu,
                // Compact bar; docked at the bottom the user drags the TOP edge to grow/shrink.
                MinimumSize = new System.Drawing.Size(480, 140),
                Size = new System.Drawing.Size(960, 240),
                DockEnabled = DockSides.Bottom | DockSides.Top | DockSides.Left | DockSides.Right,
            };

            CompanionLog.Info("Creating ChatView (WPF)");
            _view = new ChatView();

            CompanionLog.Info("Wrapping ChatView in ElementHost");
            var host = new ElementHost
            {
                Child = _view,
                Dock = DockStyle.Fill,
                AutoSize = false,
            };

            // PaletteSet.Add expects WinForms Control; ElementHost hosts the WPF tree.
            _paletteSet.Add("Asystent AI", host);
            CompanionLog.Info("PaletteSet.Add(ElementHost) done");
        }

        _paletteSet.Visible = true;

        // Dock to the bottom on first creation. (On later shows we respect the user's chosen
        // dock/float so we don't fight their layout.)
        if (firstCreate)
        {
            try
            {
                _paletteSet.Dock = DockSides.Bottom;
                CompanionLog.Info("Docked palette to Bottom");
            }
            catch (Exception ex)
            {
                CompanionLog.Error("Bottom dock failed; leaving floating (non-fatal)", ex);
            }
        }

        _paletteSet.Activate(0);
        _paletteSet.KeepFocus = true;
        CompanionLog.Info($"PaletteSet visible={_paletteSet.Visible} dock={_paletteSet.Dock}");
    }
}
