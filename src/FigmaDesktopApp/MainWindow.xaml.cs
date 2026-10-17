using FigmaDesktopApp.Features.ChatStudio;
using Microsoft.UI.Xaml;

namespace FigmaDesktopApp;

public sealed partial class MainWindow : Window
{
    public ChatStudioViewModel ViewModel { get; }

    public MainWindow(ChatStudioViewModel viewModel)
    {
        ViewModel = viewModel;
        this.InitializeComponent();
    }
}
