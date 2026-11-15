using Batto.Models;
using Batto.Notifications;
using Xunit;

namespace Batto.Tests;

public class NotificationManagerTests
{
    [Fact]
    public void CheckBatteryAlerts_TriggersLowBatteryAlert_OncePerDischarge()
    {
        var settings = new AppSettings
        {
            NotifyLowBattery = true,
            NotifyCriticalBattery = true
        };

        var notifications = new List<NotificationPayload>();
        var manager = new NotificationManager(settings, n => notifications.Add(n));

        var device = new BluetoothDeviceModel
        {
            Id = "device_1",
            Name = "Mouse Gamer",
            IsConnected = true,
            BatteryLevel = 19
        };

        // First trigger for 19% (< 20%)
        manager.CheckBatteryAlerts(device);
        Assert.Single(notifications);
        Assert.Contains("Bateria Baixa", notifications[0].Title);

        // Second check with 18% should NOT trigger again (debounced / once per cycle)
        device.BatteryLevel = 18;
        manager.CheckBatteryAlerts(device);
        Assert.Single(notifications);

        // Battery drops to critical (< 10%)
        device.BatteryLevel = 8;
        manager.CheckBatteryAlerts(device);
        Assert.Equal(2, notifications.Count);
        Assert.Contains("Bateria Crítica", notifications[1].Title);

        // Recharge to 50% resets state
        device.BatteryLevel = 50;
        manager.CheckBatteryAlerts(device);

        // Now drops to 15% again -> should trigger low battery again because it was recharged
        device.BatteryLevel = 15;
        manager.CheckBatteryAlerts(device);
        Assert.Equal(3, notifications.Count);
    }

    [Fact]
    public void NotifyConnectionChanged_DebouncesRapidJitter()
    {
        var settings = new AppSettings
        {
            NotifyConnected = true,
            NotifyDisconnected = true
        };

        var notifications = new List<NotificationPayload>();
        var manager = new NotificationManager(settings, n => notifications.Add(n));

        var device = new BluetoothDeviceModel
        {
            Id = "device_test",
            Name = "Headset",
            IsConnected = true
        };

        // First connect event
        manager.NotifyConnectionChanged(device, true);
        Assert.Single(notifications);
        Assert.Contains("Conectado", notifications[0].Title);

        // Rapid duplicate call within milliseconds
        manager.NotifyConnectionChanged(device, true);
        Assert.Single(notifications); // Suppressed
    }
}
