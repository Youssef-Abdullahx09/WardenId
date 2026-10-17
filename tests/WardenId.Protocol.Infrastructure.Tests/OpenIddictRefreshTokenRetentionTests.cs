using Microsoft.Extensions.Options;
using WardenId.Protocol.Infrastructure.OpenIddict;

namespace WardenId.Protocol.Infrastructure.Tests;

public class OpenIddictRefreshTokenRetentionTests
{
    [Fact]
    public void MinimumRetention_IsThePruneAge_WhenPruning()
    {
        var retention = new OpenIddictRefreshTokenRetention(Options.Create(
            new ProtocolOpenIddictPruningOptions { PruneMinimumAge = TimeSpan.FromDays(120) }));

        Assert.Equal(TimeSpan.FromDays(120), retention.MinimumRetention);
    }

    [Fact]
    public void MinimumRetention_IsNull_WhenPruningIsOff()
    {
        var retention = new OpenIddictRefreshTokenRetention(Options.Create(
            new ProtocolOpenIddictPruningOptions { PruningEnabled = false }));

        Assert.Null(retention.MinimumRetention);
    }
}
