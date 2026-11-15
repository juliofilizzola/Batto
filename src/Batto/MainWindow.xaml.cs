using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using Batto.Models;
using Batto.Native;
using Batto.ViewModels;

namespace Batto;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isRealExit;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        EnableImmersiveDarkMode();
    }

    private void EnableImmersiveDarkMode()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                int darkMode = 1;
                // DWMWA_USE_IMMERSIVE_DARK_MODE = 20
                NativeMethods.DwmSetWindowAttribute(hwnd, 20, ref darkMode, sizeof(int));
            }
        }
        catch
        {
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        CrashLogger.Log($"MainWindow.OnClosing called. _isRealExit={_isRealExit}", null);
        if (!_isRealExit)
        {
            // Minimize to tray instead of closing process
            e.Cancel = true;
            Hide();
        }
        else
        {
            base.OnClosing(e);
        }
    }

    public void RequestRealClose()
    {
        _isRealExit = true;
        Close();
    }

    private void OnMinimizeToTrayClicked(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OnTrayVisibilityToggled(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: BluetoothDeviceModel device })
        {
            _viewModel.ToggleDeviceTray(device);
        }
    }

    private void OnDeviceTypeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.ComboBox { DataContext: BluetoothDeviceModel device, SelectedValue: DeviceType type }
            && device.Type != type)
        {
            _viewModel.ChangeDeviceType(device, type);
        }
    }

    private void OnPriorityUpClicked(object sender, RoutedEventArgs e) => ChangePriority(sender, +1);

    private void OnPriorityDownClicked(object sender, RoutedEventArgs e) => ChangePriority(sender, -1);

    private void ChangePriority(object sender, int delta)
    {
        if (sender is FrameworkElement { DataContext: BluetoothDeviceModel device })
        {
            _viewModel.ChangeDevicePriority(device, delta);
        }
    }
}
