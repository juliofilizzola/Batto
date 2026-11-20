using System.Windows.Input;
using Avalonia.Media;
using Batto.Models;

namespace Batto.Linux.UI;

/// <summary>One device card. Holds a copy of the device state and sends user changes back through <see cref="IDeviceActions"/>.</summary>
public sealed class DeviceCardViewModel : ViewModelBase
{
    private const string UnknownSource = "Verificando...";

    private readonly BluetoothDeviceModel _model;
    private readonly IDeviceActions _actions;
    private bool _isTrayVisible;
    private DeviceTypeOption _selectedType;

    public DeviceCardViewModel(BluetoothDeviceModel model, IDeviceActions actions)
    {
        _model = model;
        _actions = actions;
        _isTrayVisible = model.IsTrayVisible;
        _selectedType = DeviceTypeOption.All.FirstOrDefault(o => o.Type == model.Type) ?? DeviceTypeOption.All[^1];

        PriorityUpCommand = new RelayCommand(() => _actions.SetPriority(_model, _model.Priority + 1));
        PriorityDownCommand = new RelayCommand(() => _actions.SetPriority(_model, _model.Priority - 1));
    }

    public BluetoothDeviceModel Model => _model;
    public string Name => _model.Name;
    public string TypeGlyph => _model.TypeGlyph;
    public bool IsConnected => _model.IsConnected;
    public string StatusText => _model.StatusDescription;
    public IBrush StatusBrush => Brush(_model.StatusColorHex);
    public string SourceText => string.IsNullOrEmpty(_model.BatterySource) ? UnknownSource : _model.BatterySource;
    public string LastUpdatedText => _model.LastUpdatedText;
    public string BatteryText => _model.BatteryText;
    public IBrush BatteryBrush => Brush(_model.BatteryColorHex);
    public int Priority => _model.Priority;
    public IReadOnlyList<DeviceTypeOption> TypeOptions => DeviceTypeOption.All;

    public ICommand PriorityUpCommand { get; }
    public ICommand PriorityDownCommand { get; }

    /// <summary>Tray switch. Only usable for connected devices, like the Windows app.</summary>
    public bool IsTrayVisible
    {
        get => _isTrayVisible;
        set
        {
            if (!SetField(ref _isTrayVisible, value)) return;
            _actions.SetTrayVisibility(_model, value);
        }
    }

    public DeviceTypeOption SelectedType
    {
        get => _selectedType;
        set
        {
            if (value is null || !SetField(ref _selectedType, value)) return;
            _actions.SetDeviceType(_model, value.Type);
        }
    }

    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
}
