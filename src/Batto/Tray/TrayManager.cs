using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Batto.Battery;
using Batto.Models;
using Batto.Native;

namespace Batto.Tray;

/// <summary>
/// Owns the single Batto notification-area icon. The icon is a ring split per device,
/// redrawn whenever a battery value or connection changes. Hovering shows the device flyout.
/// </summary>
public class TrayManager : IDisposable
{
    private const uint TrayId = 1001;

    private readonly BatteryMonitor _batteryMonitor;
    private readonly AppSettings _settings;
    private readonly Action _openMainWindowAction;
    private readonly Action _exitAppAction;
    private readonly object _syncLock = new();

    private HwndSource? _hwndSource;
    private IntPtr _hwnd = IntPtr.Zero;
    private uint _taskbarCreatedMsg;
    private IntPtr _currentHIcon = IntPtr.Zero;
    private bool _iconAdded;
    private DeviceFlyout? _flyout;
    private readonly Action<BluetoothDeviceModel> _onDeviceChanged;

    public TrayManager(
        BatteryMonitor batteryMonitor,
        AppSettings settings,
        Action openMainWindowAction,
        Action exitAppAction)
    {
        _batteryMonitor = batteryMonitor;
        _settings = settings;
        _openMainWindowAction = openMainWindowAction;
        _exitAppAction = exitAppAction;

        InitializeMessageWindow();
        _flyout = new DeviceFlyout(batteryMonitor.ObservableDevices);

        _onDeviceChanged = _ => Refresh();
        _batteryMonitor.DeviceBatteryUpdated += _onDeviceChanged;
        _batteryMonitor.DeviceConnectionChanged += _onDeviceChanged;
        _batteryMonitor.DevicesCollectionChanged += Refresh;

        Refresh();
    }

    private void InitializeMessageWindow()
    {
        _taskbarCreatedMsg = NativeMethods.RegisterWindowMessage("TaskbarCreated");

        var parameters = new HwndSourceParameters("BattoTrayMsgWindow")
        {
            WindowStyle = 0,
            ExtendedWindowStyle = 0,
            ParentWindow = IntPtr.Zero
        };
        parameters.SetSize(0, 0);
        parameters.SetPosition(0, 0);

        _hwndSource = new HwndSource(parameters);
        _hwnd = _hwndSource.Handle;
        _hwndSource.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Explorer restarted: the icon is gone and must be added again
        if (_taskbarCreatedMsg != 0 && (uint)msg == _taskbarCreatedMsg)
        {
            lock (_syncLock) _iconAdded = false;
            Refresh();
            handled = true;
            return IntPtr.Zero;
        }

        if ((uint)msg != NativeMethods.WM_TRAYCALLBACK) return IntPtr.Zero;

        int eventId = (int)lParam.ToInt64() & 0xFFFF;

        switch (eventId)
        {
            case NativeMethods.NIN_POPUPOPEN:
                _flyout?.ShowNearCursor();
                break;

            case NativeMethods.NIN_POPUPCLOSE:
                _flyout?.ScheduleHide();
                break;

            case NativeMethods.WM_LBUTTONUP:
            case NativeMethods.WM_LBUTTONDBLCLK:
            case NativeMethods.NIN_SELECT:
            case NativeMethods.NIN_KEYSELECT:
                _openMainWindowAction();
                break;

            case NativeMethods.WM_RBUTTONUP:
            case NativeMethods.WM_CONTEXTMENU:
                ShowContextMenu();
                break;
        }

        handled = true;
        return IntPtr.Zero;
    }

    /// <summary>Redraws the ring from the current devices and adds or updates the icon.</summary>
    public void Refresh()
    {
        if (_hwnd == IntPtr.Zero) return;

        lock (_syncLock)
        {
            var devices = _batteryMonitor.GetDevicesForTray()
                .Take(Math.Max(1, _settings.MaxTrayIcons))
                .ToList();

            IntPtr newHIcon = IconGenerator.CreateSummaryIcon(devices);

            var nid = new NativeMethods.NOTIFYICONDATA
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = TrayId,
                uFlags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_ICON,
                uCallbackMessage = NativeMethods.WM_TRAYCALLBACK,
                hIcon = newHIcon
            };

            if (!_iconAdded)
            {
                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_ADD, ref nid);
                nid.uTimeoutOrVersion = NativeMethods.NOTIFYICON_VERSION_4;
                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_SETVERSION, ref nid);
                _iconAdded = true;

                if (_settings.PromoteTrayIcons)
                {
                    TrayPromotion.SchedulePromotion();
                }
            }
            else
            {
                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref nid);
            }

            if (_currentHIcon != IntPtr.Zero)
            {
                NativeMethods.DestroyIcon(_currentHIcon);
            }
            _currentHIcon = newHIcon;
        }

        Application.Current?.Dispatcher.BeginInvoke(() => _flyout?.UpdateContent());
    }

    public void ShowBalloonNotification(string title, string message, bool isWarning = false)
    {
        if (_hwnd == IntPtr.Zero) return;

        lock (_syncLock)
        {
            if (!_iconAdded) return;

            var nid = new NativeMethods.NOTIFYICONDATA
            {
                cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = TrayId,
                uFlags = NativeMethods.NIF_INFO,
                szInfoTitle = title.Length >= 63 ? title.Substring(0, 60) + "..." : title,
                szInfo = message.Length >= 255 ? message.Substring(0, 250) + "..." : message,
                dwInfoFlags = isWarning ? NativeMethods.NIIF_WARNING : NativeMethods.NIIF_INFO
            };

            NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_MODIFY, ref nid);
        }
    }

    private void ShowContextMenu()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var menu = new ContextMenu();

            var openItem = new MenuItem { Header = "Abrir Gerenciador Batto" };
            openItem.Click += (s, e) => _openMainWindowAction();
            menu.Items.Add(openItem);

            var refreshAllItem = new MenuItem { Header = "Atualizar Todos os Dispositivos" };
            refreshAllItem.Click += async (s, e) => await _batteryMonitor.RefreshAllDevicesAsync();
            menu.Items.Add(refreshAllItem);

            menu.Items.Add(new Separator());

            var exitItem = new MenuItem { Header = "Sair do Batto" };
            exitItem.Click += (s, e) => _exitAppAction();
            menu.Items.Add(exitItem);

            NativeMethods.SetForegroundWindow(_hwnd);
            menu.IsOpen = true;
        });
    }

    public void Dispose()
    {
        _batteryMonitor.DeviceBatteryUpdated -= _onDeviceChanged;
        _batteryMonitor.DeviceConnectionChanged -= _onDeviceChanged;
        _batteryMonitor.DevicesCollectionChanged -= Refresh;

        lock (_syncLock)
        {
            if (_iconAdded)
            {
                var nid = new NativeMethods.NOTIFYICONDATA
                {
                    cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.NOTIFYICONDATA>(),
                    hWnd = _hwnd,
                    uID = TrayId
                };
                NativeMethods.Shell_NotifyIcon(NativeMethods.NIM_DELETE, ref nid);
                _iconAdded = false;
            }

            if (_currentHIcon != IntPtr.Zero)
            {
                NativeMethods.DestroyIcon(_currentHIcon);
                _currentHIcon = IntPtr.Zero;
            }
        }

        _flyout?.Close();
        _flyout = null;

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource.Dispose();
            _hwndSource = null;
        }
    }
}
