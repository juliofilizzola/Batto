using System.Threading;
using System.Windows;
using Batto.Battery;
using Batto.Bluetooth;
using Batto.Notifications;
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
    private BatteryMonitor? _batteryMonitor;
    private TrayManager? _trayManager;
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

        // Prevent application from shutting down when MainWindow is hidden
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // 2. Initialize Core Services
        _settingsManager = new SettingsManager();

        _notificationManager = new NotificationManager(
            _settingsManager.Settings,
            payload => _trayManager?.ShowBalloonNotification(payload.Title, payload.Message, payload.IsWarning));

        _bluetoothManager = new BluetoothManager();
        _gattReader = new GattBatteryReader();
        _pnpReader = new PnpBatteryReader();

        _batteryMonitor = new BatteryMonitor(
            _bluetoothManager,
            _gattReader,
            _pnpReader,
            _notificationManager,
            _settingsManager);

        // 3. Initialize Tray Manager
        _trayManager = new TrayManager(
            _batteryMonitor,
            _settingsManager.Settings,
            OpenMainWindow,
            ExitApplication);

        // 4. Initialize Main Window
        var viewModel = new MainViewModel(_batteryMonitor, _settingsManager, ExitApplication);
        _mainWindow = new MainWindow(viewModel);
        MainWindow = _mainWindow;

        // 5. Start Background Monitoring
        _batteryMonitor.Start();

        // 6. Handle CLI Flags
        bool startMinimized = e.Args.Contains("--minimized");
        if (startMinimized)
        {
            _mainWindow.WindowState = WindowState.Minimized;
            _mainWindow.Show();
            _mainWindow.Hide();
        }
        else
        {
            OpenMainWindow();
        }

        CrashLogger.Log($"Batto started successfully. startMinimized={startMinimized}", null);
    }

    public void OpenMainWindow()
    {
        Dispatcher.Invoke(() =>
        {
            if (_mainWindow != null)
            {
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
            }
        });
    }

    public void ExitApplication()
    {
        CrashLogger.Log("App.ExitApplication called", null);
        Dispatcher.Invoke(() =>
        {
            _mainWindow?.RequestRealClose();
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
