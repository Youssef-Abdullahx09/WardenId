using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Features.Policies.GetIdentity;

namespace WardenId.Identity.Application.Tests.Features.Policies.GetIdentity;

public class HandlerTests
{
    [Fact]
    public async Task Handle_ReturnsConfiguredPolicy()
    {
        var options = new IdentityPolicyOptions
        {
            PasswordMinLength = 12,
            PasswordRequireUppercase = false,
            PasswordRequireDigit = true,
            PasswordRequireSymbol = true,
            PasswordBreachCheckEnabled = false,
            PasswordHistoryCount = 3,
            MaxFailedLoginAttempts = 7,
            LockoutDuration = TimeSpan.FromMinutes(30),
            MfaPolicyMode = MfaPolicyMode.Required
        };
        var handler = new Handler(Options.Create(options));

        var result = await handler.Handle(new Query(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var policy = result.Value;
        Assert.Equal(new PasswordSettings(12, false, true, true, false, 3), policy.Password);
        Assert.Equal(new LockoutSettings(7, "PT30M"), policy.Lockout);
        Assert.Equal(MfaPolicyMode.Required, policy.Mfa.Mode);
        Assert.Equal("global", policy.Scope);
    }

    [Fact]
    public async Task Handle_ReturnsCodeDefaults_WhenNothingIsConfigured()
    {
        var handler = new Handler(Options.Create(new IdentityPolicyOptions()));

        var result = await handler.Handle(new Query(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var policy = result.Value;
        Assert.Equal(new PasswordSettings(8, true, true, false, true, 5), policy.Password);
        Assert.Equal(new LockoutSettings(5, "PT15M"), policy.Lockout);
        Assert.Equal(MfaPolicyMode.Optional, policy.Mfa.Mode);
    }
}
