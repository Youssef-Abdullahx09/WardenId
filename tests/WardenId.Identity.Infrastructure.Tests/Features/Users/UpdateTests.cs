using WardenId.Identity.Application;
using WardenId.Identity.Application.Abstractions;
using RegisterCommand = WardenId.Identity.Application.Features.Users.Register.Command;
using RegisterHandler = WardenId.Identity.Application.Features.Users.Register.Handler;
using UpdateCommand = WardenId.Identity.Application.Features.Users.Update.Command;
using UpdateHandler = WardenId.Identity.Application.Features.Users.Update.Handler;
using VerifyEmailCommand = WardenId.Identity.Application.Features.Users.VerifyEmail.Command;
using VerifyEmailHandler = WardenId.Identity.Application.Features.Users.VerifyEmail.Handler;

namespace WardenId.Identity.Infrastructure.Tests.Features.Users;

/// <summary>
/// Changing a user's email or username on Postgres: soft-deleted users keep theirs taken (the
/// unique indexes cover them), the old address's tokens are deleted, and the audit diff — the
/// generic capture reads the change tracker's original and current scalar values — sees both
/// the old and the new value.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class UpdateTests(PostgresFixture database)
{
    private const string Password = "Correct-Horse-1";

    private static readonly IdentityPolicyResolver Policies =
        GlobalPolicies.Resolver(new IdentityPolicyOptions { PasswordBreachCheckEnabled = false });

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly RecordingEmailSender _emailSender = new();

    [SkippableFact]
    public async Task ChangingTheEmail_ToASoftDeletedUsersEmail_FailsWithEmailAlreadyInUse()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var (deletedEmail, _) = await SeedSoftDeletedUserAsync();
        var (userId, _, _) = await SeedVerifiedUserAsync();

        var result = await InRequestAsync(repository =>
            NewUpdateHandler(repository).Handle(new UpdateCommand(_tenantId, userId, deletedEmail.ToUpperInvariant(), null, Guid.NewGuid()), CancellationToken.None));

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrors.EmailAlreadyInUse().Message, result.Errors[0].Message);
    }

    [SkippableFact]
    public async Task ChangingTheUsername_ToASoftDeletedUsersUsername_FailsWithUsernameAlreadyInUse()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var (_, deletedUsername) = await SeedSoftDeletedUserAsync();
        var (userId, _, _) = await SeedVerifiedUserAsync();

        var result = await InRequestAsync(repository =>
            NewUpdateHandler(repository).Handle(new UpdateCommand(_tenantId, userId, null, deletedUsername, Guid.NewGuid()), CancellationToken.None));

        Assert.True(result.IsFailed);
        Assert.Equal(UserErrors.UsernameAlreadyInUse().Message, result.Errors[0].Message);
    }

    [SkippableFact]
    public async Task ChangingTheEmail_DeletesTheOldAddressTokenRows_AndTheNewTokenVerifiesIt()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var (userId, _, _) = await SeedVerifiedUserAsync();
        var oldTokenIds = await InRequestAsync(async repository =>
        {
            var user = (await repository.GetByIdAsync(_tenantId, userId)).Value;
            user.RequestPasswordReset(PasswordResetToken.Create("reset-hash", DateTimeOffset.UtcNow.AddHours(1)));
            await repository.UpdateAsync(user, userId);
            return user.EmailVerificationTokens.Select(t => t.Id).Concat(user.PasswordResetTokens.Select(t => t.Id)).ToArray();
        });
        var newEmail = NewEmail();
        var newUsername = NewUsername();

        var result = await InRequestAsync(repository =>
            NewUpdateHandler(repository).Handle(new UpdateCommand(_tenantId, userId, newEmail, newUsername, Guid.NewGuid()), CancellationToken.None));

        Assert.True(result.IsSuccess);
        var reloaded = await InRequestAsync(async repository => (await repository.GetByIdAsync(_tenantId, userId)).Value);
        Assert.Equal(newEmail, reloaded.Email.Value);
        Assert.Equal(newUsername, reloaded.Username.Value);
        Assert.Equal(UserStatus.Active, reloaded.Status);
        Assert.Equal([RequiredAction.VerifyEmail], reloaded.PendingActions);
        Assert.Single(reloaded.EmailVerificationTokens);
        Assert.Empty(reloaded.PasswordResetTokens);
        Assert.Equal(0, await database.CountAsync(
            """SELECT count(*) FROM "Identity"."EmailVerificationTokens" WHERE "Id" = ANY($1)""", oldTokenIds));
        Assert.Equal(0, await database.CountAsync(
            """SELECT count(*) FROM "Identity"."PasswordResetTokens" WHERE "Id" = ANY($1)""", oldTokenIds));

        var sent = _emailSender.Sent.Last();
        Assert.Equal(newEmail, sent.ToEmail);
        var verify = await InRequestAsync(repository =>
            new VerifyEmailHandler(repository).Handle(new VerifyEmailCommand(_tenantId, userId, sent.RawToken), CancellationToken.None));
        Assert.True(verify.IsSuccess);
        var verified = await InRequestAsync(async repository => (await repository.GetByIdAsync(_tenantId, userId)).Value);
        Assert.Equal(UserStatus.Active, verified.Status);
        Assert.Empty(verified.PendingActions);
    }

    [SkippableFact]
    public async Task ChangingTheEmailAndUsername_ShowsOldAndNewValuesToTheAuditDiff()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var (userId, oldEmail, oldUsername) = await SeedVerifiedUserAsync();
        var newEmail = Email.Create(NewEmail()).Value;
        var newUsername = Username.Create(NewUsername()).Value;

        await using var context = database.CreateContext();
        var user = (await new UserRepository(context).GetByIdAsync(_tenantId, userId)).Value;
        Assert.True(user.ChangeEmail(newEmail, EmailVerificationToken.Create("new-hash", DateTimeOffset.UtcNow.AddHours(1))).IsSuccess);
        Assert.True(user.ChangeUsername(newUsername).IsSuccess);
        context.ChangeTracker.DetectChanges();

        // What AuditCaptureInterceptor renders: modified scalar properties, value?.ToString().
        // Email and username aren't in User.AuditSensitivePropertyNames, so they stay plain.
        var entry = context.Entry(user);
        Assert.True(entry.Property(u => u.Email).IsModified);
        Assert.Equal(oldEmail, entry.Property(u => u.Email).OriginalValue.ToString());
        Assert.Equal(newEmail.Value, entry.Property(u => u.Email).CurrentValue.ToString());
        Assert.True(entry.Property(u => u.Username).IsModified);
        Assert.Equal(oldUsername, entry.Property(u => u.Username).OriginalValue.ToString());
        Assert.Equal(newUsername.Value, entry.Property(u => u.Username).CurrentValue.ToString());
        Assert.DoesNotContain(nameof(User.Email), user.AuditSensitivePropertyNames);
        Assert.DoesNotContain(nameof(User.Username), user.AuditSensitivePropertyNames);
    }

    private async Task<(Guid UserId, string Email, string Username)> SeedVerifiedUserAsync()
    {
        var (email, username) = (NewEmail(), NewUsername());
        var registered = await InRequestAsync(repository =>
            NewRegisterHandler(repository).Handle(new RegisterCommand(_tenantId, email, username, Password), CancellationToken.None));
        Assert.True(registered.IsSuccess);

        var verify = await InRequestAsync(repository =>
            new VerifyEmailHandler(repository).Handle(
                new VerifyEmailCommand(_tenantId, registered.Value.UserId, registered.Value.RawVerificationToken), CancellationToken.None));
        Assert.True(verify.IsSuccess);

        return (registered.Value.UserId, email, username);
    }

    private async Task<(string Email, string Username)> SeedSoftDeletedUserAsync()
    {
        var (email, username) = (NewEmail(), NewUsername());
        var registered = await InRequestAsync(repository =>
            NewRegisterHandler(repository).Handle(new RegisterCommand(_tenantId, email, username, Password), CancellationToken.None));
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

    private RegisterHandler NewRegisterHandler(UserRepository repository) =>
        new(repository, new PlainPasswordHasher(), new NeverBreached(), _emailSender, Policies);

    private UpdateHandler NewUpdateHandler(UserRepository repository) => new(repository, _emailSender);

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

    private sealed class RecordingEmailSender : IIdentityEmailSender
    {
        public List<(string ToEmail, string RawToken)> Sent { get; } = [];

        public Task SendEmailVerificationAsync(string toEmail, string username, string rawToken, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken)
        {
            Sent.Add((toEmail, rawToken));
            return Task.CompletedTask;
        }

        public Task SendPasswordResetAsync(string toEmail, string username, string rawToken, DateTimeOffset expiresAtUtc, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
