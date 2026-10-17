using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WardenId.Identity.Application;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Application.Features.Policies.UpdateIdentity;
using GetQuery = WardenId.Identity.Application.Features.Policies.GetIdentity.Query;
using GetHandler = WardenId.Identity.Application.Features.Policies.GetIdentity.Handler;
using RegisterCommand = WardenId.Identity.Application.Features.Users.Register.Command;
using RegisterHandler = WardenId.Identity.Application.Features.Users.Register.Handler;
using UpdateHandler = WardenId.Identity.Application.Features.Policies.UpdateIdentity.Handler;

namespace WardenId.Identity.Infrastructure.Tests.Features.Policies;

/// <summary>
/// Per-tenant identity policy on Postgres: the overrides round-trip (nullable columns, the interval
/// and the enum as text), one row per tenant, and sign-up and sign-in read them.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class TenantPolicyTests(PostgresFixture database)
{
    private static readonly IOptions<IdentityPolicyOptions> Global =
        Options.Create(new IdentityPolicyOptions { PasswordBreachCheckEnabled = false });

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly IdentityTenantPolicyCache _cache = new(TimeProvider.System);

    [SkippableFact]
    public async Task Put_StoresEveryOverride_AndGetReturnsThemAsTheTenantScope()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);

        var put = await UpdateAsync(new Command(
            _tenantId,
            new PasswordOverrides(12, false, true, true, false, 10),
            new LockoutOverrides(3, "PT45M"),
            new MfaOverrides(MfaPolicyMode.Required),
            new SelfRegistrationOverrides(false),
            _adminId));
        Assert.True(put.IsSuccess);

        await using var context = database.CreateContext();
        var policy = (await new GetHandler(new IdentityTenantPolicyRepository(context), Global)
            .Handle(new GetQuery(_tenantId), CancellationToken.None)).Value;

        Assert.Equal("tenant", policy.Scope);
        Assert.Equal(12, policy.Password.MinLength);
        Assert.False(policy.Password.RequireUppercase);
        Assert.Equal(10, policy.Password.HistoryCount);
        Assert.Equal(3, policy.Lockout.MaxFailedAttempts);
        Assert.Equal("PT45M", policy.Lockout.LockoutDuration);
        Assert.Equal(MfaPolicyMode.Required, policy.Mfa.Mode);
        Assert.False(policy.SelfRegistration.Enabled);
        Assert.Equal(10, policy.Overrides.Count);
        Assert.Equal(1, await database.CountAsync(
            "SELECT count(*) FROM \"Identity\".\"TenantPolicies\" WHERE \"TenantId\" = $1 AND \"MfaMode\" = 'Required'", _tenantId));
    }

    [SkippableFact]
    public async Task PutAgain_ReplacesTheSameRow_AndNullsClearOverrides()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        await UpdateAsync(new Command(_tenantId, new PasswordOverrides(12, null, null, null, null, null), null, null, null, _adminId));

        var second = await UpdateAsync(new Command(_tenantId, null, new LockoutOverrides(7, null), null, null, _adminId));

        Assert.True(second.IsSuccess);
        Assert.Equal(1, await database.CountAsync("SELECT count(*) FROM \"Identity\".\"TenantPolicies\" WHERE \"TenantId\" = $1", _tenantId));
        await using var context = database.CreateContext();
        var stored = await new IdentityTenantPolicyRepository(context).GetOverridesAsync(_tenantId);
        Assert.Equal(IdentityPolicyOverrides.None with { MaxFailedLoginAttempts = 7 }, stored);
    }

    [SkippableFact]
    public async Task TheUniqueIndex_AllowsOnePolicyPerTenant()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        await using (var first = database.CreateContext())
            await new IdentityTenantPolicyRepository(first).CreateAsync(IdentityTenantPolicy.Create(_tenantId, IdentityPolicyOverrides.None).Value, _adminId);

        await using var second = database.CreateContext();
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            new IdentityTenantPolicyRepository(second).CreateAsync(IdentityTenantPolicy.Create(_tenantId, IdentityPolicyOverrides.None).Value, _adminId));
    }

    [SkippableFact]
    public async Task Register_IsForbidden_OnceTheTenantDisablesSelfRegistration_ThroughTheCache()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        Assert.True((await RegisterAsync("first")).IsSuccess);

        await UpdateAsync(new Command(_tenantId, null, null, null, new SelfRegistrationOverrides(false), _adminId));
        var result = await RegisterAsync("second");

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.Forbidden, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
    }

    [SkippableFact]
    public async Task Resolver_ReturnsGlobalValues_ForATenantWithoutARow()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        await using var context = database.CreateContext();

        var policy = (await new IdentityPolicyResolver(new IdentityTenantPolicyRepository(context), _cache, Global)
            .ResolveAsync(Guid.NewGuid(), CancellationToken.None)).Value;

        Assert.Equal(5, policy.MaxFailedLoginAttempts);
        Assert.False(policy.Overrides.HasAny);
    }

    private async Task<FluentResults.Result> UpdateAsync(Command command)
    {
        await using var context = database.CreateContext();
        return await new UpdateHandler(new IdentityTenantPolicyRepository(context), _cache).Handle(command, CancellationToken.None);
    }

    private async Task<FluentResults.Result<WardenId.Identity.Application.Features.Users.Register.UserRegistered>> RegisterAsync(string name)
    {
        await using var context = database.CreateContext();
        var resolver = new IdentityPolicyResolver(new IdentityTenantPolicyRepository(context), _cache, Global);
        return await new RegisterHandler(new UserRepository(context), new PlainPasswordHasher(), new NeverBreached(), new NoEmail(), resolver)
            .Handle(new RegisterCommand(_tenantId, $"{name}{Guid.NewGuid():N}@example.test", $"{name}{Guid.NewGuid().ToString("N")[..8]}", "Correct-Horse-1"), CancellationToken.None);
    }

    private sealed class PlainPasswordHasher : IPasswordHasher
    {
        public string Algorithm => "plain";
        public string Hash(string plaintext) => $"plain:{plaintext}";
        public bool Verify(string plaintext, string hash) => hash == Hash(plaintext);
        public bool NeedsRehash(string hash) => false;
    }

    private sealed class NeverBreached : IBreachedPasswordChecker
    {
        public Task<bool> IsBreachedAsync(string plaintext) => Task.FromResult(false);
    }

    private sealed class NoEmail : IIdentityEmailSender
    {
        public Task SendEmailVerificationAsync(string toEmail, string username, string rawToken, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SendPasswordResetAsync(string toEmail, string username, string rawToken, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
