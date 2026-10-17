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

    private void OnCopyCodeClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string code)
        {
            ViewModel.CopyCode(code);
        }
    }

    private void OnStudioNavClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SetNavIndex(0);
    }

    private void OnSettingsNavClick(object sender, RoutedEventArgs e)
    {
        ViewModel.SetNavIndex(1);
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
}
