using WardenId.Identity.Application.Features.Policies.UpdateIdentity;

namespace WardenId.Identity.Application.Tests.Features.Policies.UpdateIdentity;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid AdminId = Guid.NewGuid();

    private readonly FakeIdentityTenantPolicyRepository _repository = new();
    private readonly IdentityTenantPolicyCache _cache = new(TimeProvider.System);

    private Handler NewHandler() => new(_repository, _cache);

    private static Command NewCommand(
        PasswordOverrides? password = null,
        LockoutOverrides? lockout = null,
        MfaOverrides? mfa = null,
        SelfRegistrationOverrides? selfRegistration = null,
        TermsOverrides? terms = null) =>
        new(TenantId, password, lockout, mfa, selfRegistration, terms, AdminId);

    [Fact]
    public async Task Handle_CreatesTheTenantsPolicy_AsTheAdmin()
    {
        var result = await NewHandler().Handle(
            NewCommand(
                new PasswordOverrides(12, null, true, null, null, 10),
                new LockoutOverrides(3, "PT30M"),
                new MfaOverrides(MfaPolicyMode.Required),
                new SelfRegistrationOverrides(false),
                new TermsOverrides(" https://example.test/terms ")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, _repository.CreateCallCount);
        Assert.Equal(AdminId, _repository.LastActorId);
        var stored = (await _repository.GetAsync(TenantId))!.Overrides;
        Assert.Equal(
            new IdentityPolicyOverrides(12, null, true, null, null, 10, 3, TimeSpan.FromMinutes(30), MfaPolicyMode.Required, false, "https://example.test/terms"),
            stored);
    }

    [Fact]
    public async Task Handle_ReplacesTheExistingPolicy_NullsClearingOverrides()
    {
        _repository.With(TenantId, IdentityPolicyOverrides.None with { PasswordMinLength = 12, MaxFailedLoginAttempts = 3 });

        var result = await NewHandler().Handle(NewCommand(lockout: new LockoutOverrides(10, null)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, _repository.CreateCallCount);
        Assert.Equal(1, _repository.UpdateCallCount);
        Assert.Equal(IdentityPolicyOverrides.None with { MaxFailedLoginAttempts = 10 }, (await _repository.GetAsync(TenantId))!.Overrides);
    }

    [Fact]
    public async Task Handle_InvalidatesTheTenantsCachedPolicy()
    {
        _cache.Set(TenantId, IdentityPolicyOverrides.None, _cache.Generation(TenantId));

        await NewHandler().Handle(NewCommand(mfa: new MfaOverrides(MfaPolicyMode.Required)), CancellationToken.None);

        Assert.False(_cache.TryGet(TenantId, out _));
    }

    [Fact]
    public async Task Handle_FailsWithValidation_AndStoresNothing_WhenAValueIsOutOfBounds()
    {
        var result = await NewHandler().Handle(
            NewCommand(new PasswordOverrides(4, null, null, null, null, null)), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.Validation, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
        Assert.Null(await _repository.GetAsync(TenantId));
    }

    [Theory]
    [InlineData("15 minutes")]
    [InlineData("")]
    public async Task Handle_FailsWithValidation_WhenTheLockoutDurationIsNotIso8601(string duration)
    {
        var result = await NewHandler().Handle(NewCommand(lockout: new LockoutOverrides(null, duration)), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(IdentityPolicyErrors.LockoutDurationInvalidFormat().Message, result.Errors[0].Message);
    }

    [Fact]
    public async Task Handle_FailsWithValidation_WhenTheLockoutDurationIsTooLong()
    {
        var result = await NewHandler().Handle(NewCommand(lockout: new LockoutOverrides(null, "P2D")), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(IdentityPolicyErrors.LockoutDurationOutOfRange().Message, result.Errors[0].Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ABlankTermsUrl_ClearsTheOverride(string url)
    {
        var result = await NewHandler().Handle(NewCommand(terms: new TermsOverrides(url)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null((await _repository.GetAsync(TenantId))!.TermsUrl);
    }

    [Fact]
    public async Task Handle_FailsWithValidation_WhenTheTermsUrlIsRelative()
    {
        var result = await NewHandler().Handle(NewCommand(terms: new TermsOverrides("/terms")), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(IdentityPolicyErrors.TermsUrlInvalid().Message, result.Errors[0].Message);
        Assert.Null(await _repository.GetAsync(TenantId));
    }
}
