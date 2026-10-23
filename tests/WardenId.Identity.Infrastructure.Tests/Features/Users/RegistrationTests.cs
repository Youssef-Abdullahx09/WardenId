using WardenId.Identity.Application;
using WardenId.Identity.Application.Abstractions;
using CreateCommand = WardenId.Identity.Application.Features.Users.Create.Command;
using CreateHandler = WardenId.Identity.Application.Features.Users.Create.Handler;
using RegisterCommand = WardenId.Identity.Application.Features.Users.Register.Command;
using RegisterHandler = WardenId.Identity.Application.Features.Users.Register.Handler;

namespace WardenId.Identity.Infrastructure.Tests.Features.Users;

/// <summary>
/// The unique (TenantId, Email) and (TenantId, Username) indexes cover soft-deleted users, so a
/// soft-deleted user's email and username stay taken (a restore needs them). Registering or
/// creating a user with them must fail with the usual "already in use" error, not a unique
/// violation from Postgres.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class RegistrationTests(PostgresFixture database)
{
    private const string Password = "Correct-Horse-1";

    private static readonly IdentityPolicyResolver Policies =
        GlobalPolicies.Resolver(new IdentityPolicyOptions { PasswordBreachCheckEnabled = false });

    private readonly Guid _tenantId = Guid.NewGuid();

    [SkippableFact]
    public async Task Register_FailsWithEmailAlreadyInUse_WhenASoftDeletedUserHasTheEmail()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var (email, _) = await SeedSoftDeletedUserAsync();

        var result = await InRequestAsync(repository =>
            NewRegisterHandler(repository).Handle(new RegisterCommand(_tenantId, email.ToUpperInvariant(), NewUsername(), null, null, Password), CancellationToken.None));

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrors.EmailAlreadyInUse().Message, result.Errors[0].Message);
    }

    [SkippableFact]
    public async Task Register_FailsWithUsernameAlreadyInUse_WhenASoftDeletedUserHasTheUsername()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var (_, username) = await SeedSoftDeletedUserAsync();

        var result = await InRequestAsync(repository =>
            NewRegisterHandler(repository).Handle(new RegisterCommand(_tenantId, NewEmail(), username, null, null, Password), CancellationToken.None));

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrors.UsernameAlreadyInUse().Message, result.Errors[0].Message);
    }

    [SkippableFact]
    public async Task Create_FailsWithEmailAlreadyInUse_WhenASoftDeletedUserHasTheEmail()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var (email, _) = await SeedSoftDeletedUserAsync();

        var result = await InRequestAsync(repository =>
            NewCreateHandler(repository).Handle(new CreateCommand(_tenantId, email, NewUsername(), null, null, Password, false, Guid.NewGuid()), CancellationToken.None));

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrors.EmailAlreadyInUse().Message, result.Errors[0].Message);
    }

    [SkippableFact]
    public async Task Create_FailsWithUsernameAlreadyInUse_WhenASoftDeletedUserHasTheUsername()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var (_, username) = await SeedSoftDeletedUserAsync();

        var result = await InRequestAsync(repository =>
            NewCreateHandler(repository).Handle(new CreateCommand(_tenantId, NewEmail(), username, null, null, Password, false, Guid.NewGuid()), CancellationToken.None));

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrors.UsernameAlreadyInUse().Message, result.Errors[0].Message);
    }

    [SkippableFact]
    public async Task Register_Succeeds_WhenTheSoftDeletedUserIsInAnotherTenant()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var (email, username) = await SeedSoftDeletedUserAsync();

        var result = await InRequestAsync(repository =>
            NewRegisterHandler(repository).Handle(new RegisterCommand(Guid.NewGuid(), email, username, null, null, Password), CancellationToken.None));

        Assert.True(result.IsSuccess);
    }

    private async Task<(string Email, string Username)> SeedSoftDeletedUserAsync()
    {
        var (email, username) = (NewEmail(), NewUsername());
        var registered = await InRequestAsync(repository =>
            NewRegisterHandler(repository).Handle(new RegisterCommand(_tenantId, email, username, null, null, Password), CancellationToken.None));
        Assert.True(registered.IsSuccess);

        await InRequestAsync(async repository =>
        {
            var user = (await repository.GetByIdAsync(_tenantId, registered.Value.UserId)).Value;
            Assert.True(user.SoftDelete().IsSuccess);
            await repository.SoftDeleteAsync(user, Guid.NewGuid());
            return true;
        });

        return (email, username);
    }

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.test";

    private static string NewUsername() => $"user{Guid.NewGuid().ToString("N")[..12]}";

    private static RegisterHandler NewRegisterHandler(UserRepository repository) =>
        new(repository, new PlainPasswordHasher(), new NeverBreached(), new NoEmail(), Policies);

    private static CreateHandler NewCreateHandler(UserRepository repository) =>
        new(repository, new PlainPasswordHasher(), new NeverBreached(), new NoEmail(), Policies);

    private async Task<T> InRequestAsync<T>(Func<UserRepository, Task<T>> step)
    {
        await using var context = database.CreateContext();
        return await step(new UserRepository(context));
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
