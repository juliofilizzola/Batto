using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Batto.Models;

public class BluetoothDeviceModel : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _name = "Dispositivo Bluetooth";
    private DeviceType _type = DeviceType.Generic;
    private bool _isConnected;
    private int? _batteryLevel;
    private DateTime? _lastUpdated;
    private bool _isTrayVisible = true;
    private int _priority;
    private bool _hasBatteryInfo;
    private string _batterySource = string.Empty;
    private ulong _bluetoothAddress;

    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public DeviceType Type
    {
        get => _type;
        set
        {
            if (SetField(ref _type, value))
            {
                OnPropertyChanged(nameof(TypeGlyph));
            }
        }
    }

    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (SetField(ref _isConnected, value))
            {
                OnPropertyChanged(nameof(StatusDescription));
                OnPropertyChanged(nameof(StatusColorHex));
            }
        }
    }

    public int? BatteryLevel
    {
        get => _batteryLevel;
        set
        {
            if (SetField(ref _batteryLevel, value))
            {
                OnPropertyChanged(nameof(BatteryText));
                OnPropertyChanged(nameof(BatteryFaixa));
                OnPropertyChanged(nameof(BatteryColorHex));
            }
        }
    }

    public DateTime? LastUpdated
    {
        get => _lastUpdated;
        set
        {
            if (SetField(ref _lastUpdated, value))
            {
                OnPropertyChanged(nameof(LastUpdatedText));
            }
        }
    }

    public bool IsTrayVisible
    {
        get => _isTrayVisible;
        set
        {
            if (SetField(ref _isTrayVisible, value))
            {
                OnPropertyChanged(nameof(TrayStatusText));
            }
        }
    }

    public string TrayStatusText => _isTrayVisible ? string.Empty : " · fora da barra";

    public int Priority
    {
        get => _priority;
        set => SetField(ref _priority, value);
    }

    public bool HasBatteryInfo
    {
        get => _hasBatteryInfo;
        set => SetField(ref _hasBatteryInfo, value);
    }

    public string BatterySource
    {
        get => _batterySource;
        set => SetField(ref _batterySource, value);
    }

    public ulong BluetoothAddress
    {
        get => _bluetoothAddress;
        set => SetField(ref _bluetoothAddress, value);
    }

    public BatteryFaixa BatteryFaixa => BatteryRangeHelper.GetFaixa(_batteryLevel);

    public string BatteryColorHex => BatteryRangeHelper.GetHexColor(BatteryFaixa);

    public string BatteryText => _batteryLevel.HasValue ? $"{_batteryLevel.Value}%" : (HasBatteryInfo ? "Lendo..." : "N/D");

    public string StatusDescription => IsConnected ? "Conectado" : "Desconectado";

    public string StatusColorHex => IsConnected ? "#22C55E" : "#9CA3AF";

    public string LastUpdatedText => _lastUpdated.HasValue ? _lastUpdated.Value.ToString("HH:mm:ss") : "--";

    public string TypeGlyph => Type switch
    {
        DeviceType.Headphones => "🎧",
        DeviceType.Headset => "🎧",
        DeviceType.Speaker => "🔊",
        DeviceType.Mouse => "🖱",
        DeviceType.Keyboard => "⌨",
        DeviceType.Gamepad => "🎮",
        DeviceType.Phone => "📱",
        DeviceType.Pen => "🖊",
        _ => "📶"
    };

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
