using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Batto.Battery;
using Batto.Models;
using Batto.Storage;

namespace Batto.ViewModels;

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute == null || _canExecute();
    public void Execute(object? parameter) => _execute();
}

public class MainViewModel : INotifyPropertyChanged
{
    private readonly BatteryMonitor _batteryMonitor;
    private readonly SettingsManager _settingsManager;
    private readonly Action _exitAction;

    private bool _isScanning;
    private string _statusMessage = "Pronto. Monitorando em segundo plano.";
    private int _selectedTabIndex;

    public ObservableCollection<BluetoothDeviceModel> Devices => _batteryMonitor.ObservableDevices;

    public int TotalDevicesCount
    {
        get => Devices.Count;
        set { }
    }

    public int ConnectedDevicesCount
    {
        get => Devices.Count(d => d.IsConnected);
        set { }
    }

    public int TrayVisibleCount
    {
        get => Devices.Count(d => d.IsTrayVisible);
        set { }
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetField(ref _selectedTabIndex, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        set => SetField(ref _isScanning, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public int RefreshIntervalSeconds
    {
        get => _settingsManager.Settings.RefreshIntervalSeconds;
        set
        {
            if (_settingsManager.Settings.RefreshIntervalSeconds != value)
            {
                _batteryMonitor.UpdateRefreshInterval(value);
                OnPropertyChanged();
            }
        }
    }

    public bool StartWithWindows
    {
        get => _settingsManager.Settings.StartWithWindows;
        set
        {
            if (_settingsManager.Settings.StartWithWindows != value)
            {
                _settingsManager.Settings.StartWithWindows = value;
                _settingsManager.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool StartMinimized
    {
        get => _settingsManager.Settings.StartMinimized;
        set
        {
            if (_settingsManager.Settings.StartMinimized != value)
            {
                _settingsManager.Settings.StartMinimized = value;
                _settingsManager.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool PromoteTrayIcons
    {
        get => _settingsManager.Settings.PromoteTrayIcons;
        set
        {
            if (_settingsManager.Settings.PromoteTrayIcons != value)
            {
                _settingsManager.Settings.PromoteTrayIcons = value;
                _settingsManager.Save();
                if (value)
                {
                    Tray.TrayPromotion.PromoteOwnIcons();
                }
                OnPropertyChanged();
            }
        }
    }

    public bool NotifyLowBattery
    {
        get => _settingsManager.Settings.NotifyLowBattery;
        set
        {
            if (_settingsManager.Settings.NotifyLowBattery != value)
            {
                _settingsManager.Settings.NotifyLowBattery = value;
                _settingsManager.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool NotifyCriticalBattery
    {
        get => _settingsManager.Settings.NotifyCriticalBattery;
        set
        {
            if (_settingsManager.Settings.NotifyCriticalBattery != value)
            {
                _settingsManager.Settings.NotifyCriticalBattery = value;
                _settingsManager.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool NotifyConnected
    {
        get => _settingsManager.Settings.NotifyConnected;
        set
        {
            if (_settingsManager.Settings.NotifyConnected != value)
            {
                _settingsManager.Settings.NotifyConnected = value;
                _settingsManager.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool NotifyDisconnected
    {
        get => _settingsManager.Settings.NotifyDisconnected;
        set
        {
            if (_settingsManager.Settings.NotifyDisconnected != value)
            {
                _settingsManager.Settings.NotifyDisconnected = value;
                _settingsManager.Save();
                OnPropertyChanged();
            }
        }
    }

    public ICommand RefreshCommand { get; }
    public ICommand OpenTaskbarSettingsCommand { get; }
    public ICommand OpenBluetoothSettingsCommand { get; }
    public ICommand ExitAppCommand { get; }
    public ICommand OpenRepositoryCommand { get; }

    public string AppVersionText
    {
        get
        {
            var version = typeof(MainViewModel).Assembly.GetName().Version;
            return version is null ? "Versão desconhecida" : $"Versão {version.ToString(3)}";
        }
    }

    public MainViewModel(
        BatteryMonitor batteryMonitor,
        SettingsManager settingsManager,
        Action exitAction)
    {
        _batteryMonitor = batteryMonitor;
        _settingsManager = settingsManager;
        _exitAction = exitAction;

        RefreshCommand = new RelayCommand(async () => await RefreshAllAsync());
        OpenTaskbarSettingsCommand = new RelayCommand(OpenTaskbarSettings);
        OpenBluetoothSettingsCommand = new RelayCommand(OpenBluetoothSettings);
        OpenRepositoryCommand = new RelayCommand(OpenRepository);
        ExitAppCommand = new RelayCommand(_exitAction);

        _batteryMonitor.DevicesCollectionChanged += () =>
        {
            OnPropertyChanged(nameof(TotalDevicesCount));
            OnPropertyChanged(nameof(ConnectedDevicesCount));
            OnPropertyChanged(nameof(TrayVisibleCount));
        };
        _batteryMonitor.DeviceBatteryUpdated += (dev) =>
        {
            OnPropertyChanged(nameof(ConnectedDevicesCount));
            OnPropertyChanged(nameof(TrayVisibleCount));
        };
    }

    public async Task RefreshAllAsync()
    {
        IsScanning = true;
        StatusMessage = "Atualizando níveis de bateria...";
        try
        {
            await _batteryMonitor.RefreshAllDevicesAsync();
            StatusMessage = $"Atualizado às {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao atualizar: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    public void ToggleDeviceTray(BluetoothDeviceModel device)
    {
        _batteryMonitor.SetDeviceTrayVisibility(device, device.IsTrayVisible);
        OnPropertyChanged(nameof(TrayVisibleCount));
    }

    public void ChangeDeviceType(BluetoothDeviceModel device, DeviceType type)
    {
        _batteryMonitor.SetDeviceType(device, type);
    }

    public void ChangeDevicePriority(BluetoothDeviceModel device, int delta)
    {
        _batteryMonitor.SetDevicePriority(device, device.Priority + delta);
        OnPropertyChanged(nameof(Devices));
    }

    private void OpenTaskbarSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:taskbar") { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private void OpenRepository()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/juliofilizzola/Batto") { UseShellExecute = true });
        }
        catch
        {
        }
    }

    private void OpenBluetoothSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:bluetooth") { UseShellExecute = true });
        }
        catch
        {
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
