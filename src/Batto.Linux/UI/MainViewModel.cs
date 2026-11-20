using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows.Input;
using Batto.Models;

namespace Batto.Linux.UI;

/// <summary>State of the main window: the device cards, the summary chips, the tabs and the settings.</summary>
public sealed class MainViewModel : ViewModelBase
{
    private const string ReadyMessage = "Pronto. Monitorando em segundo plano.";
    private const string ProductName = "Batto";

    private IDeviceActions? _actions;
    private AppSettings _settings = new();
    private int _totalDevices;
    private int _connectedDevices;
    private int _trayDevices;
    private bool _isDevicesTab = true;
    private string _statusMessage = ReadyMessage;

    public MainViewModel()
    {
        RefreshCommand = new RelayCommand(() => _actions?.RefreshNow());
        MinimizeCommand = new RelayCommand(() => _actions?.MinimizeToTray());
        QuitCommand = new RelayCommand(() => _actions?.Quit());
    }

    /// <summary>Connects the view model to the running services. Until then the window shows defaults and no devices.</summary>
    public void Attach(IDeviceActions actions, AppSettings settings)
    {
        _actions = actions;
        _settings = settings;
        Raise(nameof(RefreshIntervalSeconds));
        Raise(nameof(StartWithSystem));
        Raise(nameof(StartMinimized));
        Raise(nameof(NotifyLowBattery));
        Raise(nameof(NotifyCriticalBattery));
        Raise(nameof(NotifyConnected));
        Raise(nameof(NotifyDisconnected));
    }

    public ObservableCollection<DeviceCardViewModel> Devices { get; } = new();

    /// <summary>Choices for the refresh interval, in seconds. The Windows app takes a free value; the Linux UI offers a list.</summary>
    public IReadOnlyList<int> RefreshIntervalOptions { get; } = [15, 30, 60, 120, 300];

    public int TotalDevices
    {
        get => _totalDevices;
        private set => SetField(ref _totalDevices, value);
    }

    public int ConnectedDevices
    {
        get => _connectedDevices;
        private set => SetField(ref _connectedDevices, value);
    }

    public int TrayDevices
    {
        get => _trayDevices;
        private set => SetField(ref _trayDevices, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string VersionText => $"{ProductName} {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";

    public bool IsDevicesTab
    {
        get => _isDevicesTab;
        set
        {
            if (!SetField(ref _isDevicesTab, value)) return;
            Raise(nameof(IsSettingsTab));
        }
    }

    public bool IsSettingsTab
    {
        get => !_isDevicesTab;
        set => IsDevicesTab = !value;
    }

    public ICommand RefreshCommand { get; }
    public ICommand MinimizeCommand { get; }
    public ICommand QuitCommand { get; }

    /// <summary>Rebuilds the cards from the current device list. Called on the UI thread.</summary>
    public void Load(IReadOnlyList<BluetoothDeviceModel> devices)
    {
        Devices.Clear();
        foreach (var device in devices)
        {
            Devices.Add(new DeviceCardViewModel(device, _actions!));
        }

        TotalDevices = devices.Count;
        ConnectedDevices = devices.Count(d => d.IsConnected);
        TrayDevices = devices.Count(d => d.IsTrayVisible);
    }

    public int RefreshIntervalSeconds
    {
        get => _settings.RefreshIntervalSeconds;
        set
        {
            if (_settings.RefreshIntervalSeconds == value) return;
            _settings.RefreshIntervalSeconds = value;
            Save();
            Raise();
        }
    }

    public bool StartWithSystem
    {
        get => _settings.StartWithWindows;
        set => ChangeSetting(() => _settings.StartWithWindows, v => _settings.StartWithWindows = v, value);
    }

    public bool StartMinimized
    {
        get => _settings.StartMinimized;
        set => ChangeSetting(() => _settings.StartMinimized, v => _settings.StartMinimized = v, value);
    }

    public bool NotifyLowBattery
    {
        get => _settings.NotifyLowBattery;
        set => ChangeSetting(() => _settings.NotifyLowBattery, v => _settings.NotifyLowBattery = v, value);
    }

    public bool NotifyCriticalBattery
    {
        get => _settings.NotifyCriticalBattery;
        set => ChangeSetting(() => _settings.NotifyCriticalBattery, v => _settings.NotifyCriticalBattery = v, value);
    }

    public bool NotifyConnected
    {
        get => _settings.NotifyConnected;
        set => ChangeSetting(() => _settings.NotifyConnected, v => _settings.NotifyConnected = v, value);
    }

    public bool NotifyDisconnected
    {
        get => _settings.NotifyDisconnected;
        set => ChangeSetting(() => _settings.NotifyDisconnected, v => _settings.NotifyDisconnected = v, value);
    }

    private void ChangeSetting(Func<bool> read, Action<bool> write, bool value)
    {
        if (read() == value) return;
        write(value);
        Save();
        Raise(nameof(StartWithSystem));
        Raise(nameof(StartMinimized));
        Raise(nameof(NotifyLowBattery));
        Raise(nameof(NotifyCriticalBattery));
        Raise(nameof(NotifyConnected));
        Raise(nameof(NotifyDisconnected));
    }

    private void Save() => _actions?.SaveSettings();
}
