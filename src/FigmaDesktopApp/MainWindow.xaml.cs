using FigmaDesktopApp.Features.ChatStudio;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace FigmaDesktopApp;

public sealed partial class MainWindow : Window
{
    public ChatStudioViewModel ViewModel { get; }

    public MainWindow(ChatStudioViewModel viewModel)
    {
        ViewModel = viewModel;
        this.InitializeComponent();

        try
        {
            this.SystemBackdrop = new MicaBackdrop();
        }
        catch
        {
            // Graceful fallback on unsupported platforms
        }

        ViewModel.Messages.CollectionChanged += (s, e) =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ChatScrollViewer.ChangeView(null, ChatScrollViewer.ScrollableHeight, null, false);
            });
        };
    }

    public ImageSource? ConvertBase64(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64)) return null;
        try
        {
            if (base64.StartsWith("data:image", StringComparison.OrdinalIgnoreCase))
            {
                int comma = base64.IndexOf(',');
                if (comma >= 0) base64 = base64.Substring(comma + 1);
            }
            byte[] bytes = System.Convert.FromBase64String(base64.Trim());
            var image = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
            using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
            using (var writer = new Windows.Storage.Streams.DataWriter(stream.GetOutputStreamAt(0)))
            {
                writer.WriteBytes(bytes);
                writer.StoreAsync().AsTask().GetAwaiter().GetResult();
            }
            image.SetSource(stream);
            return image;
        }
        catch { return null; }
    }

    private void OnDismissStatusBannerClick(object sender, RoutedEventArgs e)
    {
        ViewModel.DismissStatusBanner();
    }

    private void OnUserInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            if (ViewModel.CanSendPrompt)
            {
                _ = ViewModel.SendPromptAsync();
            }
        }
    }

    private void OnSuggestionChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string prompt)
        {
            ViewModel.SelectSuggestion(prompt);
        }
    }

    private void OnSuggestedOptionClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string optionText)
        {
            ViewModel.SelectSuggestion(optionText);
        }
    }

    private void OnCopyCodeClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string code)
        {
            ViewModel.CopyCode(code);
        }
    }

    private void OnToggleSettingsClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleSettings();
    }

    private void OnCloseSettingsClick(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseSettings();
    }

    private void OnStudioNavClick(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseSettings();
    }

    private void OnSettingsNavClick(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenSettings();
    }

    private void OnDarkModeClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedThemeIndex = 0;
    }

    private void OnLightModeClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedThemeIndex = 1;
    }

    private void OnSystemModeClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedThemeIndex = 2;
    }

    private void OnToggleAssistantClick(object sender, RoutedEventArgs e)
    {
        ViewModel.ToggleAssistant();
    }

    private void OnToggleThemeClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedThemeIndex = ViewModel.SelectedThemeIndex == 0 ? 1 : 0;
    }
}
