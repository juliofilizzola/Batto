namespace Batto.Logitech;

/// <summary>
/// Decides whether a receiver slot still counts as connected. A mouse that sleeps or misses one
/// HID++ reply stops answering for a moment, so a single silent poll must not mark it disconnected.
/// </summary>
public static class LogitechPresence
{
    /// <summary>How long after the last successful reply a silent slot still counts as connected.</summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(5);

    /// <param name="answeredNow">True when the slot replied to the current poll.</param>
    /// <param name="lastReply">Time of the last successful reply, or null if it never replied.</param>
    /// <param name="now">Current time.</param>
    public static bool IsConsideredConnected(bool answeredNow, DateTime? lastReply, DateTime now)
    {
        if (answeredNow) return true;
        return lastReply is { } last && now - last < GracePeriod;
    }
}
