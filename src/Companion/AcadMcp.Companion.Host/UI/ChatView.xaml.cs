using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcadMcp.Companion.Host.ViewModels;

namespace AcadMcp.Companion.Host.UI;

/// <summary>Code-behind for the chat palette view. Handles Enter-to-send, auto-scroll and PasswordBox.</summary>
public partial class ChatView : System.Windows.Controls.UserControl
{
    private readonly ChatViewModel _vm;

    public ChatView()
    {
        InitializeComponent();
        ApplyTheme();
        _vm = new ChatViewModel(Dispatcher);
        DataContext = _vm;
        _vm.Messages.CollectionChanged += OnMessagesChanged;
    }

    /// <summary>Merges the AutoCAD-theme-aware brush set so the panel reads well in dark and light.</summary>
    private void ApplyTheme()
    {
        var brushes = ThemePalette.ForCurrentAutoCad();
        Resources.MergedDictionaries.Add(brushes);
    }

    public ChatViewModel ViewModel => _vm;

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => MessagesScroll.ScrollToEnd();

    private void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
        {
            e.Handled = true;
            if (_vm.SendCommand.CanExecute(null)) _vm.SendCommand.Execute(null);
        }
    }

    private void SaveKey_Click(object sender, RoutedEventArgs e)
    {
        _vm.SaveApiKey(ApiKeyBox.Password);
        ApiKeyBox.Clear();
    }

    private void ClearKey_Click(object sender, RoutedEventArgs e)
    {
        _vm.ClearApiKey();
        ApiKeyBox.Clear();
    }
}
