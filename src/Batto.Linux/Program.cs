using Avalonia;
using Batto.Linux.Bluez;
using Tmds.DBus.Protocol;

namespace Batto.Linux;

public static class Program
{
    private const string ProbeArgument = "--probe-bluez";
    private const string NotifyProbeArgument = "--probe-notify";
    private const string MinimizedArgument = "--minimized";
    private const string InstanceBusName = "org.batto.Batto";
    private const string AlreadyRunningMessage = "O Batto já está em execução.";
    private const string NoSessionBusMessage = "O barramento de sessão D-Bus não está disponível.";
    private static readonly TimeSpan ProbeDuration = TimeSpan.FromSeconds(3);

    /// <summary>True when launched by the autostart entry, so the window stays hidden and only the tray shows.</summary>
    public static bool StartMinimized { get; private set; }

    /// <summary>The session bus connection, shared by the tray, notifications and the single-instance name.</summary>
    public static DBusConnection? SessionConnection { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains(ProbeArgument))
        {
            return RunBlueZProbeAsync().GetAwaiter().GetResult();
        }

        if (args.Contains(NotifyProbeArgument))
        {
            return RunNotificationProbeAsync().GetAwaiter().GetResult();
        }

        StartMinimized = args.Contains(MinimizedArgument);
        RegisterCrashLogging();

        if (!AcquireSingleInstanceAsync().GetAwaiter().GetResult())
        {
            return 0;
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>Writes unhandled exceptions from any thread (including D-Bus dispatch) to the crash log before the process ends.</summary>
    private static void RegisterCrashLogging()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            CrashLogger.Log("AppDomain.UnhandledException", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            CrashLogger.Log("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();

    /// <summary>Claims a well-known session bus name. A second instance gets false and exits.</summary>
    private static async Task<bool> AcquireSingleInstanceAsync()
    {
        if (DBusAddress.Session is not { } sessionAddress)
        {
            Console.Error.WriteLine(NoSessionBusMessage);
            return false;
        }

        var connection = new DBusConnection(sessionAddress);
        await connection.ConnectAsync();

        bool primary = await connection.TryRequestNameAsync(InstanceBusName, RequestNameOptions.None);
        if (!primary)
        {
            connection.Dispose();
            Console.Error.WriteLine(AlreadyRunningMessage);
            return false;
        }

        SessionConnection = connection;
        return true;
    }

    /// <summary>Sends one real notification through the freedesktop path and prints the id the server returned.</summary>
    private static async Task<int> RunNotificationProbeAsync()
    {
        if (DBusAddress.Session is not { } sessionBus)
        {
            Console.Error.WriteLine(NoSessionBusMessage);
            return 1;
        }

        using var connection = new DBusConnection(sessionBus);
        await connection.ConnectAsync();

        var notifier = new Notifications.FreedesktopNotifier(connection);
        uint id = await notifier.ShowAsync(new Batto.Notifications.NotificationPayload(
            "Batto: teste de notificação",
            "Se você está lendo isto, a notificação do Batto chegou ao desktop.",
            IsWarning: false));

        Console.WriteLine(id == 0 ? "notification NOT delivered (see crash log)" : $"notification delivered, server id={id}");
        return id == 0 ? 1 : 0;
    }

    /// <summary>Prints what the BlueZ layer sees on the system bus. Diagnostic only, it does not start the UI.</summary>
    private static async Task<int> RunBlueZProbeAsync()
    {
        if (DBusAddress.System is not { } systemBus)
        {
            Console.Error.WriteLine("System bus is not available.");
            return 1;
        }

        using var connection = new DBusConnection(systemBus);
        await connection.ConnectAsync();

        using var registry = new BlueZRegistry(connection, action => action());
        using var source = new BlueZDeviceSource(registry);
        using var reader = new BlueZBatteryReader(registry);

        source.DeviceDiscovered += device =>
            Console.WriteLine($"device  {device.Id}  '{device.Name}'  type={device.Type}  connected={device.IsConnected}");
        source.DeviceUpdated += device =>
            Console.WriteLine($"update  {device.Id}  '{device.Name}'  connected={device.IsConnected}");
        source.DeviceRemoved += id => Console.WriteLine($"removed {id}");
        reader.BatteryPushed += (id, battery) =>
            Console.WriteLine($"battery {id}  {battery.Level}%  ({battery.Source})");

        try
        {
            await registry.StartAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"BlueZ is not reachable: {ex.Message}");
            return 1;
        }

        await Task.Delay(ProbeDuration);
        return 0;
    }
}
