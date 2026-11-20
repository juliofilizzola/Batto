using Batto.Linux.Storage;
using Batto.Storage;
using Xunit;

namespace Batto.Linux.Tests;

public class LinuxAutostartTests : IDisposable
{
    private const string FakeExecutable = "/opt/batto/batto";

    private readonly string _configHome = Path.Combine(Path.GetTempPath(), "BattoAutostart_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_configHome)) Directory.Delete(_configHome, true);
    }

    [Fact]
    public void Enable_WritesDesktopEntryWithMinimizedArgument()
    {
        var autostart = new LinuxAutostart(_configHome);

        autostart.Enable(FakeExecutable);

        Assert.True(autostart.IsEnabled());
        string content = File.ReadAllText(autostart.EntryPath);
        Assert.Contains("[Desktop Entry]", content);
        Assert.Contains("Type=Application", content);
        Assert.Contains($"Exec=\"{FakeExecutable}\" --minimized", content);
    }

    [Fact]
    public void Entry_IsInXdgAutostartFolderUnderConfigHome()
    {
        var autostart = new LinuxAutostart(_configHome);

        Assert.Equal(Path.Combine(_configHome, "autostart", "batto.desktop"), autostart.EntryPath);
    }

    [Fact]
    public void Disable_RemovesEntry()
    {
        var autostart = new LinuxAutostart(_configHome);
        autostart.Enable(FakeExecutable);

        autostart.Disable();

        Assert.False(autostart.IsEnabled());
    }

    [Fact]
    public void SettingsSave_WithStartWithWindows_EnablesAutostartThroughSettingsManager()
    {
        var autostart = new LinuxAutostart(_configHome);
        var settings = new SettingsManager(Path.Combine(_configHome, "app"), autostart);

        settings.Settings.StartWithWindows = true;
        settings.Save();
        Assert.True(autostart.IsEnabled());

        settings.Settings.StartWithWindows = false;
        settings.Save();
        Assert.False(autostart.IsEnabled());
    }

    [Fact]
    public void Disable_WhenNothingWasEnabled_DoesNotThrow()
    {
        var autostart = new LinuxAutostart(_configHome);

        autostart.Disable();

        Assert.False(autostart.IsEnabled());
    }
}
