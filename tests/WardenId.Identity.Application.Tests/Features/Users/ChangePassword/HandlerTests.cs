using WardenId.Identity.Application.Features.Users.ChangePassword;

namespace WardenId.Identity.Application.Tests.Features.Users.ChangePassword;

public class HandlerTests
{
    private const string CurrentPassword = "P@ssw0rd1";

    private static async Task<(User User, FakeUserRepository Repository, FakePasswordHasher Hasher)> SeedUserAsync()
    {
        var tenantId = Guid.NewGuid();
        var user = TestUsers.CreatePendingVerification(tenantId);
        var hasher = new FakePasswordHasher();
        user.SetPassword(
            PasswordCredential.Create(hasher.Hash(CurrentPassword), "Argon2id", 1, DateTimeOffset.UtcNow),
            PasswordPolicy.FromTrustedSource(8, true, true, false, true, 5));
        var repository = new FakeUserRepository();
        // Seeded with a different actor so post-Handle actor assertions prove the handler set it.
        await repository.CreateAsync(user, Guid.NewGuid(), CancellationToken.None);
        return (user, repository, hasher);
    }

    [Fact]
    public async Task Handle_UpdatesPassword_WhenCurrentPasswordIsCorrect()
    {
        var (user, repository, hasher) = await SeedUserAsync();
        var handler = new Handler(repository, hasher, new FakeBreachedPasswordChecker(), TestPolicies.Resolver(new IdentityPolicyOptions()));

        var result = await handler.Handle(
            new Command(user.TenantId, user.Id, CurrentPassword, "N3wP@ssword"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(hasher.Verify("N3wP@ssword", user.Credential!.Hash));
        Assert.Equal(1, repository.UpdateCallCount);
        Assert.Equal(user.Id, repository.LastActorId!.Value);
    }

    [Fact]
    public async Task Handle_Fails_WhenCurrentPasswordIsWrong()
    {
        var (user, repository, hasher) = await SeedUserAsync();
        var handler = new Handler(repository, hasher, new FakeBreachedPasswordChecker(), TestPolicies.Resolver(new IdentityPolicyOptions()));

        var result = await handler.Handle(
            new Command(user.TenantId, user.Id, "wrong-current-password", "N3wP@ssword"),
            CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task Handle_DoesNotPersist_WhenCurrentPasswordIsWrong()
    {
        var (user, repository, hasher) = await SeedUserAsync();
        var handler = new Handler(repository, hasher, new FakeBreachedPasswordChecker(), TestPolicies.Resolver(new IdentityPolicyOptions()));

        var result = await handler.Handle(
            new Command(user.TenantId, user.Id, "wrong-current-password", "N3wP@ssword"),
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(0, repository.UpdateCallCount);
        Assert.True(hasher.Verify(CurrentPassword, user.Credential!.Hash));
    }

    [Fact]
    public async Task Handle_Fails_WhenNewPasswordReusesCurrentPassword()
    {
        var (user, repository, hasher) = await SeedUserAsync();
        var handler = new Handler(repository, hasher, new FakeBreachedPasswordChecker(), TestPolicies.Resolver(new IdentityPolicyOptions()));

        var result = await handler.Handle(
            new Command(user.TenantId, user.Id, CurrentPassword, CurrentPassword),
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(0, repository.UpdateCallCount);
    }

    [Fact]
    public async Task Handle_Fails_WhenTheNewPasswordBreaksTheTenantsStricterRules()
    {
        var (user, repository, hasher) = await SeedUserAsync();
        var tenantPolicies = new FakeIdentityTenantPolicyRepository()
            .With(user.TenantId, IdentityPolicyOverrides.None with { PasswordMinLength = 16, PasswordRequireSymbol = true });
        var handler = new Handler(repository, hasher, new FakeBreachedPasswordChecker(),
            TestPolicies.Resolver(new IdentityPolicyOptions(), tenantPolicies));

        var result = await handler.Handle(new Command(user.TenantId, user.Id, CurrentPassword, "N3wP@ssword"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrors.PasswordTooShort(16).Message, result.Errors[0].Message);
        Assert.Equal(0, repository.UpdateCallCount);
    }
}
