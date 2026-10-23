using MediatR;
using Microsoft.AspNetCore.DataProtection;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetReference;
using WardenId.Identity.Integration.Command.Features.Users.Authenticate;
using WardenId.Identity.Integration.Command.Features.Users.GetForSignIn;
using WardenId.Tenancy.Application.Abstractions;
using WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys;
using WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance;
using AuthenticateCommand = WardenId.Identity.Integration.Command.Features.Users.Authenticate.Command;
using GetForSignInQuery = WardenId.Identity.Integration.Command.Features.Users.GetForSignIn.Query;
using GetActiveForSigningQuery = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning.Query;
using GetForAuthenticationQuery = WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication.Query;
using GetPublicKeysQuery = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys.Query;
using ResolveForIssuanceQuery = WardenId.Tenancy.Integration.Command.Features.Tenants.ResolveForIssuance.Query;
using TenancyActiveSigningKey = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning.ActiveSigningKey;
using GetReferenceQuery = WardenId.ClientManagement.Integration.Command.Features.Clients.GetReference.Query;
using ListCorsOriginsQuery = WardenId.ClientManagement.Integration.Command.Features.Clients.ListCorsOrigins.Query;
using VerifySecretQuery = WardenId.ClientManagement.Integration.Command.Features.Clients.VerifySecret.Query;
using RecordAuditEventCommand = WardenId.Audit.Integration.Command.Features.Events.Record.Command;
using WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor;
using BeginTotpEnrollmentCommand = WardenId.Identity.Integration.Command.Features.Users.BeginTotpEnrollment.Command;
using TotpEnrollment = WardenId.Identity.Integration.Command.Features.Users.BeginTotpEnrollment.TotpEnrollment;
using CompleteTotpEnrollmentCommand = WardenId.Identity.Integration.Command.Features.Users.CompleteTotpEnrollment.Command;
using CompletedTotpEnrollment = WardenId.Identity.Integration.Command.Features.Users.CompleteTotpEnrollment.CompletedTotpEnrollment;
using VerifySecondFactorCommand = WardenId.Identity.Integration.Command.Features.Users.VerifySecondFactor.Command;
using AcceptTermsCommand = WardenId.Identity.Integration.Command.Features.Users.AcceptTerms.Command;
using AccountFactor = WardenId.Identity.Integration.Command.Features.Users.GetAccount.AccountFactor;
using AccountFactorType = WardenId.Identity.Integration.Command.Features.Users.GetAccount.AccountFactorType;
using AccountView = WardenId.Identity.Integration.Command.Features.Users.GetAccount.AccountView;
using BeginTotpReplacementCommand = WardenId.Identity.Integration.Command.Features.Users.BeginTotpReplacement.Command;
using ChangeOwnPasswordCommand = WardenId.Identity.Integration.Command.Features.Users.ChangeOwnPassword.Command;
using GetAccountQuery = WardenId.Identity.Integration.Command.Features.Users.GetAccount.Query;
using RegenerateRecoveryCodesCommand = WardenId.Identity.Integration.Command.Features.Users.RegenerateRecoveryCodes.Command;
using RemoveMfaCommand = WardenId.Identity.Integration.Command.Features.Users.RemoveMfa.Command;

namespace WardenId.Protocol.Presentation.Tests.Fakes;

internal static class Fail
{
    public static Error NotFound => new Error("Not found.").WithMetadata("ErrorCode", "NotFound");
}

public sealed class ResolveForIssuanceHandler(FakeDirectory directory)
    : IRequestHandler<ResolveForIssuanceQuery, Result<TenantForIssuance>>
{
    public Task<Result<TenantForIssuance>> Handle(ResolveForIssuanceQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(directory.Tenants.TryGetValue(request.Slug, out var t)
            ? Result.Ok(new TenantForIssuance(t.TenantId, t.Slug, t.Status, t.DisplayName))
            : Result.Fail<TenantForIssuance>(Fail.NotFound));
}

public sealed class GetPublicKeysHandler(FakeDirectory directory)
    : IRequestHandler<GetPublicKeysQuery, Result<TenantPublicKeys>>
{
    public Task<Result<TenantPublicKeys>> Handle(GetPublicKeysQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(directory.Tenants.TryGetValue(request.Slug, out var t)
            ? Result.Ok(new TenantPublicKeys(t.TenantId, t.Slug, [FakeDirectory.PublicJwk(t)]))
            : Result.Fail<TenantPublicKeys>(Fail.NotFound));
}

public sealed class GetActiveForSigningHandler(FakeDirectory directory, IDataProtectionProvider dataProtection)
    : IRequestHandler<GetActiveForSigningQuery, Result<TenancyActiveSigningKey>>
{
    public Task<Result<TenancyActiveSigningKey>> Handle(GetActiveForSigningQuery request, CancellationToken cancellationToken)
    {
        var tenant = directory.Tenants.Values.Single(t => t.TenantId == request.TenantId);
        var encrypted = dataProtection.CreateProtector(SigningKeyProtectionPurposes.PrivateKey)
            .Protect(tenant.Key.ExportPkcs8PrivateKey());
        return Task.FromResult(Result.Ok(new TenancyActiveSigningKey(tenant.KeyId, "ES256", encrypted, tenant.Slug)));
    }
}

public sealed class GetForAuthenticationHandler(FakeDirectory directory)
    : IRequestHandler<GetForAuthenticationQuery, Result<ClientForAuthentication>>
{
    public Task<Result<ClientForAuthentication>> Handle(GetForAuthenticationQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(directory.FindClient(request.TenantId, request.ClientId) is { } c
            ? Result.Ok(new ClientForAuthentication(c.Id, c.ClientId, c.Kind, c.Enabled, c.Grants, c.Auth,
                c.AccessTokenLifetimeSeconds, c.RefreshTokenLifetimeSeconds, c.RedirectUris, c.RequirePkce,
                c.PostLogoutRedirectUris, c.BackchannelLogoutUri, c.IdTokenLifetimeSeconds))
            : Result.Fail<ClientForAuthentication>(Fail.NotFound));
}

public sealed class GetReferenceHandler(FakeDirectory directory)
    : IRequestHandler<GetReferenceQuery, Result<ClientReference>>
{
    public Task<Result<ClientReference>> Handle(GetReferenceQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(directory.Clients.FirstOrDefault(c => c.TenantId == request.TenantId && c.Id == request.Id) is { } c
            ? Result.Ok(new ClientReference(c.Id, c.ClientId))
            : Result.Fail<ClientReference>(Fail.NotFound));
}

public sealed class ListCorsOriginsHandler(FakeDirectory directory)
    : IRequestHandler<ListCorsOriginsQuery, Result<IReadOnlyList<string>>>
{
    public Task<Result<IReadOnlyList<string>>> Handle(ListCorsOriginsQuery request, CancellationToken cancellationToken)
    {
        directory.CorsOriginLookups++;
        IReadOnlyList<string> origins = directory.Clients
            .Where(c => c.TenantId == request.TenantId && c.Enabled)
            .SelectMany(c => c.CorsOrigins)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        return Task.FromResult(Result.Ok(origins));
    }
}

public sealed class VerifySecretHandler(FakeDirectory directory) : IRequestHandler<VerifySecretQuery, Result>
{
    public Task<Result> Handle(VerifySecretQuery request, CancellationToken cancellationToken)
    {
        var client = directory.FindClient(request.TenantId, request.ClientId);
        var ok = client is { Enabled: true, Kind: ClientKind.Confidential, Auth: ClientAuthMethod.SharedSecret }
                 && request.PresentedSecret.Length > 0
                 && client.Secrets.Contains(request.PresentedSecret);
        return Task.FromResult(ok ? Result.Ok() : Result.Fail(new Error("Invalid client credentials.")));
    }
}

public sealed class AuthenticateHandler(FakeDirectory directory)
    : IRequestHandler<AuthenticateCommand, Result<AuthenticatedUser>>
{
    public Task<Result<AuthenticatedUser>> Handle(AuthenticateCommand request, CancellationToken cancellationToken)
    {
        var user = directory.Users.FirstOrDefault(u =>
            u.TenantId == request.TenantId
            && (u.Username == request.EmailOrUsername || u.Email == request.EmailOrUsername));
        // Tagged like Identity's handler, so Protocol can audit why.
        FailureReason? failure = user switch
        {
            null => FailureReason.UnknownUser,
            { Status: UserStatus.Locked } => FailureReason.Locked,
            { Status: UserStatus.Disabled } => FailureReason.Disabled,
            { Status: UserStatus.SoftDeleted } => FailureReason.Deleted,
            _ when request.Password != FakeDirectory.UserPassword => FailureReason.InvalidPassword,
            _ => null
        };
        return Task.FromResult(failure is null
            ? Result.Ok(new AuthenticatedUser(
                user!.UserId, user.Email, user.Username, user.Status, user.PendingActions.ToArray(), user.HasFactor))
            : Result.Fail<AuthenticatedUser>(
                AuthenticationFailure.Tag(new Error("Invalid credentials."), failure.Value, user?.UserId)));
    }
}

public sealed class GetForSignInHandler(FakeDirectory directory) : IRequestHandler<GetForSignInQuery, Result<SignInUser>>
{
    public Task<Result<SignInUser>> Handle(GetForSignInQuery request, CancellationToken cancellationToken)
    {
        var user = directory.Users.FirstOrDefault(u => u.TenantId == request.TenantId && u.UserId == request.UserId);
        return Task.FromResult(user is not null && user.Status != UserStatus.SoftDeleted
            ? Result.Ok(new SignInUser(
                user.UserId, user.Email, user.Username, user.Status, user.PendingActions.ToArray(), user.FirstName, user.LastName))
            : Result.Fail<SignInUser>(Fail.NotFound));
    }
}

public sealed class RecordAuditEventHandler(FakeAuditLog log) : IRequestHandler<RecordAuditEventCommand, Result>
{
    public Task<Result> Handle(RecordAuditEventCommand request, CancellationToken cancellationToken)
    {
        log.Add(request);
        return Task.FromResult(Result.Ok());
    }
}

public sealed class BeginTotpEnrollmentHandler(FakeDirectory directory, FakeAuditActorScope actorScope)
    : IRequestHandler<BeginTotpEnrollmentCommand, Result<TotpEnrollment>>
{
    public Task<Result<TotpEnrollment>> Handle(BeginTotpEnrollmentCommand request, CancellationToken cancellationToken)
    {
        actorScope.Observe("BeginTotpEnrollment");
        var user = directory.Users.Single(u => u.TenantId == request.TenantId && u.UserId == request.UserId);
        user.BeginEnrollmentCalls++;
        user.LastEnrollmentIssuer = request.Issuer;
        user.UnverifiedFactorId = Guid.NewGuid();
        var uri = $"otpauth://totp/{request.Issuer}:{user.Username}?secret={FakeDirectory.TotpSecret}&issuer={request.Issuer}";
        return Task.FromResult(Result.Ok(new TotpEnrollment(user.UnverifiedFactorId.Value, FakeDirectory.TotpSecret, uri)));
    }
}

public sealed class CompleteTotpEnrollmentHandler(FakeDirectory directory, FakeAuditActorScope actorScope)
    : IRequestHandler<CompleteTotpEnrollmentCommand, Result<CompletedTotpEnrollment>>
{
    public Task<Result<CompletedTotpEnrollment>> Handle(CompleteTotpEnrollmentCommand request, CancellationToken cancellationToken)
    {
        actorScope.Observe("CompleteTotpEnrollment");
        var user = directory.Users.Single(u => u.TenantId == request.TenantId && u.UserId == request.UserId);
        user.CompleteEnrollmentCalls++;
        if (user.UnverifiedFactorId != request.FactorId || request.Code != FakeDirectory.TotpCode)
            return Task.FromResult(Result.Fail<CompletedTotpEnrollment>("Invalid code."));

        var replaced = user.HasFactor;
        user.HasFactor = true;
        user.UnverifiedFactorId = null;
        user.PendingActions.Remove(RequiredAction.EnrollMfa);
        user.RecoveryCodes.Clear();
        user.RecoveryCodes.AddRange(Enumerable.Range(1, 10).Select(i => $"code{i:00}-{user.Username}"));
        return Task.FromResult(Result.Ok(new CompletedTotpEnrollment(user.RecoveryCodes.ToArray(), replaced)));
    }
}

public sealed class VerifySecondFactorHandler(FakeDirectory directory, FakeAuditActorScope actorScope)
    : IRequestHandler<VerifySecondFactorCommand, Result<SecondFactorVerified>>
{
    public Task<Result<SecondFactorVerified>> Handle(VerifySecondFactorCommand request, CancellationToken cancellationToken)
    {
        actorScope.Observe("VerifySecondFactor");
        var user = directory.Users.Single(u => u.TenantId == request.TenantId && u.UserId == request.UserId);
        if (user.Status != UserStatus.Active) return Fail(SecondFactorFailureReason.AccountInactive);
        if (!user.HasFactor) return Fail(SecondFactorFailureReason.NotEnrolled);
        if (request.Code == FakeDirectory.TotpCode)
            return Task.FromResult(Result.Ok(new SecondFactorVerified(SecondFactorMethod.Totp, user.RecoveryCodes.Count)));
        if (user.RecoveryCodes.Remove(request.Code))
            return Task.FromResult(Result.Ok(new SecondFactorVerified(SecondFactorMethod.RecoveryCode, user.RecoveryCodes.Count)));
        return Fail(SecondFactorFailureReason.InvalidCode);
    }

    private static Task<Result<SecondFactorVerified>> Fail(SecondFactorFailureReason reason) =>
        Task.FromResult(Result.Fail<SecondFactorVerified>(SecondFactorFailure.Tag(new Error("Invalid code."), reason)));
}

/// <summary>The code check shared by the account step-ups, tagged like Identity's (TOTP, or a recovery code if allowed).</summary>
internal static class FakeStepUp
{
    public static Error? Check(FakeDirectory.UserEntry user, string code, bool allowRecoveryCode)
    {
        if (!user.HasFactor) return SecondFactorFailure.Tag(new Error("Invalid code."), SecondFactorFailureReason.NotEnrolled);
        if (code == FakeDirectory.TotpCode) return null;
        if (allowRecoveryCode && user.RecoveryCodes.Remove(code)) return null;
        return SecondFactorFailure.Tag(new Error("Invalid code."), SecondFactorFailureReason.InvalidCode);
    }

    public static FakeDirectory.UserEntry User(FakeDirectory directory, Guid tenantId, Guid userId) =>
        directory.Users.Single(u => u.TenantId == tenantId && u.UserId == userId);
}

public sealed class AcceptTermsHandler(FakeDirectory directory, TimeProvider timeProvider, FakeAuditActorScope actorScope)
    : IRequestHandler<AcceptTermsCommand, Result>
{
    public Task<Result> Handle(AcceptTermsCommand request, CancellationToken cancellationToken)
    {
        actorScope.Observe("AcceptTerms");
        var user = FakeStepUp.User(directory, request.TenantId, request.UserId);
        user.AcceptTermsCalls++;
        user.PendingActions.Remove(RequiredAction.AcceptTos);
        user.TermsAcceptedAt = timeProvider.GetUtcNow();
        return Task.FromResult(Result.Ok());
    }
}

public sealed class GetAccountHandler(FakeDirectory directory) : IRequestHandler<GetAccountQuery, Result<AccountView>>
{
    public Task<Result<AccountView>> Handle(GetAccountQuery request, CancellationToken cancellationToken)
    {
        var user = directory.Users.FirstOrDefault(u => u.TenantId == request.TenantId && u.UserId == request.UserId);
        if (user is null) return Task.FromResult(Result.Fail<AccountView>(Fail.NotFound));

        IReadOnlyList<AccountFactor> factors = user.HasFactor
            ? [new AccountFactor(Guid.NewGuid(), AccountFactorType.Totp, FakeDirectory.FactorEnrolledAt, null)]
            : [];
        return Task.FromResult(Result.Ok(new AccountView(
            user.Email, user.Username, user.TermsAcceptedAt, directory.TermsUrl, directory.MfaAvailable,
            directory.MfaRequired, factors, user.RecoveryCodes.Count)));
    }
}

public sealed class ChangeOwnPasswordHandler(FakeDirectory directory, FakeAuditActorScope actorScope)
    : IRequestHandler<ChangeOwnPasswordCommand, Result>
{
    public const string WrongPassword = "Your current password is incorrect.";
    public const string TooShort = "The new password must be at least 12 characters long.";

    public Task<Result> Handle(ChangeOwnPasswordCommand request, CancellationToken cancellationToken)
    {
        actorScope.Observe("ChangeOwnPassword");
        var user = FakeStepUp.User(directory, request.TenantId, request.UserId);
        if (request.CurrentPassword != FakeDirectory.UserPassword)
            return Task.FromResult(Result.Fail(
                AuthenticationFailure.Tag(new Error(WrongPassword), FailureReason.InvalidPassword, user.UserId)));
        if (request.NewPassword.Length < 12) return Task.FromResult(Result.Fail(TooShort));

        user.PasswordChanged = true;
        return Task.FromResult(Result.Ok());
    }
}

public sealed class RegenerateRecoveryCodesHandler(FakeDirectory directory, FakeAuditActorScope actorScope)
    : IRequestHandler<RegenerateRecoveryCodesCommand, Result<IReadOnlyList<string>>>
{
    public Task<Result<IReadOnlyList<string>>> Handle(RegenerateRecoveryCodesCommand request, CancellationToken cancellationToken)
    {
        actorScope.Observe("RegenerateRecoveryCodes");
        var user = FakeStepUp.User(directory, request.TenantId, request.UserId);
        if (FakeStepUp.Check(user, request.Code, allowRecoveryCode: false) is { } error)
            return Task.FromResult(Result.Fail<IReadOnlyList<string>>(error));

        user.RecoveryCodes.Clear();
        user.RecoveryCodes.AddRange(Enumerable.Range(1, 10).Select(i => $"new{i:00}-{user.Username}"));
        return Task.FromResult(Result.Ok<IReadOnlyList<string>>(user.RecoveryCodes.ToArray()));
    }
}

public sealed class BeginTotpReplacementHandler(FakeDirectory directory, FakeAuditActorScope actorScope)
    : IRequestHandler<BeginTotpReplacementCommand, Result<TotpEnrollment>>
{
    public Task<Result<TotpEnrollment>> Handle(BeginTotpReplacementCommand request, CancellationToken cancellationToken)
    {
        actorScope.Observe("BeginTotpReplacement");
        var user = FakeStepUp.User(directory, request.TenantId, request.UserId);
        if (FakeStepUp.Check(user, request.Code, allowRecoveryCode: true) is { } error)
            return Task.FromResult(Result.Fail<TotpEnrollment>(error));

        user.BeginEnrollmentCalls++;
        user.LastEnrollmentIssuer = request.Issuer;
        user.UnverifiedFactorId = Guid.NewGuid();
        var uri = $"otpauth://totp/{request.Issuer}:{user.Username}?secret={FakeDirectory.TotpSecret}&issuer={request.Issuer}";
        return Task.FromResult(Result.Ok(new TotpEnrollment(user.UnverifiedFactorId.Value, FakeDirectory.TotpSecret, uri)));
    }
}

public sealed class RemoveMfaHandler(FakeDirectory directory, FakeAuditActorScope actorScope) : IRequestHandler<RemoveMfaCommand, Result>
{
    public Task<Result> Handle(RemoveMfaCommand request, CancellationToken cancellationToken)
    {
        actorScope.Observe("RemoveMfa");
        var user = FakeStepUp.User(directory, request.TenantId, request.UserId);
        if (directory.MfaRequired) return Task.FromResult(Result.Fail("The tenant requires MFA."));
        if (FakeStepUp.Check(user, request.Code, allowRecoveryCode: true) is { } error) return Task.FromResult(Result.Fail(error));

        user.HasFactor = false;
        user.RecoveryCodes.Clear();
        return Task.FromResult(Result.Ok());
    }
}
