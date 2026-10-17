using Microsoft.AspNetCore.DataProtection;
using WardenId.Identity.Application;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Application.Mfa;
using AcceptTermsCommand = WardenId.Identity.Integration.Command.Features.Users.AcceptTerms.Command;
using AcceptTermsHandler = WardenId.Identity.Application.Features.Users.AcceptTerms.Handler;
using BeginReplacementCommand = WardenId.Identity.Integration.Command.Features.Users.BeginTotpReplacement.Command;
using BeginReplacementHandler = WardenId.Identity.Application.Features.Users.BeginTotpReplacement.Handler;
using CompleteCommand = WardenId.Identity.Integration.Command.Features.Users.CompleteTotpEnrollment.Command;
using CompleteHandler = WardenId.Identity.Application.Features.Users.CompleteTotpEnrollment.Handler;
using RegenerateCommand = WardenId.Identity.Integration.Command.Features.Users.RegenerateRecoveryCodes.Command;
using RegenerateHandler = WardenId.Identity.Application.Features.Users.RegenerateRecoveryCodes.Handler;
using RemoveMfaCommand = WardenId.Identity.Integration.Command.Features.Users.RemoveMfa.Command;
using RemoveMfaHandler = WardenId.Identity.Application.Features.Users.RemoveMfa.Handler;

namespace WardenId.Identity.Infrastructure.Tests.Features.Users;

/// <summary>
/// The account page's changes and the accept-terms step on Postgres, each step on its own DbContext like separate
/// requests: what a test sees is what the database holds, removed factors and codes included.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AccountTests(PostgresFixture database)
{
    private static readonly byte[] Secret = "12345678901234567890"u8.ToArray();
    private static readonly ITotpSecretProtector Protector = new DataProtectionTotpSecretProtector(new EphemeralDataProtectionProvider());
    private static readonly IdentityPolicyOptions Options = new() { MaxFailedLoginAttempts = 100 };

    private readonly Guid _tenantId = Guid.NewGuid();

    [SkippableFact]
    public async Task AcceptTerms_StoresTheTime_AndClearsAcceptTos()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var userId = await SeedUserAsync(acceptTos: true);

        await using (var context = database.CreateContext())
        {
            var result = await new AcceptTermsHandler(new UserRepository(context), TimeProvider.System)
                .Handle(new AcceptTermsCommand(_tenantId, userId), CancellationToken.None);
            Assert.True(result.IsSuccess);
        }

        var user = await LoadAsync(userId);
        Assert.NotNull(user.TermsAcceptedAtUtc);
        Assert.DoesNotContain(RequiredAction.AcceptTos, user.PendingActions);
    }

    [SkippableFact]
    public async Task ReplacingTheAuthenticator_DeletesTheOldFactorsRow()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var userId = await SeedUserAsync();
        var oldFactorId = (await LoadAsync(userId)).MfaFactors.Single().Id;

        Guid newFactorId;
        await using (var context = database.CreateContext())
        {
            var begin = await new BeginReplacementHandler(new UserRepository(context), GlobalPolicies.Resolver(Options), Protector, TimeProvider.System)
                .Handle(new BeginReplacementCommand(_tenantId, userId, CodeNow(Secret), "Acme"), CancellationToken.None);
            Assert.True(begin.IsSuccess);
            newFactorId = begin.Value.FactorId;
        }

        var newSecret = Protector.TryUnprotect((await LoadAsync(userId)).MfaFactors.Single(f => f.Id == newFactorId).CredentialData)!;
        await using (var context = database.CreateContext())
        {
            var complete = await new CompleteHandler(new UserRepository(context), Protector, TimeProvider.System)
                .Handle(new CompleteCommand(_tenantId, userId, newFactorId, CodeNow(newSecret)), CancellationToken.None);
            Assert.True(complete.IsSuccess);
        }

        var user = await LoadAsync(userId);
        Assert.Equal(newFactorId, Assert.Single(user.MfaFactors).Id);
        Assert.Equal(0, await database.CountAsync("""SELECT count(*) FROM "Identity"."MfaFactors" WHERE "Id" = $1""", oldFactorId));
        Assert.Equal(RecoveryCodes.Count, user.RemainingRecoveryCodeCount);
    }

    [SkippableFact]
    public async Task RegeneratingRecoveryCodes_DeletesTheOldRows()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var userId = await SeedUserAsync();
        var oldCodeIds = (await LoadAsync(userId)).RecoveryCodes.Select(c => c.Id).ToArray();

        await using (var context = database.CreateContext())
        {
            var result = await new RegenerateHandler(new UserRepository(context), GlobalPolicies.Resolver(Options), Protector, TimeProvider.System)
                .Handle(new RegenerateCommand(_tenantId, userId, CodeNow(Secret)), CancellationToken.None);
            Assert.True(result.IsSuccess);
        }

        Assert.Equal(RecoveryCodes.Count, (await LoadAsync(userId)).RemainingRecoveryCodeCount);
        Assert.Equal(0, await database.CountAsync("""SELECT count(*) FROM "Identity"."RecoveryCodes" WHERE "Id" = ANY($1)""", oldCodeIds));
    }

    [SkippableFact]
    public async Task RemovingMfa_DeletesEveryFactorAndCodeRow()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var userId = await SeedUserAsync();

        await using (var context = database.CreateContext())
        {
            var result = await new RemoveMfaHandler(new UserRepository(context), GlobalPolicies.Resolver(Options), Protector, TimeProvider.System)
                .Handle(new RemoveMfaCommand(_tenantId, userId, CodeNow(Secret)), CancellationToken.None);
            Assert.True(result.IsSuccess);
        }

        var user = await LoadAsync(userId);
        Assert.Empty(user.MfaFactors);
        Assert.Empty(user.RecoveryCodes);
        Assert.Equal(0, await database.CountAsync("""SELECT count(*) FROM "Identity"."MfaFactors" WHERE "UserId" = $1""", userId));
        Assert.Equal(0, await database.CountAsync("""SELECT count(*) FROM "Identity"."RecoveryCodes" WHERE "UserId" = $1""", userId));
    }

    private static string CodeNow(byte[] secret) => Totp.ComputeCode(secret, Totp.TimeStepAt(DateTimeOffset.UtcNow));

    private async Task<Guid> SeedUserAsync(bool acceptTos = false)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var user = User.Register(_tenantId, Email.FromTrustedSource($"{suffix}@example.test"), Username.FromTrustedSource($"user{suffix}")).Value;
        if (acceptTos) user.AddRequiredAction(RequiredAction.AcceptTos);
        var factor = MfaFactor.Create(MfaFactorType.Totp, Protector.Protect(Secret), DateTimeOffset.UtcNow.AddHours(-1));
        user.BeginMfaEnrollment(factor);
        // The first code was an hour ago, so every code around now is newer.
        user.CompleteMfaEnrollment(factor.Id, Totp.TimeStepAt(DateTimeOffset.UtcNow.AddHours(-1)),
            [RecoveryCode.Create("code-1"), RecoveryCode.Create("code-2")], DateTimeOffset.UtcNow.AddHours(-1));

        await using var context = database.CreateContext();
        await new UserRepository(context).CreateAsync(user, user.Id);
        return user.Id;
    }

    private async Task<User> LoadAsync(Guid userId)
    {
        await using var context = database.CreateContext();
        return (await new UserRepository(context).GetByIdAsync(_tenantId, userId)).Value;
    }
}
