using Batto.Storage;

namespace Batto.Linux.Storage;

/// <summary>
/// Starts Batto with the session through an XDG autostart entry, ~/.config/autostart/batto.desktop
/// (or $XDG_CONFIG_HOME/autostart). Works on KDE, GNOME, Xfce and the other XDG desktops.
/// </summary>
public sealed class LinuxAutostart : IAutostart
{
    private const string EntryFileName = "batto.desktop";
    private const string AutostartFolder = "autostart";
    private const string ConfigHomeVariable = "XDG_CONFIG_HOME";
    private const string DefaultConfigFolder = ".config";
    private const string MinimizedArgument = "--minimized";

    private readonly string _entryPath;

    /// <param name="configHome">XDG config directory. Defaults to $XDG_CONFIG_HOME, then ~/.config.</param>
    public LinuxAutostart(string? configHome = null)
    {
        _entryPath = Path.Combine(configHome ?? ResolveConfigHome(), AutostartFolder, EntryFileName);
    }

    public string EntryPath => _entryPath;

    public bool IsEnabled() => File.Exists(_entryPath);

    public void Enable(string executablePath)
    {
        string folder = Path.GetDirectoryName(_entryPath)!;
        Directory.CreateDirectory(folder);
        File.WriteAllText(_entryPath, BuildEntry(executablePath));
    }

    public void Disable()
    {
        if (File.Exists(_entryPath)) File.Delete(_entryPath);
    }

    private static string BuildEntry(string executablePath) =>
        $"""
        [Desktop Entry]
        Type=Application
        Name=Batto
        Comment=Bateria de dispositivos Bluetooth na bandeja
        Exec="{executablePath}" {MinimizedArgument}
        Terminal=false
        X-GNOME-Autostart-enabled=true
        """ + Environment.NewLine;

    private static string ResolveConfigHome() =>
        Environment.GetEnvironmentVariable(ConfigHomeVariable) is { Length: > 0 } configured
            ? configured
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), DefaultConfigFolder);
}
