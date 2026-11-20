using Avalonia.Threading;
using Batto.Battery;
using Batto.Linux.Bluez;
using Batto.Linux.Notifications;
using Batto.Linux.Platform;
using Batto.Linux.Storage;
using Batto.Linux.Tray;
using Batto.Logitech;
using Batto.Notifications;
using Batto.Storage;
using Tmds.DBus.Protocol;

namespace Batto.Linux;

/// <summary>
/// Builds and owns the Linux services: the system bus for BlueZ, the core monitor, the tray, notifications and settings.
/// Core events are re-raised on the UI thread through <see cref="StateChanged"/>.
/// </summary>
public sealed class AppServices : IDisposable
{
    private readonly DBusConnection _system;
    private readonly BlueZRegistry _registry;

    public event Action? StateChanged;

    public BatteryMonitor Monitor { get; }
    public SettingsManager Settings { get; }
    public StatusNotifierHost? Tray { get; private set; }

    private AppServices(DBusConnection system, BlueZRegistry registry, SettingsManager settings, BatteryMonitor monitor)
    {
        _system = system;
        _registry = registry;
        Settings = settings;
        Monitor = monitor;
    }

    /// <param name="session">Session bus, shared with the single-instance check.</param>
    /// <param name="show">Opens the main window (tray left click and menu entry).</param>
    /// <param name="quit">Exits the application (tray menu entry).</param>
    public static async Task<AppServices> CreateAsync(DBusConnection session, Action show, Action quit)
    {
        var systemAddress = DBusAddress.System
            ?? throw new InvalidOperationException("The system D-Bus is not available.");
        var system = new DBusConnection(systemAddress);
        await system.ConnectAsync();

        var registry = new BlueZRegistry(system, action => Dispatcher.UIThread.Post(action));
        var source = new BlueZDeviceSource(registry);
        var reader = new BlueZBatteryReader(registry);

        var settings = new SettingsManager(autostart: new LinuxAutostart());
        var notifier = new FreedesktopNotifier(session);
        var notifications = new NotificationManager(settings.Settings, notifier.Show);

        var monitor = new BatteryMonitor(
            source,
            reader,
            notifications,
            settings,
            new LogitechDeviceScanner(),
            new AvaloniaUiDispatcher());

        var services = new AppServices(system, registry, settings, monitor);

        monitor.DeviceBatteryUpdated += _ => services.RaiseStateChanged();
        monitor.DeviceConnectionChanged += _ => services.RaiseStateChanged();
        monitor.DevicesCollectionChanged += services.RaiseStateChanged;

        services.Tray = await StatusNotifierHost.StartAsync(session, show, quit);
        services.StateChanged += services.RefreshTray;

        monitor.Start();
        return services;
    }

    private void RaiseStateChanged() => Dispatcher.UIThread.Post(() => StateChanged?.Invoke());

    private void RefreshTray() => Tray?.Update(Monitor.GetDevicesForTray());

    public void Dispose()
    {
        Monitor.Dispose();
        _registry.Dispose();
        _system.Dispose();
    }
}
