using FluentResults;
using MediatR;
using Microsoft.Extensions.Logging;
using WardenId.Audit.Domain;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using Command = WardenId.Audit.Integration.Command.Features.Events.Record.Command;
using ContractActor = WardenId.Audit.Integration.Command.Features.Events.Record.Actor;
using DomainActor = WardenId.Audit.Domain.Actor;

namespace WardenId.Audit.Application.Features.Events.Record;

/// <summary>
/// Turns another module's authentication/token event into an envelope and appends it through
/// <see cref="IAuditRecorder"/> (direct append, spec §5). Routing lives here, not at the call sites: each
/// <see cref="SecurityEvent"/> has exactly one event type, channel, severity and outcome. Fail-open (I10):
/// nothing here throws — an envelope that can't be built or written is logged and reported as a failed Result.
/// </summary>
/// <remarks>
/// Channels: anything the user does in the browser (sign-in, sign-out, their own account changes and the
/// step-ups in front of them) and the session lifecycle go to authn.interactive; token issuance and revocation for a user's grant go to authn.silent; client_credentials
/// and revocation of a client-only token go to authn.machine. token.ops gets nothing yet: whether it is
/// platform-only or per-tenant is still open (docs/audit/OPEN-DECISIONS.md #3), and it is default-off.
/// </remarks>
public sealed class Handler(IAuditRecorder recorder, IAuditContextAccessor ambient, ILogger<Handler> logger)
    : IRequestHandler<Command, Result>
{
    private const int SchemaVersion = 1;
    private const string Oidc = "oidc";
    private const string OAuth2 = "oauth2";

    private sealed record Route(
        AuditEventType EventType, AuditChannel Channel, AuditSeverity Severity, bool IsFailure, string Protocol,
        OutcomeReason? DefaultReason = null);

    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        try
        {
            var built = Build(request);
            if (built.IsFailed)
            {
                logger.LogWarning(
                    "Audit event {Event} for tenant {TenantId} was not recorded: {Errors}",
                    request.Event, request.TenantId, string.Join("; ", built.Errors.Select(e => e.Message)));
                return built.ToResult();
            }

            await recorder.RecordAsync(built.Value, request.TenantId, cancellationToken);
            return Result.Ok();
        }
        catch (Exception ex)
        {
            // IAuditRecorder already fails open; this also covers a recorder that doesn't, and envelope bugs.
            logger.LogError(ex, "Audit event {Event} for tenant {TenantId} was not recorded", request.Event, request.TenantId);
            return Result.Fail(new Error("The audit event could not be recorded.").CausedBy(ex));
        }
    }

    private Result<AuditEvent> Build(Command request)
    {
        var route = RouteOf(request);
        var reasonCode = request.Reason ?? route.DefaultReason;
        if (route.IsFailure && reasonCode is null)
            return Result.Fail(new Error($"{request.Event} requires an outcome reason."));

        var actor = ToActor(request.Actor, request.TenantId);
        if (actor.IsFailed) return actor.ToResult();

        var targets = new List<AuditTarget>(request.Targets.Count);
        foreach (var target in request.Targets)
        {
            var built = AuditTarget.Create(ToTargetType(target.Kind), target.Id);
            if (built.IsFailed) return built.ToResult();
            targets.Add(built.Value);
        }

        var reason = reasonCode is { } code ? ToReason(code) : null;
        var outcome = route.IsFailure ? AuditOutcome.Failure(reason!) : AuditOutcome.Success(reason);

        var session = request.Session;
        var authContext = AuthContext.Create(
            sid: session?.Sid?.ToString(),
            jti: session?.TokenId,
            amr: session?.Amr,
            authTime: session?.AuthTime,
            protocol: route.Protocol);

        var context = ambient.Current;

        return AuditEvent.Create(
            Guid.CreateVersion7(),
            route.Channel,
            route.EventType,
            SchemaVersion,
            request.OccurredAt,
            route.Severity,
            actor.Value,
            outcome,
            context?.Client ?? ClientContext.Unknown(),
            authContext,
            context?.Correlation ?? Correlation.None(),
            targets);
    }

    private static Route RouteOf(Command request) => request.Event switch
    {
        SecurityEvent.SignInFailed =>
            new(AuditEventType.UserLoginFailed, AuditChannel.AuthnInteractive, AuditSeverity.Warning, true, Oidc),
        SecurityEvent.SessionStarted =>
            new(AuditEventType.UserSessionStart, AuditChannel.AuthnInteractive, AuditSeverity.Info, false, Oidc),
        SecurityEvent.SignedOut =>
            new(AuditEventType.UserLogout, AuditChannel.AuthnInteractive, AuditSeverity.Info, false, Oidc),
        SecurityEvent.SessionEnded =>
            new(AuditEventType.UserSessionEnd, AuditChannel.AuthnInteractive, AuditSeverity.Info, false, Oidc),
        SecurityEvent.TokenIssuedAuthorizationCode =>
            new(AuditEventType.TokenIssueAuthorizationCode, AuditChannel.AuthnSilent, AuditSeverity.Info, false, OAuth2),
        SecurityEvent.TokenIssuedRefreshToken =>
            new(AuditEventType.TokenIssueRefreshToken, AuditChannel.AuthnSilent, AuditSeverity.Info, false, OAuth2),
        SecurityEvent.TokenIssuedClientCredentials =>
            new(AuditEventType.TokenIssueClientCredentials, AuditChannel.AuthnMachine, AuditSeverity.Info, false, OAuth2),
        SecurityEvent.TokenRevoked =>
            new(AuditEventType.TokenRevoke,
                request.Targets.Any(t => t.Kind == TargetKind.User) ? AuditChannel.AuthnSilent : AuditChannel.AuthnMachine,
                AuditSeverity.Info, false, OAuth2),
        SecurityEvent.RefreshTokenReplayed =>
            new(AuditEventType.TokenRefreshReplay, AuditChannel.AuthnSilent, AuditSeverity.Critical, true, OAuth2,
                OutcomeReason.InvalidGrant),
        SecurityEvent.TermsAccepted =>
            new(AuditEventType.UserTermsAccept, AuditChannel.AuthnInteractive, AuditSeverity.Info, false, Oidc),
        SecurityEvent.TotpEnrolled =>
            new(AuditEventType.UserMfaTotpEnroll, AuditChannel.AuthnInteractive, AuditSeverity.Info, false, Oidc),
        SecurityEvent.AuthenticatorReplaced =>
            new(AuditEventType.UserMfaTotpReplace, AuditChannel.AuthnInteractive, AuditSeverity.Info, false, Oidc),
        SecurityEvent.MfaRemoved =>
            new(AuditEventType.UserMfaRemove, AuditChannel.AuthnInteractive, AuditSeverity.Warning, false, Oidc),
        SecurityEvent.RecoveryCodesRegenerated =>
            new(AuditEventType.UserMfaRecoveryCodesRegenerate, AuditChannel.AuthnInteractive, AuditSeverity.Info, false, Oidc),
        SecurityEvent.PasswordChanged =>
            new(AuditEventType.UserPasswordChange, AuditChannel.AuthnInteractive, AuditSeverity.Info, false, Oidc),
        SecurityEvent.StepUpFailed =>
            new(AuditEventType.UserStepUpFailed, AuditChannel.AuthnInteractive, AuditSeverity.Warning, true, Oidc),
        _ => throw new ArgumentOutOfRangeException(nameof(request), request.Event, "Unknown security event.")
    };

    /// <summary>
    /// The actors of these events belong to the event's tenant, except a System actor (a Warden component), which
    /// is platform-scoped like the bootstrap seeder's.
    /// </summary>
    private static Result<DomainActor> ToActor(ContractActor actor, Guid tenantId)
    {
        var tenantScope = ActorScope.ForTenant(tenantId);
        return actor.Kind switch
        {
            ActorKind.Anonymous => Result.Ok(DomainActor.Anonymous(tenantScope)),
            ActorKind.EndUser => DomainActor.Create(ActorType.EndUser, actor.Id ?? "", tenantScope),
            ActorKind.OAuthClient => DomainActor.Create(ActorType.OAuthClient, actor.Id ?? "", tenantScope),
            ActorKind.System => DomainActor.Create(ActorType.System, actor.Id ?? "", ActorScope.Platform()),
            _ => throw new ArgumentOutOfRangeException(nameof(actor), actor.Kind, "Unknown actor kind.")
        };
    }

    private static AuditTargetType ToTargetType(TargetKind kind) => kind switch
    {
        TargetKind.User => AuditTargetType.User,
        TargetKind.Session => AuditTargetType.Session,
        TargetKind.OAuthClient => AuditTargetType.OAuthClient,
        TargetKind.TokenFamily => AuditTargetType.TokenFamily,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown target kind.")
    };

    private static AuditOutcomeReason ToReason(OutcomeReason reason) => reason switch
    {
        OutcomeReason.UnknownUser => AuditOutcomeReason.UnknownUsername,
        OutcomeReason.InvalidPassword => AuditOutcomeReason.InvalidPassword,
        OutcomeReason.AccountLockedOut => AuditOutcomeReason.AccountLockedOut,
        OutcomeReason.AccountLocked => AuditOutcomeReason.AccountLocked,
        OutcomeReason.AccountDisabled => AuditOutcomeReason.AccountDisabled,
        OutcomeReason.AccountDeleted => AuditOutcomeReason.AccountDeleted,
        OutcomeReason.InvalidCredentials => AuditOutcomeReason.InvalidCredentials,
        OutcomeReason.EmailNotVerified => AuditOutcomeReason.EmailNotVerified,
        OutcomeReason.PasswordResetRequired => AuditOutcomeReason.PasswordResetRequired,
        OutcomeReason.TermsNotAccepted => AuditOutcomeReason.TermsNotAccepted,
        OutcomeReason.MfaEnrollmentRequired => AuditOutcomeReason.MfaEnrollmentRequired,
        OutcomeReason.PendingVerification => AuditOutcomeReason.PendingVerification,
        OutcomeReason.UserLogout => AuditOutcomeReason.UserLogout,
        OutcomeReason.PasswordChanged => AuditOutcomeReason.PasswordChanged,
        OutcomeReason.UserDisabled => AuditOutcomeReason.UserDisabled,
        OutcomeReason.UserDeleted => AuditOutcomeReason.UserDeleted,
        OutcomeReason.AdminEnded => AuditOutcomeReason.AdminEnded,
        OutcomeReason.InvalidGrant => AuditOutcomeReason.InvalidGrant,
        OutcomeReason.InvalidSecondFactor => AuditOutcomeReason.InvalidSecondFactor,
        OutcomeReason.MfaChanged => AuditOutcomeReason.MfaChanged,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown outcome reason.")
    };
}
