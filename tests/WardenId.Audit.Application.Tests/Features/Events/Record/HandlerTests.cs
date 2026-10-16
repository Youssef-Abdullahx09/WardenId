using Microsoft.Extensions.Logging.Abstractions;
using WardenId.Audit.Application.Features.Events.Record;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using Command = WardenId.Audit.Integration.Command.Features.Events.Record.Command;
using ContractActor = WardenId.Audit.Integration.Command.Features.Events.Record.Actor;
using DomainActor = WardenId.Audit.Domain.Actor;

namespace WardenId.Audit.Application.Tests.Features.Events.Record;

public class HandlerTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Sid = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-16T08:00:00Z");

    private static (Handler Handler, FakeAuditRecorder Recorder, FakeAuditContextAccessor Ambient) Create()
    {
        var recorder = new FakeAuditRecorder();
        var ambient = new FakeAuditContextAccessor();
        return (new Handler(recorder, ambient, NullLogger<Handler>.Instance), recorder, ambient);
    }

    private static async Task<(AuditEvent Event, Guid? TenantId)> RecordSingleAsync(Command command)
    {
        var (handler, recorder, _) = Create();

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return Assert.Single(recorder.Recorded);
    }

    private static string[] TargetsOf(AuditEvent e) => e.Targets.Select(t => $"{t.Type.Key}:{t.Id}").ToArray();

    [Fact]
    public async Task SignInFailed_UnknownUser_IsAnonymousInteractiveFailure()
    {
        var (recorded, tenantId) = await RecordSingleAsync(
            new Command(SecurityEvent.SignInFailed, TenantId, Now, ContractActor.Anonymous, [], OutcomeReason.UnknownUser));

        Assert.Equal(TenantId, tenantId);
        Assert.Equal(AuditChannel.AuthnInteractive, recorded.Channel);
        Assert.Same(AuditEventType.UserLoginFailed, recorded.EventType);
        Assert.Equal(AuditSeverity.Warning, recorded.Severity);
        Assert.Equal(Now, recorded.OccurredAt);
        Assert.Equal(ActorType.Anonymous, recorded.Actor.Type);
        Assert.Equal(TenantId, recorded.Actor.Scope.TenantId);
        Assert.Equal(AuditResult.Failure, recorded.Outcome.Status);
        Assert.Same(AuditOutcomeReason.UnknownUsername, recorded.Outcome.Reason);
        Assert.Equal("oidc", recorded.AuthenticationContext.Protocol);
        Assert.Empty(recorded.Targets);
    }

    [Theory]
    [InlineData(OutcomeReason.InvalidPassword, "invalid_password")]
    [InlineData(OutcomeReason.AccountLockedOut, "account_locked_out")]
    [InlineData(OutcomeReason.AccountLocked, "account_locked")]
    [InlineData(OutcomeReason.AccountDisabled, "account_disabled")]
    [InlineData(OutcomeReason.AccountDeleted, "account_deleted")]
    [InlineData(OutcomeReason.InvalidCredentials, "invalid_credentials")]
    [InlineData(OutcomeReason.EmailNotVerified, "email_not_verified")]
    [InlineData(OutcomeReason.PasswordResetRequired, "password_reset_required")]
    [InlineData(OutcomeReason.TermsNotAccepted, "terms_not_accepted")]
    [InlineData(OutcomeReason.MfaEnrollmentRequired, "mfa_enrollment_required")]
    [InlineData(OutcomeReason.PendingVerification, "pending_verification")]
    public async Task SignInFailed_KnownUser_CarriesTheReason_AndTheUserAsActorAndTarget(OutcomeReason reason, string key)
    {
        var (recorded, _) = await RecordSingleAsync(new Command(
            SecurityEvent.SignInFailed, TenantId, Now, ContractActor.User(UserId), [Target.User(UserId)], reason));

        Assert.Equal(key, recorded.Outcome.Reason!.Key);
        Assert.Equal(ActorType.EndUser, recorded.Actor.Type);
        Assert.Equal(UserId.ToString(), recorded.Actor.Id);
        Assert.Equal([$"user:{UserId}"], TargetsOf(recorded));
    }

    [Fact]
    public async Task SignInFailed_WithoutReason_IsRejected_AndNothingIsRecorded()
    {
        var (handler, recorder, _) = Create();

        var result = await handler.Handle(
            new Command(SecurityEvent.SignInFailed, TenantId, Now, ContractActor.Anonymous, []), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Empty(recorder.Recorded);
    }

    [Fact]
    public async Task SessionStarted_IsInteractiveSuccess_WithTheAuthenticationContext()
    {
        var (recorded, _) = await RecordSingleAsync(new Command(
            SecurityEvent.SessionStarted, TenantId, Now, ContractActor.User(UserId),
            [Target.User(UserId), Target.Session(Sid)],
            Session: new SessionContext(Sid, Now, ["pwd"])));

        Assert.Same(AuditEventType.UserSessionStart, recorded.EventType);
        Assert.Equal(AuditChannel.AuthnInteractive, recorded.Channel);
        Assert.Equal(AuditSeverity.Info, recorded.Severity);
        Assert.Equal(AuditResult.Success, recorded.Outcome.Status);
        Assert.Null(recorded.Outcome.Reason);
        Assert.Equal(Sid.ToString(), recorded.AuthenticationContext.Sid);
        Assert.Equal(Now, recorded.AuthenticationContext.AuthTime);
        Assert.Equal(["pwd"], recorded.AuthenticationContext.Amr);
        Assert.Equal([$"user:{UserId}", $"session:{Sid}"], TargetsOf(recorded));
    }

    [Fact]
    public async Task SignedOut_IsInteractive_UserLogout()
    {
        var (recorded, _) = await RecordSingleAsync(new Command(
            SecurityEvent.SignedOut, TenantId, Now, ContractActor.User(UserId),
            [Target.User(UserId), Target.Session(Sid), Target.Client("web")], Session: new SessionContext(Sid)));

        Assert.Same(AuditEventType.UserLogout, recorded.EventType);
        Assert.Equal(AuditChannel.AuthnInteractive, recorded.Channel);
        Assert.Equal([$"user:{UserId}", $"session:{Sid}", "oauth_client:web"], TargetsOf(recorded));
    }

    [Theory]
    [InlineData(OutcomeReason.UserLogout, "user_logout")]
    [InlineData(OutcomeReason.PasswordChanged, "password_changed")]
    [InlineData(OutcomeReason.UserDisabled, "user_disabled")]
    [InlineData(OutcomeReason.UserDeleted, "user_deleted")]
    [InlineData(OutcomeReason.AdminEnded, "admin_ended")]
    [InlineData(OutcomeReason.AccountLocked, "account_locked")]
    public async Task SessionEnded_IsAnInteractiveSuccess_CarryingTheEndReason_ByAPlatformSystemActor(OutcomeReason reason, string key)
    {
        var (recorded, tenantId) = await RecordSingleAsync(new Command(
            SecurityEvent.SessionEnded, TenantId, Now, ContractActor.System("protocol"),
            [Target.User(UserId), Target.Session(Sid)], reason, new SessionContext(Sid)));

        Assert.Equal(TenantId, tenantId);
        Assert.Same(AuditEventType.UserSessionEnd, recorded.EventType);
        Assert.Equal(AuditChannel.AuthnInteractive, recorded.Channel);
        Assert.Equal(AuditResult.Success, recorded.Outcome.Status);
        Assert.Equal(key, recorded.Outcome.Reason!.Key);
        Assert.Equal(ActorType.System, recorded.Actor.Type);
        Assert.True(recorded.Actor.Scope.IsPlatform);
    }

    [Theory]
    [InlineData(SecurityEvent.TokenIssuedAuthorizationCode, "token.issue.authorizationcode", AuditChannel.AuthnSilent)]
    [InlineData(SecurityEvent.TokenIssuedRefreshToken, "token.issue.refreshtoken", AuditChannel.AuthnSilent)]
    [InlineData(SecurityEvent.TokenIssuedClientCredentials, "token.issue.clientcredentials", AuditChannel.AuthnMachine)]
    public async Task TokenIssued_IsRoutedByGrantType(SecurityEvent securityEvent, string key, AuditChannel channel)
    {
        var (recorded, _) = await RecordSingleAsync(new Command(
            securityEvent, TenantId, Now, ContractActor.Client("web"), [Target.Client("web")],
            Session: new SessionContext(TokenId: "jti-1")));

        Assert.Equal(key, recorded.EventType.Key);
        Assert.Equal(channel, recorded.Channel);
        Assert.Equal(AuditResult.Success, recorded.Outcome.Status);
        Assert.Equal(ActorType.OAuthClient, recorded.Actor.Type);
        Assert.Equal("web", recorded.Actor.Id);
        Assert.Equal("jti-1", recorded.AuthenticationContext.Jti);
        Assert.Equal("oauth2", recorded.AuthenticationContext.Protocol);
    }

    [Fact]
    public async Task TokenRevoked_OfAUserToken_IsSilent()
    {
        var (recorded, _) = await RecordSingleAsync(new Command(
            SecurityEvent.TokenRevoked, TenantId, Now, ContractActor.Client("web"),
            [Target.Client("web"), Target.User(UserId), Target.TokenFamily("auth-1")]));

        Assert.Same(AuditEventType.TokenRevoke, recorded.EventType);
        Assert.Equal(AuditChannel.AuthnSilent, recorded.Channel);
        Assert.Contains("token_family:auth-1", TargetsOf(recorded));
    }

    [Fact]
    public async Task TokenRevoked_OfAClientToken_IsMachine()
    {
        var (recorded, _) = await RecordSingleAsync(new Command(
            SecurityEvent.TokenRevoked, TenantId, Now, ContractActor.Client("svc"), [Target.Client("svc")]));

        Assert.Equal(AuditChannel.AuthnMachine, recorded.Channel);
    }

    [Fact]
    public async Task RefreshTokenReplayed_IsACriticalSilentFailure()
    {
        var (recorded, _) = await RecordSingleAsync(new Command(
            SecurityEvent.RefreshTokenReplayed, TenantId, Now, ContractActor.Client("web"),
            [Target.Client("web"), Target.User(UserId), Target.TokenFamily("auth-1")],
            Session: new SessionContext(TokenId: "tok-1")));

        Assert.Same(AuditEventType.TokenRefreshReplay, recorded.EventType);
        Assert.Equal(AuditChannel.AuthnSilent, recorded.Channel);
        Assert.Equal(AuditSeverity.Critical, recorded.Severity);
        Assert.Equal(AuditResult.Failure, recorded.Outcome.Status);
        Assert.Same(AuditOutcomeReason.InvalidGrant, recorded.Outcome.Reason);
    }

    [Fact]
    public async Task ClientAndCorrelation_ComeFromTheAmbientContext()
    {
        var (handler, recorder, ambient) = Create();
        var client = ClientContext.Create("203.0.113.7", "Mozilla/5.0").Value;
        var correlation = Correlation.Create("req-1", "trace-1");
        ambient.Current = new AuditAmbientContext(DomainActor.Anonymous(ActorScope.Platform()), null, correlation, client);

        await handler.Handle(
            new Command(SecurityEvent.SignInFailed, TenantId, Now, ContractActor.Anonymous, [], OutcomeReason.UnknownUser),
            CancellationToken.None);

        var (recorded, _) = Assert.Single(recorder.Recorded);
        Assert.Equal(client, recorded.Client);
        Assert.Equal(correlation, recorded.Correlation);
    }

    [Fact]
    public async Task WithoutAmbientContext_ClientAndCorrelationAreUnknown()
    {
        var (recorded, _) = await RecordSingleAsync(
            new Command(SecurityEvent.SignInFailed, TenantId, Now, ContractActor.Anonymous, [], OutcomeReason.UnknownUser));

        Assert.Equal(ClientContext.Unknown(), recorded.Client);
        Assert.Equal(Correlation.None(), recorded.Correlation);
    }

    [Fact]
    public async Task RecorderFailure_NeverThrows()
    {
        var (handler, recorder, _) = Create();
        recorder.Throws = new InvalidOperationException("db down");

        var result = await handler.Handle(
            new Command(SecurityEvent.SessionStarted, TenantId, Now, ContractActor.User(UserId), [Target.User(UserId)]),
            CancellationToken.None);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task ActorWithoutId_IsRejected_WithoutThrowing()
    {
        var (handler, recorder, _) = Create();

        var result = await handler.Handle(
            new Command(SecurityEvent.SessionStarted, TenantId, Now, new ContractActor(ActorKind.EndUser, null), []),
            CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Empty(recorder.Recorded);
    }

    public static IEnumerable<object[]> AllSecurityEvents() => Enum.GetValues<SecurityEvent>().Select(e => new object[] { e });

    [Theory]
    [MemberData(nameof(AllSecurityEvents))]
    public async Task EverySecurityEvent_IsRoutable(SecurityEvent securityEvent)
    {
        await RecordSingleAsync(new Command(
            securityEvent, TenantId, Now, ContractActor.Client("web"), [Target.Client("web")], OutcomeReason.InvalidGrant));
    }

    public static IEnumerable<object[]> AllOutcomeReasons() => Enum.GetValues<OutcomeReason>().Select(r => new object[] { r });

    [Theory]
    [MemberData(nameof(AllOutcomeReasons))]
    public async Task EveryOutcomeReason_MapsToACatalogEntry(OutcomeReason reason)
    {
        await RecordSingleAsync(new Command(SecurityEvent.SignInFailed, TenantId, Now, ContractActor.Anonymous, [], reason));
    }
}
