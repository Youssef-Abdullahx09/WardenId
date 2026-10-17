using WardenId.Identity.Application.Features.Users.Register;

namespace WardenId.Identity.Application.Tests.Features.Users.Register;

public class HandlerTests
{
    private static Handler CreateHandler(
        FakeUserRepository repository,
        FakePasswordHasher? passwordHasher = null,
        FakeBreachedPasswordChecker? breachedPasswordChecker = null,
        FakeIdentityEmailSender? emailSender = null,
        IdentityPolicyOptions? options = null) =>
        new(
            repository,
            passwordHasher ?? new FakePasswordHasher(),
            breachedPasswordChecker ?? new FakeBreachedPasswordChecker(),
            emailSender ?? new FakeIdentityEmailSender(),
            TestPolicies.Resolver(options));

    [Fact]
    public async Task Handle_ReturnsUserIdAndRawToken_WhenEmailAndUsernameAreUnique()
    {
        var emailSender = new FakeIdentityEmailSender();
        var handler = CreateHandler(new FakeUserRepository(), emailSender: emailSender);

        var result = await handler.Handle(
            new Command(Guid.NewGuid(), "someone@example.com", "someone", "P@ssw0rd1"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.UserId);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.RawVerificationToken));
        Assert.Single(emailSender.SentVerificationEmails);
        Assert.Equal("someone@example.com", emailSender.SentVerificationEmails[0].ToEmail);
        Assert.Equal(result.Value.RawVerificationToken, emailSender.SentVerificationEmails[0].RawToken);
    }

    [Fact]
    public async Task Handle_Fails_WhenEmailAlreadyInUse()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeUserRepository();
        var handler = CreateHandler(repository);

        await handler.Handle(new Command(tenantId, "dup@example.com", "first", "P@ssw0rd1"), CancellationToken.None);
        var result = await handler.Handle(new Command(tenantId, "dup@example.com", "second", "P@ssw0rd1"), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_Fails_WhenUsernameAlreadyInUse()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeUserRepository();
        var handler = CreateHandler(repository);

        await handler.Handle(new Command(tenantId, "one@example.com", "dupname", "P@ssw0rd1"), CancellationToken.None);
        var result = await handler.Handle(new Command(tenantId, "two@example.com", "dupname", "P@ssw0rd1"), CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_Fails_WhenPasswordViolatesPolicy()
    {
        var handler = CreateHandler(new FakeUserRepository(), options: new IdentityPolicyOptions { PasswordMinLength = 12 });

        var result = await handler.Handle(
            new Command(Guid.NewGuid(), "someone@example.com", "someone", "short1A"),
            CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_Fails_WhenEmailFormatIsInvalid()
    {
        var handler = CreateHandler(new FakeUserRepository());

        var result = await handler.Handle(
            new Command(Guid.NewGuid(), "not-an-email", "someone", "P@ssw0rd1"),
            CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    private static Handler HandlerFor(FakeUserRepository repository, IdentityPolicyOptions global, FakeIdentityTenantPolicyRepository tenantPolicies) =>
        new(repository, new FakePasswordHasher(), new FakeBreachedPasswordChecker(), new FakeIdentityEmailSender(),
            TestPolicies.Resolver(global, tenantPolicies));

    [Fact]
    public async Task Handle_FailsWithForbidden_AndCreatesNoUser_WhenTheTenantDisabledSelfRegistration()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeUserRepository();
        var tenantPolicies = new FakeIdentityTenantPolicyRepository()
            .With(tenantId, IdentityPolicyOverrides.None with { SelfRegistrationEnabled = false });

        var result = await HandlerFor(repository, new IdentityPolicyOptions(), tenantPolicies)
            .Handle(new Command(tenantId, "someone@example.com", "someone", "P@ssw0rd1"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrorCodes.Forbidden, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
        Assert.Equal(0, repository.CreateCallCount);
    }

    [Fact]
    public async Task Handle_FailsWithForbidden_BeforeRevealingThatTheEmailIsTaken()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeUserRepository();
        await CreateHandler(repository).Handle(new Command(tenantId, "taken@example.com", "first", "P@ssw0rd1"), CancellationToken.None);
        var tenantPolicies = new FakeIdentityTenantPolicyRepository()
            .With(tenantId, IdentityPolicyOverrides.None with { SelfRegistrationEnabled = false });

        var result = await HandlerFor(repository, new IdentityPolicyOptions(), tenantPolicies)
            .Handle(new Command(tenantId, "taken@example.com", "second", "P@ssw0rd1"), CancellationToken.None);

        Assert.Equal(UserErrors.SelfRegistrationDisabled().Message, result.Errors[0].Message);
    }

    [Fact]
    public async Task Handle_FailsWithForbidden_WhenSelfRegistrationIsDisabledGlobally()
    {
        var result = await CreateHandler(new FakeUserRepository(), options: new IdentityPolicyOptions { SelfRegistrationEnabled = false })
            .Handle(new Command(Guid.NewGuid(), "someone@example.com", "someone", "P@ssw0rd1"), CancellationToken.None);

        Assert.Equal(UserErrorCodes.Forbidden, result.Errors[0].Metadata[UserErrorCodes.MetadataKey]);
    }

    [Fact]
    public async Task Handle_Succeeds_WhenTheTenantEnablesSelfRegistration_OverAGlobalOff()
    {
        var tenantId = Guid.NewGuid();
        var tenantPolicies = new FakeIdentityTenantPolicyRepository()
            .With(tenantId, IdentityPolicyOverrides.None with { SelfRegistrationEnabled = true });

        var result = await HandlerFor(new FakeUserRepository(), new IdentityPolicyOptions { SelfRegistrationEnabled = false }, tenantPolicies)
            .Handle(new Command(tenantId, "someone@example.com", "someone", "P@ssw0rd1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Handle_AppliesTheTenantsPasswordRules()
    {
        var tenantId = Guid.NewGuid();
        var tenantPolicies = new FakeIdentityTenantPolicyRepository()
            .With(tenantId, IdentityPolicyOverrides.None with { PasswordMinLength = 16 });

        var result = await HandlerFor(new FakeUserRepository(), new IdentityPolicyOptions(), tenantPolicies)
            .Handle(new Command(tenantId, "someone@example.com", "someone", "P@ssw0rd1"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrors.PasswordTooShort(16).Message, result.Errors[0].Message);
    }
}
