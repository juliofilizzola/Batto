using Tmds.DBus.Protocol;

namespace Batto.Linux.Bluez;

/// <summary>
/// Keeps an in-memory copy of the BlueZ devices, updated from the ObjectManager and PropertiesChanged signals.
/// All state is guarded by a lock. Change notifications are sent through <c>post</c>, so the caller
/// decides the thread (the UI thread in the app), and the core never sees a cross-thread model update.
/// </summary>
public sealed class BlueZRegistry : IDisposable
{
    private readonly DBusConnection _connection;
    private readonly Action<Action> _post;
    private readonly object _gate = new();
    private readonly Dictionary<string, DeviceState> _devices = new();
    private readonly List<IDisposable> _objectManagerSubscriptions = new();

    public event Action<BlueZDeviceSnapshot>? DeviceChanged;

    /// <summary>Raised with the object path of a device that BlueZ removed.</summary>
    public event Action<string>? DeviceRemoved;

    public BlueZRegistry(DBusConnection connection, Action<Action> post)
    {
        _connection = connection;
        _post = post;
    }

    /// <summary>Subscribes to BlueZ changes first, then loads the current devices, so no change is lost in between.</summary>
    public async Task StartAsync()
    {
        _objectManagerSubscriptions.Add(await WatchInterfacesAddedAsync());
        _objectManagerSubscriptions.Add(await WatchInterfacesRemovedAsync());

        var objects = await CallObjectManagerAsync();

        List<DeviceState> touched = new();
        List<DeviceState> added = new();
        lock (_gate)
        {
            foreach (var (path, interfaces) in objects)
            {
                ApplyInterfaces(path, interfaces, touched, added);
            }
        }

        foreach (var state in added)
        {
            _ = WatchDevicePropertiesAsync(state);
        }
        Publish(touched);
    }

    /// <summary>Battery level of the device with this address, or null when BlueZ does not expose one.</summary>
    public bool TryGetBatteryLevel(string address, out int? level)
    {
        lock (_gate)
        {
            var device = _devices.Values.FirstOrDefault(d => string.Equals(d.Address, address, StringComparison.OrdinalIgnoreCase));
            level = device?.BatteryLevel;
            return device != null;
        }
    }

    private void ApplyInterfaces(
        string path,
        IReadOnlyDictionary<string, Dictionary<string, VariantValue>> interfaces,
        List<DeviceState> touched,
        List<DeviceState> added)
    {
        if (interfaces.TryGetValue(BlueZNames.DeviceInterface, out var deviceProperties))
        {
            if (!_devices.TryGetValue(path, out var state))
            {
                state = new DeviceState(path);
                _devices[path] = state;
                added.Add(state);
            }

            ApplyDeviceProperties(state, deviceProperties);
            touched.Add(state);
        }

        if (interfaces.TryGetValue(BlueZNames.BatteryInterface, out var batteryProperties)
            && _devices.TryGetValue(path, out var owner))
        {
            ApplyBatteryProperties(owner, batteryProperties);
            if (!touched.Contains(owner)) touched.Add(owner);
        }
    }

    private static void ApplyDeviceProperties(DeviceState state, IReadOnlyDictionary<string, VariantValue> properties)
    {
        state.Address = BlueZValues.String(properties, BlueZNames.DevicePropertyAddress) ?? state.Address;
        state.Alias = BlueZValues.String(properties, BlueZNames.DevicePropertyAlias) ?? state.Alias;
        state.Name = BlueZValues.String(properties, BlueZNames.DevicePropertyName) ?? state.Name;
        state.Icon = BlueZValues.String(properties, BlueZNames.DevicePropertyIcon) ?? state.Icon;
        state.Paired = BlueZValues.Bool(properties, BlueZNames.DevicePropertyPaired) ?? state.Paired;
        state.Connected = BlueZValues.Bool(properties, BlueZNames.DevicePropertyConnected) ?? state.Connected;
    }

    private static void ApplyBatteryProperties(DeviceState state, IReadOnlyDictionary<string, VariantValue> properties)
    {
        if (properties.ContainsKey(BlueZNames.BatteryPropertyPercentage))
        {
            state.BatteryLevel = BlueZValues.Byte(properties, BlueZNames.BatteryPropertyPercentage);
        }
    }

    private void Publish(IEnumerable<DeviceState> touched)
    {
        List<BlueZDeviceSnapshot> snapshots;
        lock (_gate)
        {
            snapshots = touched.Select(state => state.ToSnapshot()).ToList();
        }

        _post(() =>
        {
            foreach (var snapshot in snapshots)
            {
                DeviceChanged?.Invoke(snapshot);
            }
        });
    }

    private void HandleInterfacesAdded(Notification<(string Path, Dictionary<string, Dictionary<string, VariantValue>> Interfaces)> notification)
    {
        if (!notification.HasValue)
        {
            LogNotificationError("BlueZ InterfacesAdded", notification.CompletionReason);
            return;
        }

        var (path, interfaces) = notification.Value;
        List<DeviceState> touched = new();
        List<DeviceState> added = new();
        lock (_gate)
        {
            ApplyInterfaces(path, interfaces, touched, added);
        }

        foreach (var state in added)
        {
            _ = WatchDevicePropertiesAsync(state);
        }
        Publish(touched);
    }

    private void HandleInterfacesRemoved(Notification<(string Path, string[] Interfaces)> notification)
    {
        if (!notification.HasValue)
        {
            LogNotificationError("BlueZ InterfacesRemoved", notification.CompletionReason);
            return;
        }

        var (path, removedInterfaces) = notification.Value;
        if (removedInterfaces.Contains(BlueZNames.DeviceInterface))
        {
            RemoveDevice(path);
            return;
        }

        if (!removedInterfaces.Contains(BlueZNames.BatteryInterface)) return;

        DeviceState? state;
        lock (_gate)
        {
            _devices.TryGetValue(path, out state);
            if (state != null) state.BatteryLevel = null;
        }

        if (state != null) Publish([state]);
    }

    private void RemoveDevice(string path)
    {
        IDisposable? watch;
        lock (_gate)
        {
            if (!_devices.Remove(path, out var state)) return;
            watch = state.PropertiesWatch;
        }

        watch?.Dispose();
        _post(() => DeviceRemoved?.Invoke(path));
    }

    private void HandleDeviceProperties(DeviceState state, Notification<(string Interface, Dictionary<string, VariantValue> Changed)> notification)
    {
        if (!notification.HasValue)
        {
            LogNotificationError($"BlueZ PropertiesChanged {state.Path}", notification.CompletionReason);
            return;
        }

        var (@interface, changed) = notification.Value;
        lock (_gate)
        {
            if (!_devices.TryGetValue(state.Path, out var current) || !ReferenceEquals(current, state)) return;

            if (@interface == BlueZNames.DeviceInterface) ApplyDeviceProperties(state, changed);
            else if (@interface == BlueZNames.BatteryInterface) ApplyBatteryProperties(state, changed);
            else return;
        }

        Publish([state]);
    }

    private async Task WatchDevicePropertiesAsync(DeviceState state)
    {
        try
        {
            var watch = await _connection.WatchSignalAsync(
                BlueZNames.Service,
                state.Path,
                BlueZNames.PropertiesInterface,
                BlueZNames.PropertiesChangedMember,
                ReadPropertiesChanged,
                (Notification<(string Interface, Dictionary<string, VariantValue> Changed)> n) => HandleDeviceProperties(state, n),
                null,
                ObserverFlags.None,
                null);

            lock (_gate)
            {
                if (_devices.TryGetValue(state.Path, out var current) && ReferenceEquals(current, state))
                {
                    state.PropertiesWatch = watch;
                    return;
                }
            }

            watch.Dispose();
        }
        catch (Exception ex)
        {
            CrashLogger.Log($"BlueZ watch PropertiesChanged {state.Path}", ex);
        }
    }

    private ValueTask<IDisposable> WatchInterfacesAddedAsync() =>
        _connection.WatchSignalAsync(
            BlueZNames.Service,
            BlueZNames.RootPath,
            BlueZNames.ObjectManagerInterface,
            BlueZNames.InterfacesAddedMember,
            ReadInterfacesAdded,
            (Notification<(string Path, Dictionary<string, Dictionary<string, VariantValue>> Interfaces)> n) => HandleInterfacesAdded(n),
            null,
            ObserverFlags.None,
            null);

    private ValueTask<IDisposable> WatchInterfacesRemovedAsync() =>
        _connection.WatchSignalAsync(
            BlueZNames.Service,
            BlueZNames.RootPath,
            BlueZNames.ObjectManagerInterface,
            BlueZNames.InterfacesRemovedMember,
            ReadInterfacesRemoved,
            (Notification<(string Path, string[] Interfaces)> n) => HandleInterfacesRemoved(n),
            null,
            ObserverFlags.None,
            null);

    private Task<Dictionary<string, Dictionary<string, Dictionary<string, VariantValue>>>> CallObjectManagerAsync()
    {
        MessageBuffer request;
        using (var writer = _connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(
                destination: BlueZNames.Service,
                path: BlueZNames.RootPath,
                @interface: BlueZNames.ObjectManagerInterface,
                member: BlueZNames.GetManagedObjectsMember,
                signature: null);
            request = writer.CreateMessage();
        }

        return _connection.CallMethodAsync(request, ReadManagedObjectsReply, null);
    }

    /// <summary>Reads the a{oa{sa{sv}}} reply of GetManagedObjects.</summary>
    private static Dictionary<string, Dictionary<string, Dictionary<string, VariantValue>>> ReadManagedObjectsReply(Message message, object? state)
    {
        var reader = message.GetBodyReader();
        var objects = new Dictionary<string, Dictionary<string, Dictionary<string, VariantValue>>>();
        ArrayEnd end = reader.ReadDictionaryStart();
        while (reader.HasNext(end))
        {
            string path = reader.ReadObjectPathAsString();
            objects[path] = ReadInterfaces(ref reader);
        }
        return objects;
    }

    private static (string Path, Dictionary<string, Dictionary<string, VariantValue>> Interfaces) ReadInterfacesAdded(Message message, object? state)
    {
        var reader = message.GetBodyReader();
        string path = reader.ReadObjectPathAsString();
        return (path, ReadInterfaces(ref reader));
    }

    private static (string Path, string[] Interfaces) ReadInterfacesRemoved(Message message, object? state)
    {
        var reader = message.GetBodyReader();
        string path = reader.ReadObjectPathAsString();
        return (path, reader.ReadArrayOfString());
    }

    private static (string Interface, Dictionary<string, VariantValue> Changed) ReadPropertiesChanged(Message message, object? state)
    {
        var reader = message.GetBodyReader();
        string @interface = reader.ReadString();
        var changed = reader.ReadDictionaryOfStringToVariantValue();
        return (@interface, changed);
    }

    /// <summary>Reads the a{sa{sv}} of interface name to its properties.</summary>
    private static Dictionary<string, Dictionary<string, VariantValue>> ReadInterfaces(ref Reader reader)
    {
        var interfaces = new Dictionary<string, Dictionary<string, VariantValue>>();
        ArrayEnd end = reader.ReadDictionaryStart();
        while (reader.HasNext(end))
        {
            string @interface = reader.ReadString();
            interfaces[@interface] = reader.ReadDictionaryOfStringToVariantValue();
        }
        return interfaces;
    }

    private static void LogNotificationError(string context, Exception? exception)
    {
        if (exception != null) CrashLogger.Log(context, exception);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var state in _devices.Values)
            {
                state.PropertiesWatch?.Dispose();
            }
            _devices.Clear();
        }

        foreach (var subscription in _objectManagerSubscriptions)
        {
            subscription.Dispose();
        }
        _objectManagerSubscriptions.Clear();
    }

    private sealed class DeviceState(string path)
    {
        public string Path { get; } = path;
        public string Address { get; set; } = string.Empty;
        public string? Alias { get; set; }
        public string? Name { get; set; }
        public string? Icon { get; set; }
        public bool Paired { get; set; }
        public bool Connected { get; set; }
        public int? BatteryLevel { get; set; }
        public IDisposable? PropertiesWatch { get; set; }

        public BlueZDeviceSnapshot ToSnapshot() =>
            new(Path, Address, Alias ?? Name ?? Address, Icon, Paired, Connected, BatteryLevel);
    }
}
