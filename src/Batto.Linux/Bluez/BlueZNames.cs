namespace Batto.Linux.Bluez;

/// <summary>D-Bus names used to talk to BlueZ (org.bluez on the system bus).</summary>
public static class BlueZNames
{
    public const string Service = "org.bluez";
    public const string RootPath = "/";

    public const string ObjectManagerInterface = "org.freedesktop.DBus.ObjectManager";
    public const string PropertiesInterface = "org.freedesktop.DBus.Properties";
    public const string DeviceInterface = "org.bluez.Device1";
    public const string BatteryInterface = "org.bluez.Battery1";

    public const string GetManagedObjectsMember = "GetManagedObjects";
    public const string InterfacesAddedMember = "InterfacesAdded";
    public const string InterfacesRemovedMember = "InterfacesRemoved";
    public const string PropertiesChangedMember = "PropertiesChanged";

    public const string DevicePropertyConnected = "Connected";
    public const string DevicePropertyPaired = "Paired";
    public const string DevicePropertyAlias = "Alias";
    public const string DevicePropertyName = "Name";
    public const string DevicePropertyIcon = "Icon";
    public const string DevicePropertyAddress = "Address";
    public const string BatteryPropertyPercentage = "Percentage";

    /// <summary>Prefix of the device ids used by the Batto core, so they never collide with Logitech ids.</summary>
    public const string DeviceIdPrefix = "bluez:";

    /// <summary>Label shown in the UI for the source of a battery value.</summary>
    public const string BatterySourceLabel = "BlueZ (Battery1)";
}
