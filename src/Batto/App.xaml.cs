using System.Threading;
using System.Windows;
using Batto.Battery;
using Batto.Bluetooth;
using Batto.Notifications;
using Batto.Platform;
using Batto.Storage;
using Batto.Tray;
using Batto.ViewModels;

namespace Batto;

public partial class App : Application
{
    private const string AppMutexName = @"Local\BattoBluetoothBatteryTray_Mutex";
    private Mutex? _instanceMutex;

    private SettingsManager? _settingsManager;
    private NotificationManager? _notificationManager;
    private BluetoothManager? _bluetoothManager;
    private GattBatteryReader? _gattReader;
    private PnpBatteryReader? _pnpReader;
    private WindowsBatteryReader? _batteryReader;
    private BatteryMonitor? _batteryMonitor;
    private TrayManager? _trayManager;
    private MainViewModel? _mainViewModel;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Global exception handling
        DispatcherUnhandledException += (s, args) =>
        {
            CrashLogger.Log("DispatcherUnhandledException", args.Exception);
            MessageBox.Show($"Ocorreu um erro: {args.Exception.Message}", "Batto", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            CrashLogger.Log("AppDomain.UnhandledException", ex);
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            CrashLogger.Log("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        // 1. Single instance enforcement
        _instanceMutex = new Mutex(true, AppMutexName, out bool isOnlyInstance);
        if (!isOnlyInstance)
        {
            MessageBox.Show("O Batto já está em execução na barra de tarefas.", "Batto", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);

        // The app lives in the tray: closing the main window must not shut it down
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // 2. Initialize Core Services
        _settingsManager = new SettingsManager(autostart: new WindowsAutostart());

        _notificationManager = new NotificationManager(
            _settingsManager.Settings,
            payload => _trayManager?.ShowBalloonNotification(payload.Title, payload.Message, payload.IsWarning));

        _bluetoothManager = new BluetoothManager();
        _gattReader = new GattBatteryReader();
        _pnpReader = new PnpBatteryReader();
        _batteryReader = new WindowsBatteryReader(_pnpReader, _gattReader);

        _batteryMonitor = new BatteryMonitor(
            _bluetoothManager,
            _batteryReader,
            _notificationManager,
            _settingsManager,
            new Batto.Logitech.LogitechDeviceScanner(),
            new WpfUiDispatcher());

        // 3. Initialize Tray Manager
        _trayManager = new TrayManager(
            _batteryMonitor,
            _settingsManager.Settings,
            OpenMainWindow,
            ExitApplication);

        // 4. The view model lives as long as the app; the window is only built when it is opened
        _mainViewModel = new MainViewModel(_batteryMonitor, _settingsManager, ExitApplication);

        // 5. Start Background Monitoring
        _batteryMonitor.Start();

        // 6. Handle CLI Flags: --minimized (autostart) never builds the window
        bool startMinimized = e.Args.Contains("--minimized");
        if (!startMinimized)
        {
            OpenMainWindow();
        }

        CrashLogger.Log($"Batto started successfully. startMinimized={startMinimized}", null);
    }

    public void OpenMainWindow()
    {
        Dispatcher.Invoke(() =>
        {
            if (_mainViewModel == null) return;

            if (_mainWindow == null)
            {
                _mainWindow = new MainWindow(_mainViewModel);
                _mainWindow.Closed += OnMainWindowClosed;
                MainWindow = _mainWindow;
            }

            if (!_mainWindow.IsVisible)
            {
                _mainWindow.Show();
            }

            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }

            _mainWindow.Activate();
            _mainWindow.Focus();
        });
    }

    /// <summary>
    /// Closing the window to the tray destroys it, so its visual tree can be collected while only the tray runs.
    /// The view model stays, so the selected tab and status survive the next open.
    /// </summary>
    private void OnMainWindowClosed(object? sender, EventArgs e)
    {
        if (_mainWindow != null)
        {
            _mainWindow.Closed -= OnMainWindowClosed;
            _mainWindow = null;
        }
        MainWindow = null;

        // With only the tray left the app allocates almost nothing, so the GC would take a long time to run on its own.
        // One compacting collection once the dispatcher is idle returns the closed window's memory right away.
        Dispatcher.BeginInvoke(() =>
        {
            System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    public void ExitApplication()
    {
        CrashLogger.Log("App.ExitApplication called", null);
        Dispatcher.Invoke(() =>
        {
            _mainWindow?.Close();
            Shutdown();
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        CrashLogger.Log($"App.OnExit called. ApplicationCode={e.ApplicationExitCode}", null);
        _trayManager?.Dispose();
        _batteryMonitor?.Dispose();

        if (_instanceMutex != null)
        {
            _instanceMutex.ReleaseMutex();
            _instanceMutex.Dispose();
            _instanceMutex = null;
        }

        base.OnExit(e);
    }
}
