using System.IO;
using Batto.Storage;
using Xunit;

namespace Batto.Tests;

public class SettingsManagerTests
{
    [Fact]
    public void LoadAndSave_PersistsConfiguration()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "BattoTests_" + Guid.NewGuid().ToString("N"));
        try
        {
            var manager = new SettingsManager(tempDir);
            Assert.Equal(60, manager.Settings.RefreshIntervalSeconds);
            Assert.True(manager.Settings.NotifyLowBattery);

            // Change values
            manager.Settings.RefreshIntervalSeconds = 30;
            manager.Settings.StartMinimized = false;
            var pref = manager.Settings.GetPreference("dev_123");
            pref.IsVisibleInTray = false;
            pref.Priority = 5;

            manager.Save();

            // Re-load
            var manager2 = new SettingsManager(tempDir);
            Assert.Equal(30, manager2.Settings.RefreshIntervalSeconds);
            Assert.False(manager2.Settings.StartMinimized);
            Assert.True(manager2.Settings.DevicePreferences.ContainsKey("dev_123"));
            Assert.False(manager2.Settings.DevicePreferences["dev_123"].IsVisibleInTray);
            Assert.Equal(5, manager2.Settings.DevicePreferences["dev_123"].Priority);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
