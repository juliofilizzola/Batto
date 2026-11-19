using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Windows;
using Batto.Bluetooth;
using Batto.Logitech;
using Batto.Models;
using Batto.Notifications;
using Batto.Storage;

namespace Batto.Battery;

public class BatteryMonitor : IDisposable
{
    private readonly BluetoothManager _bluetoothManager;
    private readonly GattBatteryReader _gattReader;
    private readonly PnpBatteryReader _pnpReader;
    private readonly NotificationManager _notificationManager;
    private readonly SettingsManager _settingsManager;
    private readonly LogitechDeviceScanner? _logitechScanner;

    private readonly System.Timers.Timer _refreshTimer;
    private readonly ConcurrentDictionary<string, BluetoothDeviceModel> _deviceMap = new();
    private bool _isRefreshing;

    public ObservableCollection<BluetoothDeviceModel> ObservableDevices { get; } = new();

    public event Action<BluetoothDeviceModel>? DeviceBatteryUpdated;
    public event Action<BluetoothDeviceModel>? DeviceConnectionChanged;
    public event Action? DevicesCollectionChanged;

    public BatteryMonitor(
        BluetoothManager bluetoothManager,
        GattBatteryReader gattReader,
        PnpBatteryReader pnpReader,
        NotificationManager notificationManager,
        SettingsManager settingsManager,
        LogitechDeviceScanner? logitechScanner = null)
    {
        _bluetoothManager = bluetoothManager;
        _gattReader = gattReader;
        _pnpReader = pnpReader;
        _notificationManager = notificationManager;
        _settingsManager = settingsManager;
        _logitechScanner = logitechScanner;

        // Wire GATT push notifications (real-time BLE updates)
        _gattReader.BatteryLevelReceived += OnGattBatteryPush;

        // Wire Bluetooth discovery events
        _bluetoothManager.DeviceDiscovered += OnBluetoothDeviceDiscovered;
        _bluetoothManager.DeviceUpdated += OnBluetoothDeviceUpdated;
        _bluetoothManager.DeviceRemoved += OnBluetoothDeviceRemoved;

        // Background refresh timer
        int intervalMs = Math.Max(15, _settingsManager.Settings.RefreshIntervalSeconds) * 1000;
        _refreshTimer = new System.Timers.Timer(intervalMs)
        {
            AutoReset = true,
            Enabled = true
        };
        _refreshTimer.Elapsed += async (s, e) => await RefreshAllDevicesAsync();
    }

    public void Start()
    {
        _bluetoothManager.StartDiscovery();
        _ = RefreshAllDevicesAsync();
    }

    public void UpdateRefreshInterval(int seconds)
    {
        _settingsManager.Settings.RefreshIntervalSeconds = seconds;
        _settingsManager.Save();
        _refreshTimer.Interval = Math.Max(15, seconds) * 1000;
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
            _gattReader.UnregisterDevice(deviceId);

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

    private void OnGattBatteryPush(string deviceId, int batteryLevel)
    {
        if (_deviceMap.TryGetValue(deviceId, out var device))
        {
            device.BatteryLevel = batteryLevel;
            device.HasBatteryInfo = true;
            device.BatterySource = "GATT (0x180F)";
            device.LastUpdated = DateTime.Now;

            _notificationManager.CheckBatteryAlerts(device);
            DeviceBatteryUpdated?.Invoke(device);
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

    /// <summary>Adds Logitech devices paired to a receiver, which Windows does not list as Bluetooth devices.</summary>
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
            CrashLogger.Log("LogitechDeviceScanner.DiscoverAsync", ex);
            return;
        }

        foreach (var slot in slots)
        {
            if (_deviceMap.ContainsKey(slot.Id)) continue;

            var model = new BluetoothDeviceModel
            {
                Id = slot.Id,
                Name = slot.Name,
                Type = BluetoothManager.ClassifyDeviceType(slot.Name),
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

            // 1. Try Windows PnP property first (fast, works for Classic Bluetooth & connected audio headsets)
            var pnpBattery = await _pnpReader.ReadBatteryAsync(device.Id);
            if (pnpBattery.HasValue)
            {
                device.BatteryLevel = pnpBattery.Value;
                device.HasBatteryInfo = true;
                device.BatterySource = "Windows PnP";
                device.LastUpdated = DateTime.Now;
                _notificationManager.CheckBatteryAlerts(device);
                DeviceBatteryUpdated?.Invoke(device);
                return;
            }

            // 2. Classic and BLE devices: Windows keeps the battery on a node with the same address.
            //    This is preferred over GATT because some keyboards report a stale GATT value.
            var address = PnpBatteryReader.ExtractClassicAddress(device.Id) ?? PnpBatteryReader.ExtractLeAddress(device.Id);
            if (address != null)
            {
                var serviceBattery = await _pnpReader.ReadBatteryByAddressAsync(address);
                if (serviceBattery.HasValue)
                {
                    device.BatteryLevel = serviceBattery.Value;
                    device.HasBatteryInfo = true;
                    device.BatterySource = "Windows PnP (serviço do dispositivo)";
                    device.LastUpdated = DateTime.Now;
                    _notificationManager.CheckBatteryAlerts(device);
                    DeviceBatteryUpdated?.Invoke(device);
                    return;
                }
            }

            // 3. Try GATT Battery Service 0x180F (for BLE devices, mice, keyboards) with 3s timeout
            var gattBattery = await _gattReader.ReadBatteryAsync(device.Id, TimeSpan.FromSeconds(3));
            if (gattBattery.HasValue)
            {
                device.BatteryLevel = gattBattery.Value;
                device.HasBatteryInfo = true;
                device.BatterySource = "GATT (0x180F)";
                device.LastUpdated = DateTime.Now;
                _notificationManager.CheckBatteryAlerts(device);
                DeviceBatteryUpdated?.Invoke(device);
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
            device.BatteryLevel = level.Value;
            device.HasBatteryInfo = true;
            device.BatterySource = "Logitech HID++";
            device.LastUpdated = DateTime.Now;
            _notificationManager.CheckBatteryAlerts(device);
            DeviceBatteryUpdated?.Invoke(device);
        }
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

    private static void RunOnUIThread(Action action)
    {
        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            Application.Current.Dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    public void Dispose()
    {
        _refreshTimer.Dispose();
        _gattReader.BatteryLevelReceived -= OnGattBatteryPush;
        _bluetoothManager.DeviceDiscovered -= OnBluetoothDeviceDiscovered;
        _bluetoothManager.DeviceUpdated -= OnBluetoothDeviceUpdated;
        _bluetoothManager.DeviceRemoved -= OnBluetoothDeviceRemoved;
        _bluetoothManager.Dispose();
        _gattReader.Dispose();
    }
}
