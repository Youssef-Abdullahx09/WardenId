using WardenId.Identity.Integration.Command.Features.Users.GetAccount;
using ContractRequiredAction = WardenId.Identity.Integration.Command.Features.Users.Authenticate.RequiredAction;
using ContractUserStatus = WardenId.Identity.Integration.Command.Features.Users.Authenticate.UserStatus;
using GetAccountHandler = WardenId.Identity.Application.Features.Users.GetAccount.Handler;
using GetAccountQuery = WardenId.Identity.Integration.Command.Features.Users.GetAccount.Query;
using GetForSignInHandler = WardenId.Identity.Application.Features.Users.GetForSignIn.Handler;
using GetForSignInQuery = WardenId.Identity.Integration.Command.Features.Users.GetForSignIn.Query;
using GetHandler = WardenId.Identity.Application.Features.Users.Get.Handler;
using GetQuery = WardenId.Identity.Application.Features.Users.Get.Query;

namespace WardenId.Identity.Infrastructure.Tests.Features.Users;

/// <summary>
/// The single-user reads over Identity.Query's SQL on Postgres: column names, the owned lockout columns, the
/// PendingActions text[], enums by name, timestamptz as UTC, unused recovery codes counted, soft-deleted users hidden.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class UserQueriesTests(PostgresFixture database)
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid _tenantId = Guid.NewGuid();

    [SkippableFact]
    public async Task Get_ReturnsTheUser_WithAttributesFactorsAndRemainingRecoveryCodes()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var (user, enabled, pending) = await SeedUserWithMfaAsync();

        var result = await new GetHandler(database.CreateUsersQuery()).Handle(new GetQuery(_tenantId, user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var detail = result.Value;
        Assert.Equal(user.Id, detail.Id);
        Assert.Equal(user.Email.Value, detail.Email);
        Assert.Equal(user.Username.Value, detail.Username);
        Assert.Equal("Ada", detail.FirstName);
        Assert.Equal("Lovelace", detail.LastName);
        Assert.Equal(UserStatus.PendingVerification, detail.Status);
        Assert.Equal(0, detail.FailedLoginAttemptCount);
        Assert.Null(detail.LockedUntilUtc);
        Assert.Equal([RequiredAction.VerifyEmail], detail.PendingActions);
        Assert.Equal(Now, detail.TermsAcceptedAtUtc);
        Assert.NotEqual(default, detail.CreatedAt);
        var attribute = Assert.Single(detail.Attributes);
        Assert.Equal(("department", "engineering", UserAttributeValueType.String), (attribute.Key, attribute.Value, attribute.ValueType));
        Assert.Equal([enabled.Id, pending.Id], detail.MfaFactors.Select(f => f.Id));
        Assert.True(detail.MfaFactors.First().Enabled);
        Assert.Equal(MfaFactorType.Totp, detail.MfaFactors.First().FactorType);
        Assert.Equal(Now.AddMinutes(-1), detail.MfaFactors.First().VerifiedAtUtc);
        Assert.False(detail.MfaFactors.Last().Enabled);
        Assert.Null(detail.MfaFactors.Last().VerifiedAtUtc);
        Assert.Equal(1, detail.RemainingRecoveryCodes);
    }

    [SkippableFact]
    public async Task GetAccount_ReturnsOnlyEnabledFactors_AndRemainingRecoveryCodes()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var (user, enabled, _) = await SeedUserWithMfaAsync();

        var result = await new GetAccountHandler(database.CreateUsersQuery(), GlobalPolicies.Resolver(new WardenId.Identity.Application.IdentityPolicyOptions()))
            .Handle(new GetAccountQuery(_tenantId, user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Email.Value, result.Value.Email);
        Assert.Equal(Now, result.Value.TermsAcceptedAtUtc);
        Assert.Equal(
            new AccountFactor(enabled.Id, AccountFactorType.Totp, enabled.EnrolledAtUtc, enabled.LastUsedAtUtc),
            Assert.Single(result.Value.Factors));
        Assert.Equal(1, result.Value.RemainingRecoveryCodes);
    }

    [SkippableFact]
    public async Task GetForSignIn_ReturnsNamesStatusAndPendingActions()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var (user, _, _) = await SeedUserWithMfaAsync();

        var result = await new GetForSignInHandler(database.CreateUsersQuery())
            .Handle(new GetForSignInQuery(_tenantId, user.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(user.Id, result.Value.UserId);
        Assert.Equal("Ada", result.Value.FirstName);
        Assert.Equal("Lovelace", result.Value.LastName);
        Assert.Equal(ContractUserStatus.PendingVerification, result.Value.Status);
        Assert.Equal([ContractRequiredAction.VerifyEmail], result.Value.PendingActions);
    }

    [SkippableFact]
    public async Task EveryRead_IsNotFound_ForASoftDeletedUser_OrAnotherTenant()
    {
        Skip.If(database.SkipReason is not null, database.SkipReason);
        var (user, _, _) = await SeedUserWithMfaAsync();
        var otherTenant = Guid.NewGuid();
        Assert.True((await new GetHandler(database.CreateUsersQuery()).Handle(new GetQuery(otherTenant, user.Id), CancellationToken.None)).IsFailed);

        await using (var context = database.CreateContext())
        {
            var repository = new UserRepository(context);
            var loaded = (await repository.GetByIdAsync(_tenantId, user.Id)).Value;
            Assert.True(loaded.SoftDelete().IsSuccess);
            Assert.True((await repository.SoftDeleteAsync(loaded, Guid.NewGuid())).IsSuccess);
        }

        var get = await new GetHandler(database.CreateUsersQuery()).Handle(new GetQuery(_tenantId, user.Id), CancellationToken.None);
        var account = await new GetAccountHandler(database.CreateUsersQuery(), GlobalPolicies.Resolver(new WardenId.Identity.Application.IdentityPolicyOptions()))
            .Handle(new GetAccountQuery(_tenantId, user.Id), CancellationToken.None);
        var signIn = await new GetForSignInHandler(database.CreateUsersQuery())
            .Handle(new GetForSignInQuery(_tenantId, user.Id), CancellationToken.None);

        Assert.All([get.Errors, account.Errors, signIn.Errors], errors =>
            Assert.Equal(UserErrorCodes.NotFound, errors[0].Metadata[UserErrorCodes.MetadataKey]));
    }

    /// <summary>A user with an attribute, accepted terms, an enabled TOTP factor (two recovery codes, one used) and a pending one.</summary>
    private async Task<(User User, MfaFactor Enabled, MfaFactor Pending)> SeedUserWithMfaAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var user = User.Register(_tenantId, Email.Create($"someone{suffix}@example.com").Value, Username.Create($"Someone{suffix}").Value,
            PersonName.FromTrustedSource("Ada"), PersonName.FromTrustedSource("Lovelace")).Value;
        Assert.True(user.SetAttribute("department", "engineering", UserAttributeValueType.String).IsSuccess);
        Assert.True(user.AcceptTerms(Now).IsSuccess);
        var enabled = MfaFactor.Create(MfaFactorType.Totp, "protected:enabled", Now.AddMinutes(-2));
        Assert.True(user.BeginMfaEnrollment(enabled).IsSuccess);
        Assert.True(user.CompleteMfaEnrollment(enabled.Id, 1, [RecoveryCode.Create("hash-1"), RecoveryCode.Create("hash-2")], Now.AddMinutes(-1)).IsSuccess);
        Assert.True(user.ConsumeRecoveryCode("hash-1", Now).IsSuccess);
        var pending = MfaFactor.Create(MfaFactorType.Totp, "protected:pending", Now);
        Assert.True(user.BeginMfaEnrollment(pending).IsSuccess);

        await using var context = database.CreateContext();
        await new UserRepository(context).CreateAsync(user, Guid.NewGuid());
        return (user, enabled, pending);
    }
}
