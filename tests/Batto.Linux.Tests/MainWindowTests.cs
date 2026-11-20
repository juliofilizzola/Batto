using Avalonia;
using Avalonia.Headless;
using Batto.Linux.UI;
using Batto.Models;
using Xunit;

namespace Batto.Linux.Tests;

/// <summary>Starts Avalonia once, headless, so the window can be rendered without a display.</summary>
internal static class HeadlessAvalonia
{
    private static readonly object Gate = new();
    private static bool _started;

    public static void EnsureStarted()
    {
        lock (Gate)
        {
            if (_started) return;
            AppBuilder.Configure<App>()
                .UseSkia()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .SetupWithoutStarting();
            _started = true;
        }
    }
}

public class MainWindowTests
{
    private const string ScreenshotFileName = "batto-linux-main-window.png";
    private const string SettingsScreenshotFileName = "batto-linux-settings.png";

    [Fact]
    public void ViewModel_LoadCountsConnectedAndTrayDevices()
    {
        var actions = new FakeActions();
        var viewModel = new MainViewModel();
        viewModel.Attach(actions, new Batto.Models.AppSettings());

        viewModel.Load(SampleDevices());

        Assert.Equal(4, viewModel.TotalDevices);
        Assert.Equal(3, viewModel.ConnectedDevices);
        Assert.Equal(4, viewModel.TrayDevices);
        Assert.Equal(4, viewModel.Devices.Count);
    }

    [Fact]
    public void TrayToggle_SendsChangeToActions()
    {
        var actions = new FakeActions();
        var viewModel = new MainViewModel();
        viewModel.Attach(actions, new Batto.Models.AppSettings());
        viewModel.Load(SampleDevices());
        var card = viewModel.Devices.First(c => c.Name == "MX Master 3S");

        card.IsTrayVisible = false;

        Assert.Equal(("MX Master 3S", false), actions.LastTrayChange);
    }

    [Fact]
    public void RenderMainWindow_WritesScreenshotForReview()
    {
        HeadlessAvalonia.EnsureStarted();

        var actions = new FakeActions();
        var viewModel = new MainViewModel();
        viewModel.Attach(actions, new Batto.Models.AppSettings());
        viewModel.Load(SampleDevices());

        var window = new UI.MainWindow(viewModel);
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();

        var frame = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)window.Width, (int)window.Height));
        frame.Render(window);

        string path = Path.Combine(Path.GetTempPath(), ScreenshotFileName);
        frame.Save(path, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        Assert.True(File.Exists(path));

        viewModel.IsSettingsTab = true;
        window.UpdateLayout();
        var settingsFrame = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)window.Width, (int)window.Height));
        settingsFrame.Render(window);
        string settingsPath = Path.Combine(Path.GetTempPath(), SettingsScreenshotFileName);
        settingsFrame.Save(settingsPath, new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
        Assert.True(File.Exists(settingsPath));
        window.Close();
    }

    private static IReadOnlyList<BluetoothDeviceModel> SampleDevices()
    {
        var now = DateTime.Now;
        return
        [
            Device("X11mouse2", DeviceType.Mouse, connected: true, level: 76, source: "BlueZ (Battery1)", now, priority: 1),
            Device("QQ2", DeviceType.Headphones, connected: false, level: 100, source: "BlueZ (Battery1)", now.AddMinutes(-4), priority: 1),
            Device("AK820 5.1-1", DeviceType.Keyboard, connected: true, level: 94, source: "BlueZ (Battery1)", now, priority: 0),
            Device("MX Master 3S", DeviceType.Mouse, connected: true, level: 19, source: "BlueZ (Battery1)", now, priority: 2),
        ];
    }

    private static BluetoothDeviceModel Device(string name, DeviceType type, bool connected, int level, string source, DateTime updated, int priority) => new()
    {
        Id = "bluez:" + name,
        Name = name,
        Type = type,
        IsConnected = connected,
        BatteryLevel = level,
        HasBatteryInfo = true,
        BatterySource = source,
        LastUpdated = updated,
        Priority = priority,
        IsTrayVisible = true,
    };

    private sealed class FakeActions : IDeviceActions
    {
        public (string Name, bool Visible)? LastTrayChange { get; private set; }

        public void SetTrayVisibility(BluetoothDeviceModel device, bool isVisible) =>
            LastTrayChange = (device.Name, isVisible);

        public void SetPriority(BluetoothDeviceModel device, int priority) { }

        public void SetDeviceType(BluetoothDeviceModel device, DeviceType type) { }

        public void SaveSettings() { }

        public void RefreshNow() { }

        public void MinimizeToTray() { }

        public void Quit() { }
    }
}
