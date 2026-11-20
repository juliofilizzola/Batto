using Avalonia.Controls;

namespace Batto.Linux.UI;

/// <summary>Main window. All state and behaviour live in <see cref="MainViewModel"/>; this class only hosts the view.</summary>
public partial class MainWindow : Window
{
    /// <summary>Parameterless constructor for the XAML loader; the app always uses the view model overload.</summary>
    public MainWindow() : this(new MainViewModel())
    {
    }

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
