using Avalonia;
using Batto.Linux.Bluez;
using Tmds.DBus.Protocol;

namespace Batto.Linux;

public static class Program
{
    private const string ProbeArgument = "--probe-bluez";
    private static readonly TimeSpan ProbeDuration = TimeSpan.FromSeconds(3);

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains(ProbeArgument))
        {
            return RunBlueZProbeAsync().GetAwaiter().GetResult();
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();

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
