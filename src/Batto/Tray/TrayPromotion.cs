using Microsoft.Win32;

namespace Batto.Tray;

/// <summary>
/// Windows 11 places new notification-area icons in the overflow chevron (^) by default.
/// Explorer records each icon under HKCU\Control Panel\NotifyIconSettings\{id} with its
/// ExecutablePath; setting IsPromoted = 1 on those entries is what the
/// "Show icon and notifications" toggle in Settings writes, so the icon stays on the taskbar.
/// Only entries whose ExecutablePath is this process are touched.
/// </summary>
public static class TrayPromotion
{
    private const string NotifyIconSettingsKey = @"Control Panel\NotifyIconSettings";

    public static int PromoteOwnIcons()
    {
        string? exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return 0;

        int promoted = 0;
        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(NotifyIconSettingsKey, writable: true);
            if (root == null) return 0;

            foreach (string name in root.GetSubKeyNames())
            {
                using var entry = root.OpenSubKey(name, writable: true);
                if (entry == null) continue;

                if (!string.Equals(entry.GetValue("ExecutablePath") as string, exePath, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (entry.GetValue("IsPromoted") is not int current || current != 1)
                {
                    entry.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                }
                promoted++;
            }
        }
        catch (Exception ex)
        {
            CrashLogger.Log("TrayPromotion.PromoteOwnIcons", ex);
        }

        return promoted;
    }

    /// <summary>
    /// Explorer creates the NotifyIconSettings entry asynchronously after NIM_ADD,
    /// so retry a few times over the first seconds.
    /// </summary>
    public static void SchedulePromotion()
    {
        _ = Task.Run(async () =>
        {
            foreach (int delayMs in new[] { 500, 2000, 5000 })
            {
                await Task.Delay(delayMs);
                PromoteOwnIcons();
            }
        });
    }
}
