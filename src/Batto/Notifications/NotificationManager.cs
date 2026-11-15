using System.Collections.Concurrent;
using Batto.Models;

namespace Batto.Notifications;

public record NotificationPayload(string Title, string Message, bool IsWarning);

public class NotificationManager
{
    private readonly AppSettings _settings;
    private readonly Action<NotificationPayload>? _notifyAction;

    // Track state to avoid spam
    private readonly ConcurrentDictionary<string, bool> _alertedLowBattery = new();
    private readonly ConcurrentDictionary<string, bool> _alertedCriticalBattery = new();
    private readonly ConcurrentDictionary<string, (bool isConnected, DateTime lastTime)> _lastConnectionState = new();

    public event Action<NotificationPayload>? NotificationTriggered;

    public NotificationManager(AppSettings settings, Action<NotificationPayload>? notifyAction = null)
    {
        _settings = settings;
        _notifyAction = notifyAction;
    }

    public void CheckBatteryAlerts(BluetoothDeviceModel device)
    {
        if (!device.IsConnected || !device.BatteryLevel.HasValue) return;

        int level = device.BatteryLevel.Value;
        string deviceId = device.Id;

        // Reset alert flags if user charged the device
        if (level > 25)
        {
            _alertedLowBattery.TryRemove(deviceId, out _);
            _alertedCriticalBattery.TryRemove(deviceId, out _);
            return;
        }

        // Critical battery (< 10%)
        if (level < 10 && _settings.NotifyCriticalBattery)
        {
            if (_alertedCriticalBattery.TryAdd(deviceId, true))
            {
                Send(new NotificationPayload(
                    "Bateria Crítica!",
                    $"{device.TypeGlyph} {device.Name} está com apenas {level}% de bateria.",
                    IsWarning: true));
            }
            return;
        }

        // Low battery (< 20%)
        if (level < 20 && _settings.NotifyLowBattery)
        {
            if (_alertedLowBattery.TryAdd(deviceId, true))
            {
                Send(new NotificationPayload(
                    "Bateria Baixa",
                    $"{device.TypeGlyph} {device.Name} está com {level}% de bateria.",
                    IsWarning: false));
            }
        }
    }

    public void NotifyConnectionChanged(BluetoothDeviceModel device, bool isConnected)
    {
        string deviceId = device.Id;
        var now = DateTime.UtcNow;

        if (_lastConnectionState.TryGetValue(deviceId, out var state))
        {
            // Debounce rapid bouncing within 8 seconds
            if (state.isConnected == isConnected || (now - state.lastTime).TotalSeconds < 8)
            {
                _lastConnectionState[deviceId] = (isConnected, now);
                return;
            }
        }

        _lastConnectionState[deviceId] = (isConnected, now);

        if (isConnected && _settings.NotifyConnected)
        {
            string batteryText = device.BatteryLevel.HasValue ? $" ({device.BatteryLevel.Value}%)" : "";
            Send(new NotificationPayload(
                "Dispositivo Conectado",
                $"{device.TypeGlyph} {device.Name} foi conectado{batteryText}.",
                IsWarning: false));
        }
        else if (!isConnected && _settings.NotifyDisconnected)
        {
            Send(new NotificationPayload(
                "Dispositivo Desconectado",
                $"{device.TypeGlyph} {device.Name} foi desconectado.",
                IsWarning: false));
        }
    }

    private void Send(NotificationPayload payload)
    {
        NotificationTriggered?.Invoke(payload);
        _notifyAction?.Invoke(payload);
    }
}
