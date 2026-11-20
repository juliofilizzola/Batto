using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Batto.Bluetooth;
using Batto.Logitech;
using Batto.Models;
using Batto.Notifications;
using Batto.Platform;
using Batto.Storage;

namespace Batto.Battery;

public class BatteryMonitor : IDisposable
{
    private const int MinRefreshIntervalSeconds = 15;
    private const int SecondsToMilliseconds = 1000;
    private const string LogitechDiscoveryContext = "LogitechDeviceScanner.DiscoverAsync";
    private static readonly TimeSpan BatteryReadTimeout = TimeSpan.FromSeconds(3);

    private readonly IBluetoothDeviceSource _deviceSource;
    private readonly IBatteryReader _batteryReader;
    private readonly NotificationManager _notificationManager;
    private readonly SettingsManager _settingsManager;
    private readonly LogitechDeviceScanner? _logitechScanner;
    private readonly IUiDispatcher _uiDispatcher;

    private readonly System.Timers.Timer _refreshTimer;
    private readonly ConcurrentDictionary<string, BluetoothDeviceModel> _deviceMap = new();
    private bool _isRefreshing;

    public ObservableCollection<BluetoothDeviceModel> ObservableDevices { get; } = new();

    public event Action<BluetoothDeviceModel>? DeviceBatteryUpdated;
    public event Action<BluetoothDeviceModel>? DeviceConnectionChanged;
    public event Action? DevicesCollectionChanged;

    public BatteryMonitor(
        IBluetoothDeviceSource deviceSource,
        IBatteryReader batteryReader,
        NotificationManager notificationManager,
        SettingsManager settingsManager,
        LogitechDeviceScanner? logitechScanner = null,
        IUiDispatcher? uiDispatcher = null)
    {
        _deviceSource = deviceSource;
        _batteryReader = batteryReader;
        _notificationManager = notificationManager;
        _settingsManager = settingsManager;
        _logitechScanner = logitechScanner;
        _uiDispatcher = uiDispatcher ?? ImmediateUiDispatcher.Instance;

        // Platform push updates (real-time BLE notifications on Windows)
        _batteryReader.BatteryPushed += OnBatteryPushed;

        // Platform device discovery events
        _deviceSource.DeviceDiscovered += OnBluetoothDeviceDiscovered;
        _deviceSource.DeviceUpdated += OnBluetoothDeviceUpdated;
        _deviceSource.DeviceRemoved += OnBluetoothDeviceRemoved;

        // Background refresh timer
        int intervalMs = Math.Max(MinRefreshIntervalSeconds, _settingsManager.Settings.RefreshIntervalSeconds) * SecondsToMilliseconds;
        _refreshTimer = new System.Timers.Timer(intervalMs)
        {
            AutoReset = true,
            Enabled = true
        };
        _refreshTimer.Elapsed += async (s, e) => await RefreshAllDevicesAsync();
    }

    public void Start()
    {
        _deviceSource.StartDiscovery();
        _ = RefreshAllDevicesAsync();
    }

    public void UpdateRefreshInterval(int seconds)
    {
        _settingsManager.Settings.RefreshIntervalSeconds = seconds;
        _settingsManager.Save();
        _refreshTimer.Interval = Math.Max(MinRefreshIntervalSeconds, seconds) * SecondsToMilliseconds;
    }

    private void OnBluetoothDeviceDiscovered(BluetoothDeviceModel device)
    {
        ApplyDevicePreferences(device);
        _deviceMap[device.Id] = device;

        RunOnUIThread(() =>
        {
            if (!ObservableDevices.Any(d => d.Id == device.Id))
            {
                ObservableDevices.Add(device);
                SortObservableDevices();
            }
        });

        DevicesCollectionChanged?.Invoke();

        // Query battery immediately for discovered device
        _ = QueryDeviceBatteryAsync(device);
    }

    private void OnBluetoothDeviceUpdated(BluetoothDeviceModel device)
    {
        ApplyDevicePreferences(device);
        _deviceMap[device.Id] = device;

        _notificationManager.CheckBatteryAlerts(device);
        _notificationManager.NotifyConnectionChanged(device, device.IsConnected);

        DeviceBatteryUpdated?.Invoke(device);
        DeviceConnectionChanged?.Invoke(device);
        DevicesCollectionChanged?.Invoke();
    }

    private void OnBluetoothDeviceRemoved(string deviceId)
    {
        if (_deviceMap.TryRemove(deviceId, out var removed))
        {
            _batteryReader.Forget(deviceId);

            RunOnUIThread(() =>
            {
                var existing = ObservableDevices.FirstOrDefault(d => d.Id == deviceId);
                if (existing != null)
                {
                    ObservableDevices.Remove(existing);
                }
            });

            _notificationManager.NotifyConnectionChanged(removed, false);
            DevicesCollectionChanged?.Invoke();
        }
    }

    private void OnBatteryPushed(string deviceId, BatteryReading reading)
    {
        if (_deviceMap.TryGetValue(deviceId, out var device))
        {
            ApplyReading(device, reading);
        }
    }

    public async Task RefreshAllDevicesAsync()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;

        try
        {
            await SyncLogitechDevicesAsync();

            var tasks = _deviceMap.Values.Select(device => QueryDeviceBatteryAsync(device)).ToList();
            await Task.WhenAll(tasks);
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    /// <summary>Adds Logitech devices paired to a receiver, which the OS does not list as Bluetooth devices.</summary>
    private async Task SyncLogitechDevicesAsync()
    {
        if (_logitechScanner == null) return;

        IReadOnlyList<LogitechSlot> slots;
        try
        {
            slots = await _logitechScanner.DiscoverAsync();
        }
        catch (Exception ex)
        {
            CrashLogger.Log(LogitechDiscoveryContext, ex);
            return;
        }

        foreach (var slot in slots)
        {
            if (_deviceMap.ContainsKey(slot.Id)) continue;

            var model = new BluetoothDeviceModel
            {
                Id = slot.Id,
                Name = slot.Name,
                Type = DeviceClassifier.ClassifyDeviceType(slot.Name),
                IsConnected = true
            };
            OnBluetoothDeviceDiscovered(model);
        }
    }

    public List<BluetoothDeviceModel> GetDevicesForTray()
    {
        return _deviceMap.Values
            .Where(d => d.IsTrayVisible)
            .OrderByDescending(d => d.Priority)
            .ThenByDescending(d => d.IsConnected)
            .ToList();
    }

    public async Task QueryDeviceBatteryAsync(BluetoothDeviceModel device)
    {
        try
        {
            if (_logitechScanner != null && LogitechDeviceScanner.IsLogitechId(device.Id))
            {
                await QueryLogitechBatteryAsync(device);
                return;
            }

            // Platform sources are tried in the order the platform defines (see IBatteryReader implementations)
            var reading = await _batteryReader.ReadAsync(device.Id, BatteryReadTimeout);
            if (reading != null)
            {
                ApplyReading(device, reading);
                return;
            }

            // Device responded without battery info
            if (!device.BatteryLevel.HasValue)
            {
                device.HasBatteryInfo = false;
            }
        }
        catch (Exception ex)
        {
            CrashLogger.Log($"QueryDeviceBatteryAsync({device.Name})", ex);
        }
    }

    private async Task QueryLogitechBatteryAsync(BluetoothDeviceModel device)
    {
        int? level = await _logitechScanner!.ReadBatteryAsync(device.Id);

        // A silent poll alone is not a disconnect: keep the device connected for a grace period after its last reply
        bool connected = LogitechPresence.IsConsideredConnected(level.HasValue, device.LastUpdated, DateTime.Now);

        if (device.IsConnected != connected)
        {
            device.IsConnected = connected;
            _notificationManager.NotifyConnectionChanged(device, connected);
            DeviceConnectionChanged?.Invoke(device);
        }

        if (level.HasValue)
        {
            ApplyReading(device, new BatteryReading(level.Value, "Logitech HID++"));
        }
    }

    private void ApplyReading(BluetoothDeviceModel device, BatteryReading reading)
    {
        device.BatteryLevel = reading.Level;
        device.HasBatteryInfo = true;
        device.BatterySource = reading.Source;
        device.LastUpdated = DateTime.Now;
        _notificationManager.CheckBatteryAlerts(device);
        DeviceBatteryUpdated?.Invoke(device);
    }

    public void SetDeviceTrayVisibility(BluetoothDeviceModel device, bool isVisible)
    {
        device.IsTrayVisible = isVisible;
        var pref = _settingsManager.Settings.GetPreference(device.Id);
        pref.IsVisibleInTray = isVisible;
        _settingsManager.Save();

        DeviceBatteryUpdated?.Invoke(device);
    }

    public void SetDeviceType(BluetoothDeviceModel device, DeviceType type)
    {
        device.Type = type;
        var pref = _settingsManager.Settings.GetPreference(device.Id);
        pref.TypeOverride = type;
        _settingsManager.Save();

        DeviceBatteryUpdated?.Invoke(device);
    }

    public void SetDevicePriority(BluetoothDeviceModel device, int priority)
    {
        device.Priority = priority;
        var pref = _settingsManager.Settings.GetPreference(device.Id);
        pref.Priority = priority;
        _settingsManager.Save();

        SortObservableDevices();
        DeviceBatteryUpdated?.Invoke(device);
    }

    private void ApplyDevicePreferences(BluetoothDeviceModel device)
    {
        if (_settingsManager.Settings.DevicePreferences.TryGetValue(device.Id, out var pref))
        {
            device.IsTrayVisible = pref.IsVisibleInTray;
            device.Priority = pref.Priority;
            if (pref.TypeOverride.HasValue)
            {
                device.Type = pref.TypeOverride.Value;
            }
            if (!string.IsNullOrEmpty(pref.CustomDisplayName))
            {
                device.Name = pref.CustomDisplayName;
            }
        }
    }

    private void SortObservableDevices()
    {
        RunOnUIThread(() =>
        {
            var sorted = ObservableDevices.OrderByDescending(d => d.Priority)
                                          .ThenByDescending(d => d.IsConnected)
                                          .ThenBy(d => d.Name)
                                          .ToList();

            for (int i = 0; i < sorted.Count; i++)
            {
                int oldIndex = ObservableDevices.IndexOf(sorted[i]);
                if (oldIndex != i && oldIndex >= 0)
                {
                    ObservableDevices.Move(oldIndex, i);
                }
            }
        });
    }

    private void RunOnUIThread(Action action) => _uiDispatcher.Invoke(action);

    public void Dispose()
    {
        _refreshTimer.Dispose();
        _batteryReader.BatteryPushed -= OnBatteryPushed;
        _deviceSource.DeviceDiscovered -= OnBluetoothDeviceDiscovered;
        _deviceSource.DeviceUpdated -= OnBluetoothDeviceUpdated;
        _deviceSource.DeviceRemoved -= OnBluetoothDeviceRemoved;
        _deviceSource.Dispose();
        _batteryReader.Dispose();
    }
}
