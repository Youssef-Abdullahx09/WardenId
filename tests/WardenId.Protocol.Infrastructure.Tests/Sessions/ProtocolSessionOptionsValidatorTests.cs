using Microsoft.Extensions.Options;
using WardenId.Protocol.Infrastructure.Sessions;

namespace WardenId.Protocol.Infrastructure.Tests.Sessions;

public class ProtocolSessionOptionsValidatorTests
{
    private static ValidateOptionsResult Validate(ProtocolSessionOptions options) =>
        new ProtocolSessionOptionsValidator().Validate(null, options);

    [Fact]
    public void Defaults_AreValid() =>
        Assert.True(Validate(new ProtocolSessionOptions()).Succeeded);

    [Fact]
    public void ZeroAbsoluteLifetime_Fails()
    {
        var result = Validate(new ProtocolSessionOptions { AbsoluteLifetime = TimeSpan.Zero });

        Assert.True(result.Failed);
        Assert.Contains(nameof(ProtocolSessionOptions.AbsoluteLifetime), result.FailureMessage);
    }

    [Fact]
    public void MinimumAgeBelowAbsoluteLifetime_Fails()
    {
        var result = Validate(new ProtocolSessionOptions
        {
            AbsoluteLifetime = TimeSpan.FromDays(2),
            PruneMinimumAge = TimeSpan.FromDays(2) - TimeSpan.FromSeconds(1)
        });

        Assert.True(result.Failed);
        Assert.Contains(nameof(ProtocolSessionOptions.PruneMinimumAge), result.FailureMessage);
    }

    [Fact]
    public void MinimumAgeEqualToAbsoluteLifetime_Passes() =>
        Assert.True(Validate(new ProtocolSessionOptions
        {
            AbsoluteLifetime = TimeSpan.FromDays(2),
            PruneMinimumAge = TimeSpan.FromDays(2)
        }).Succeeded);
}
