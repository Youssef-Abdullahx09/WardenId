using Microsoft.EntityFrameworkCore;

namespace WardenId.Identity.Infrastructure.Tests.Persistence;

/// <summary>
/// Every step runs on a fresh DbContext, like separate requests, so what a test sees after a
/// save is what Postgres holds, not what the change tracker remembers.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class UserRepositoryTests(PostgresFixture database)
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly MfaPolicy OptionalMfa = MfaPolicy.Create(MfaPolicyMode.Optional);
    private static readonly PasswordPolicy KeepTwoPasswords = PasswordPolicy.FromTrustedSource(8, false, false, false, false, historyCount: 2);

    private readonly Guid _tenantId = Guid.NewGuid();

    [SkippableFact]
    public async Task RevokingAnMfaFactor_DeletesItsRow()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var userId = await SeedUserAsync();
        var revokedId = Guid.Empty;

        await InRequestAsync(async repository =>
        {
            var user = await LoadAsync(repository, userId);
            revokedId = user.MfaFactors.First().Id;
            Assert.True(user.RevokeMfaFactor(revokedId, OptionalMfa, userIsAdmin: false).IsSuccess);
            await repository.UpdateAsync(user, Actor);
        });

        var reloaded = await InRequestAsync(repository => LoadAsync(repository, userId));
        Assert.Single(reloaded.MfaFactors);
        Assert.DoesNotContain(reloaded.MfaFactors, f => f.Id == revokedId);
        Assert.Equal(0, await database.CountAsync("""SELECT count(*) FROM "Identity"."MfaFactors" WHERE "Id" = $1""", revokedId));
    }

    [SkippableFact]
    public async Task RevokingTheLastFactor_DeletesTheRecoveryCodeRows()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var userId = await SeedUserAsync();
        var codeIds = Array.Empty<Guid>();

        await InRequestAsync(async repository =>
        {
            var user = await LoadAsync(repository, userId);
            codeIds = user.RecoveryCodes.Select(c => c.Id).ToArray();
            foreach (var factorId in user.MfaFactors.Select(f => f.Id).ToList())
                Assert.True(user.RevokeMfaFactor(factorId, OptionalMfa, userIsAdmin: false).IsSuccess);
            await repository.UpdateAsync(user, Actor);
        });

        var reloaded = await InRequestAsync(repository => LoadAsync(repository, userId));
        Assert.Empty(reloaded.MfaFactors);
        Assert.Empty(reloaded.RecoveryCodes);
        Assert.Equal(0, await database.CountAsync("""SELECT count(*) FROM "Identity"."RecoveryCodes" WHERE "Id" = ANY($1)""", codeIds));
        Assert.Equal(0, await database.CountAsync("""SELECT count(*) FROM "Identity"."MfaFactors" WHERE "UserId" IS NULL"""));
    }

    [SkippableFact]
    public async Task ChangingThePassword_KeepsOneCredentialRow_AndOnlyThePolicysHistoryRows()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var userId = await SeedUserAsync();
        var credentialIds = new HashSet<Guid>();
        var historyIds = new HashSet<Guid>();

        for (var change = 1; change <= 4; change++)
        {
            var hash = $"hash-{change}";
            await InRequestAsync(async repository =>
            {
                var user = await LoadAsync(repository, userId);
                credentialIds.Add(user.Credential!.Id);
                Assert.True(user.SetPassword(PasswordCredential.Create(hash, "argon2id", 1, DateTimeOffset.UtcNow), KeepTwoPasswords).IsSuccess);
                await repository.UpdateAsync(user, Actor);
                credentialIds.Add(user.Credential!.Id);
                historyIds.UnionWith(user.PasswordHistory.Select(h => h.Id));
            });
        }

        var reloaded = await InRequestAsync(repository => LoadAsync(repository, userId));
        Assert.Equal("hash-4", reloaded.Credential!.Hash);
        Assert.Equal(["hash-2", "hash-3"], reloaded.PasswordHistory.OrderBy(h => h.SetAtUtc).Select(h => h.Hash));
        Assert.Equal(1, await database.CountAsync("""SELECT count(*) FROM "Identity"."PasswordCredentials" WHERE "Id" = ANY($1)""", credentialIds.ToArray()));
        Assert.Equal(2, await database.CountAsync("""SELECT count(*) FROM "Identity"."PasswordHistoryEntries" WHERE "Id" = ANY($1)""", historyIds.ToArray()));
    }

    [SkippableFact]
    public async Task SoftDeletingAUser_KeepsItsLockoutAndChildren_AndRestoreBringsThemBack()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var userId = await SeedUserAsync();

        await InRequestAsync(async repository =>
        {
            var user = await LoadAsync(repository, userId);
            user.SetAttribute("department", "finance", UserAttributeValueType.String);
            user.RecordFailedLogin(maxFailedAttempts: 5, TimeSpan.FromMinutes(15));
            await repository.UpdateAsync(user, Actor);
        });

        await InRequestAsync(async repository =>
        {
            var user = await LoadAsync(repository, userId);
            Assert.True(user.SoftDelete().IsSuccess);
            await repository.SoftDeleteAsync(user, Actor);
        });

        var deleted = await InRequestAsync(async repository =>
        {
            Assert.True((await repository.GetByIdAsync(_tenantId, userId)).IsFailed);
            return (await repository.GetByIdIncludingDeletedAsync(_tenantId, userId)).Value;
        });
        Assert.True(deleted.IsDeleted);
        Assert.Equal(UserStatus.SoftDeleted, deleted.Status);
        AssertKeptEverything(deleted);
        Assert.Equal(0, await CountChildRowsAsync(userId, isDeleted: false));

        await InRequestAsync(async repository =>
        {
            var user = (await repository.GetByIdIncludingDeletedAsync(_tenantId, userId)).Value;
            Assert.True(user.Restore().IsSuccess);
            await repository.RestoreAsync(user, Actor);
        });

        var restored = await InRequestAsync(repository => LoadAsync(repository, userId));
        Assert.False(restored.IsDeleted);
        Assert.Equal(UserStatus.Disabled, restored.Status);
        AssertKeptEverything(restored);
        Assert.Equal(0, await CountChildRowsAsync(userId, isDeleted: true));
    }

    private async Task<long> CountChildRowsAsync(Guid userId, bool isDeleted)
    {
        string[] tables = ["PasswordCredentials", "MfaFactors", "RecoveryCodes", "UserAttributes", "PasswordHistoryEntries", "EmailVerificationTokens", "PasswordResetTokens"];
        var count = 0L;
        foreach (var table in tables)
            count += await database.CountAsync($"""SELECT count(*) FROM "Identity"."{table}" WHERE "UserId" = $1 AND "IsDeleted" = $2""", userId, isDeleted);
        return count;
    }

    [SkippableFact]
    public async Task Migrations_MatchTheModel()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        await using var context = database.CreateContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static void AssertKeptEverything(User user)
    {
        Assert.Equal(1, user.Lockout.FailedAttemptCount);
        Assert.Equal("hash-0", user.Credential?.Hash);
        Assert.Equal(2, user.MfaFactors.Count);
        Assert.Equal(2, user.RecoveryCodes.Count);
        Assert.Single(user.Attributes);
    }

    private async Task<Guid> SeedUserAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var user = User.Register(_tenantId, Email.FromTrustedSource($"{suffix}@example.test"), Username.FromTrustedSource($"user{suffix}")).Value;
        user.SetPassword(PasswordCredential.Create("hash-0", "argon2id", 1, DateTimeOffset.UtcNow), KeepTwoPasswords);

        var first = MfaFactor.Create(MfaFactorType.Totp, "secret-1");
        var second = MfaFactor.Create(MfaFactorType.Totp, "secret-2");
        user.EnrollMfaFactor(first, [RecoveryCode.Create("code-1"), RecoveryCode.Create("code-2")]);
        user.EnrollMfaFactor(second, []);
        first.Verify();
        second.Verify();

        await InRequestAsync(repository => repository.CreateAsync(user, Actor));
        return user.Id;
    }

    private async Task<User> LoadAsync(UserRepository repository, Guid userId) =>
        (await repository.GetByIdAsync(_tenantId, userId)).Value;

    private async Task InRequestAsync(Func<UserRepository, Task> step)
    {
        await using var context = database.CreateContext();
        await step(new UserRepository(context));
    }

    private async Task<T> InRequestAsync<T>(Func<UserRepository, Task<T>> step)
    {
        await using var context = database.CreateContext();
        return await step(new UserRepository(context));
    }
}
