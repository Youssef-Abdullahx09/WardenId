using Microsoft.Extensions.Options;
using WardenId.Protocol.Application;
using WardenId.Protocol.Infrastructure.OpenIddict;

namespace WardenId.Protocol.Infrastructure.Tests;

public class ProtocolOpenIddictPruningOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(ProtocolOpenIddictPruningOptions options) =>
        new ProtocolOpenIddictPruningOptionsValidator(Options.Create(new ProtocolTokenOptions()))
            .Validate(null, options);

    [Fact]
    public void Defaults_AreValid() =>
        Assert.True(Validate(new ProtocolOpenIddictPruningOptions()).Succeeded);

    [Fact]
    public void ZeroInterval_Fails()
    {
        var result = Validate(new ProtocolOpenIddictPruningOptions { PruneInterval = TimeSpan.Zero });

        Assert.True(result.Failed);
        Assert.Contains(nameof(ProtocolOpenIddictPruningOptions.PruneInterval), result.FailureMessage);
    }

    [Fact]
    public void MinimumAgeBelowRefreshTokenLifetime_Fails()
    {
        var lifetime = new ProtocolTokenOptions().RefreshTokenAbsoluteLifetime;
        var result = Validate(new ProtocolOpenIddictPruningOptions { PruneMinimumAge = lifetime - TimeSpan.FromSeconds(1) });

        Assert.True(result.Failed);
        Assert.Contains(nameof(ProtocolOpenIddictPruningOptions.PruneMinimumAge), result.FailureMessage);
    }

    [Fact]
    public void MinimumAgeEqualToRefreshTokenLifetime_Passes()
    {
        var lifetime = new ProtocolTokenOptions().RefreshTokenAbsoluteLifetime;
        Assert.True(Validate(new ProtocolOpenIddictPruningOptions { PruneMinimumAge = lifetime }).Succeeded);
    }
}
