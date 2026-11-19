using System.Collections.ObjectModel;
using System.Windows;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Batto.Models;
using Batto.Native;

namespace Batto.Tray;

/// <summary>
/// Hover card shown when the pointer rests on a Batto tray icon. It lists every device with
/// its ring and percentage. It never takes focus and hides shortly after the pointer leaves.
/// </summary>
public partial class DeviceFlyout : Window
{
    private readonly DispatcherTimer _hideTimer;
    private readonly ICollectionView _view;

    public DeviceFlyout(ObservableCollection<BluetoothDeviceModel> devices)
    {
        InitializeComponent();

        // Private view: only connected devices that are shown in the tray. The main window keeps the full list.
        _view = new CollectionViewSource { Source = devices }.View;
        _view.Filter = item => item is BluetoothDeviceModel { IsTrayVisible: true, IsConnected: true };
        DeviceList.ItemsSource = _view;
        devices.CollectionChanged += (_, _) => UpdateContent();
        UpdateContent();

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            Hide();
        };

        MouseEnter += (_, _) => _hideTimer.Stop();
        MouseLeave += (_, _) => ScheduleHide();
    }

    /// <summary>Re-applies the tray filter and empty state. Call after tray visibility changes.</summary>
    public void UpdateContent()
    {
        _view.Refresh();
        EmptyText.Visibility = _view.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Shows the card just above the mouse pointer, kept inside the work area.</summary>
    public void ShowNearCursor()
    {
        _hideTimer.Stop();

        if (!IsVisible)
        {
            Show();
        }
        UpdateLayout();

        if (!NativeMethods.GetCursorPos(out var cursor)) return;

        // GetCursorPos returns device pixels; WPF positions windows in device-independent units
        var fromDevice = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
        Point pointer = fromDevice.Transform(new Point(cursor.X, cursor.Y));

        Rect work = SystemParameters.WorkArea;
        double left = Math.Clamp(pointer.X - ActualWidth / 2, work.Left + 4, work.Right - ActualWidth - 4);
        double top = Math.Clamp(pointer.Y - ActualHeight - 12, work.Top + 4, work.Bottom - ActualHeight - 4);

        Left = left;
        Top = top;
    }

    public void ScheduleHide()
    {
        _hideTimer.Stop();
        _hideTimer.Start();
    }
}
