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
}
