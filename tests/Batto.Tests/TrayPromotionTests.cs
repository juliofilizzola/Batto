using Batto.Models;
using Batto.Tray;
using Xunit;

namespace Batto.Tests;

public class TrayPromotionTests
{
    [Fact]
    public void PromoteOwnIcons_DoesNotThrow_AndReturnsNonNegativeCount()
    {
        int promoted = TrayPromotion.PromoteOwnIcons();

        Assert.True(promoted >= 0);
    }

    [Fact]
    public void PromoteTrayIcons_IsEnabledByDefault()
    {
        Assert.True(new AppSettings().PromoteTrayIcons);
    }
}
