using Batto.Models;
using Tmds.DBus.Protocol;

namespace Batto.Linux.Tray;

/// <summary>
/// Puts Batto in the desktop tray: exports the StatusNotifierItem and its menu on the session bus,
/// then registers the item with org.kde.StatusNotifierWatcher (KDE Plasma, or GNOME with the AppIndicator extension).
/// </summary>
public sealed class StatusNotifierHost
{
    private const string WatcherService = "org.kde.StatusNotifierWatcher";
    private const string WatcherPath = "/StatusNotifierWatcher";
    private const string WatcherInterface = "org.kde.StatusNotifierWatcher";
    private const string RegisterMember = "RegisterStatusNotifierItem";
    private const string ItemNameFormat = "org.kde.StatusNotifierItem-{0}-1";
    private const string NoDevicesText = "Nenhum dispositivo conectado";
    private const string TooltipLineFormat = "{0}: {1}%";
    private const string TooltipUnknownLevel = "{0}: sem leitura";

    private readonly DBusConnection _session;
    private readonly StatusNotifierItem _item;

    private StatusNotifierHost(DBusConnection session, StatusNotifierItem item)
    {
        _session = session;
        _item = item;
    }

    /// <summary>Exports the item and registers it. Returns null when no tray host is listening (no SNI watcher on the bus).</summary>
    public static async Task<StatusNotifierHost?> StartAsync(DBusConnection session, Action open, Action quit)
    {
        string name = string.Format(ItemNameFormat, Environment.ProcessId);
        await session.RequestNameAsync(name, RequestNameOptions.ReplaceExisting);

        var item = new StatusNotifierItem(session) { Activated = open };
        session.AddMethodHandler(item);
        session.AddMethodHandler(new DBusMenu { Open = open, Quit = quit });

        if (!await RegisterWithWatcherAsync(session, name)) return null;

        return new StatusNotifierHost(session, item);
    }

    /// <summary>Redraws the icon and tooltip from the devices that are visible in the tray.</summary>
    public void Update(IReadOnlyList<BluetoothDeviceModel> trayDevices)
    {
        _item.Update(TrayIconRenderer.RenderAllSizes(trayDevices), BuildTooltip(trayDevices));
    }

    private static string BuildTooltip(IReadOnlyList<BluetoothDeviceModel> devices)
    {
        var lines = devices
            .Where(d => d.IsConnected)
            .Select(d => d.BatteryLevel.HasValue
                ? string.Format(TooltipLineFormat, d.Name, d.BatteryLevel.Value)
                : string.Format(TooltipUnknownLevel, d.Name))
            .ToList();

        return lines.Count == 0 ? NoDevicesText : string.Join(Environment.NewLine, lines);
    }

    private static async Task<bool> RegisterWithWatcherAsync(DBusConnection session, string itemName)
    {
        try
        {
            var request = BuildRegisterMessage(session, itemName);
            await session.CallMethodAsync(request, (Message message, object? state) => true, null);
            return true;
        }
        catch (Exception ex)
        {
            CrashLogger.Log("StatusNotifierWatcher registration", ex);
            return false;
        }
    }

    private static MessageBuffer BuildRegisterMessage(DBusConnection session, string itemName)
    {
        using var writer = session.GetMessageWriter();
        writer.WriteMethodCallHeader(
            destination: WatcherService,
            path: WatcherPath,
            @interface: WatcherInterface,
            member: RegisterMember,
            signature: "s");
        writer.WriteString(itemName);
        return writer.CreateMessage();
    }
}
