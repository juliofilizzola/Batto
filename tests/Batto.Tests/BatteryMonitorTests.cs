using Batto.Battery;
using Batto.Bluetooth;
using Batto.Models;
using Batto.Notifications;
using Batto.Storage;
using Xunit;

namespace Batto.Tests;

public class BatteryMonitorTests
{
    private const string DeviceId = "dev_test_1";
    private const int FakeLevel = 72;
    private const string FakeSource = "Fake source";

    [Fact]
    public async Task QueryDeviceBatteryAsync_AppliesReadingFromPlatformReader()
    {
        using var harness = new Harness();
        harness.Reader.Readings[DeviceId] = new BatteryReading(FakeLevel, FakeSource);
        var device = CreateDevice();

        await harness.Monitor.QueryDeviceBatteryAsync(device);

        Assert.Equal(FakeLevel, device.BatteryLevel);
        Assert.True(device.HasBatteryInfo);
        Assert.Equal(FakeSource, device.BatterySource);
    }

    [Fact]
    public async Task QueryDeviceBatteryAsync_WithoutReading_ClearsBatteryInfoWhenNoLevelIsKnown()
    {
        using var harness = new Harness();
        var device = CreateDevice();
        device.HasBatteryInfo = true;

        await harness.Monitor.QueryDeviceBatteryAsync(device);

        Assert.False(device.HasBatteryInfo);
    }

    [Fact]
    public void BatteryPushed_UpdatesKnownDevice()
    {
        using var harness = new Harness();
        var device = CreateDevice();
        harness.Source.Discover(device);

        harness.Reader.Push(DeviceId, new BatteryReading(FakeLevel, FakeSource));

        Assert.Equal(FakeLevel, device.BatteryLevel);
        Assert.Equal(FakeSource, device.BatterySource);
    }

    [Fact]
    public void GetDevicesForTray_ExcludesDisconnectedDevicesEvenWhenTrayVisible()
    {
        using var harness = new Harness();
        var device = CreateDevice();
        harness.Source.Discover(device);
        harness.Monitor.SetDeviceTrayVisibility(device, true);
        Assert.Single(harness.Monitor.GetDevicesForTray());

        device.IsConnected = false;

        Assert.Empty(harness.Monitor.GetDevicesForTray());
        Assert.True(device.IsTrayVisible);
    }

    [Fact]
    public void DeviceRemoved_ForgetsPlatformStateAndRemovesDevice()
    {
        using var harness = new Harness();
        harness.Source.Discover(CreateDevice());
        Assert.Single(harness.Monitor.ObservableDevices);

        harness.Source.Remove(DeviceId);

        Assert.Empty(harness.Monitor.ObservableDevices);
        Assert.Contains(DeviceId, harness.Reader.Forgotten);
    }

    private static BluetoothDeviceModel CreateDevice() => new()
    {
        Id = DeviceId,
        Name = "Test mouse",
        Type = DeviceType.Mouse,
        IsConnected = true
    };

    /// <summary>Builds a monitor wired to fake platform sources, so the core logic runs without WinRT.</summary>
    private sealed class Harness : IDisposable
    {
        public FakeDeviceSource Source { get; } = new();
        public FakeBatteryReader Reader { get; } = new();
        public BatteryMonitor Monitor { get; }

        public Harness()
        {
            string folder = Path.Combine(Path.GetTempPath(), "BattoTests_" + Guid.NewGuid().ToString("N"));
            var settings = new SettingsManager(folder);
            Monitor = new BatteryMonitor(Source, Reader, new NotificationManager(settings.Settings), settings);
        }

        public void Dispose() => Monitor.Dispose();
    }

    private sealed class FakeDeviceSource : IBluetoothDeviceSource
    {
        public event Action<BluetoothDeviceModel>? DeviceDiscovered;
        public event Action<BluetoothDeviceModel>? DeviceUpdated;
        public event Action<string>? DeviceRemoved;

        public void StartDiscovery() { }

        public void Discover(BluetoothDeviceModel device) => DeviceDiscovered?.Invoke(device);

        public void Update(BluetoothDeviceModel device) => DeviceUpdated?.Invoke(device);

        public void Remove(string deviceId) => DeviceRemoved?.Invoke(deviceId);

        public void Dispose() { }
    }

    private sealed class FakeBatteryReader : IBatteryReader
    {
        public event Action<string, BatteryReading>? BatteryPushed;

        public Dictionary<string, BatteryReading?> Readings { get; } = new();
        public List<string> Forgotten { get; } = new();

        public Task<BatteryReading?> ReadAsync(string deviceId, TimeSpan timeout) =>
            Task.FromResult<BatteryReading?>(Readings.TryGetValue(deviceId, out var reading) ? reading : null);

        public void Forget(string deviceId) => Forgotten.Add(deviceId);

        public void Push(string deviceId, BatteryReading reading) => BatteryPushed?.Invoke(deviceId, reading);

        public void Dispose() { }
    }
}
