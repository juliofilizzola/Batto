using Batto.Logitech;

namespace Batto.Tests;

public class LogitechPresenceTests
{
    private static readonly DateTime Now = new(2026, 11, 19, 12, 0, 0);

    [Fact]
    public void Answering_now_is_connected_even_without_history()
    {
        Assert.True(LogitechPresence.IsConsideredConnected(true, null, Now));
    }

    [Fact]
    public void Silent_poll_within_grace_period_stays_connected()
    {
        DateTime lastReply = Now - TimeSpan.FromMinutes(2);

        Assert.True(LogitechPresence.IsConsideredConnected(false, lastReply, Now));
    }

    [Fact]
    public void Silent_poll_after_grace_period_is_disconnected()
    {
        DateTime lastReply = Now - LogitechPresence.GracePeriod - TimeSpan.FromSeconds(1);

        Assert.False(LogitechPresence.IsConsideredConnected(false, lastReply, Now));
    }

    [Fact]
    public void Silent_poll_that_never_replied_is_disconnected()
    {
        Assert.False(LogitechPresence.IsConsideredConnected(false, null, Now));
    }
}
