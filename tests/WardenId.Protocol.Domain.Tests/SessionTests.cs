namespace WardenId.Protocol.Domain.Tests;

public class SessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SubjectId = Guid.NewGuid();

    private static Session Live() => Session.Start(TenantId, SubjectId, Now, Now.AddDays(1)).Value;

    private static string ErrorCode(FluentResults.ResultBase result) =>
        (string)result.Errors[0].Metadata[ProtocolErrorCodes.MetadataKey];

    [Fact]
    public void Start_SetsFields_AndRaisesStartedEvent()
    {
        var result = Session.Start(TenantId, SubjectId, Now, Now.AddDays(1));

        Assert.True(result.IsSuccess);
        var session = result.Value;
        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.Equal(TenantId, session.TenantId);
        Assert.Equal(SubjectId, session.SubjectId);
        Assert.Equal(Now, session.AuthTime);
        Assert.Equal(Now.AddDays(1), session.ExpiresAt);
        Assert.Null(session.EndedAt);
        Assert.Null(session.EndReason);
        Assert.Empty(session.Authorizations);

        var started = Assert.IsType<SessionStartedDomainEvent>(Assert.Single(session.DomainEvents));
        Assert.Equal(session.Id, started.Sid);
        Assert.Equal(TenantId, started.TenantId);
        Assert.Equal(SubjectId, started.SubjectId);
    }

    [Fact]
    public void Start_ExpiresAtEqualToAuthTime_FailsInvalidLifetime()
    {
        var result = Session.Start(TenantId, SubjectId, Now, Now);

        Assert.True(result.IsFailed);
        Assert.Equal(ProtocolErrorCodes.Validation, ErrorCode(result));
    }

    [Fact]
    public void Start_ExpiresAtBeforeAuthTime_FailsInvalidLifetime()
    {
        var result = Session.Start(TenantId, SubjectId, Now, Now.AddSeconds(-1));

        Assert.True(result.IsFailed);
        Assert.Equal(ProtocolErrorCodes.Validation, ErrorCode(result));
    }

    [Fact]
    public void IsLive_BeforeExpiry_IsTrue() => Assert.True(Live().IsLive(Now.AddDays(1).AddTicks(-1)));

    [Fact]
    public void IsLive_AtExpiry_IsFalse() => Assert.False(Live().IsLive(Now.AddDays(1)));

    [Fact]
    public void IsLive_AfterEnd_IsFalse()
    {
        var session = Live();
        session.End(EndReason.UserLogout, Now.AddMinutes(5));

        Assert.False(session.IsLive(Now.AddMinutes(6)));
    }

    [Fact]
    public void RecordAuthorization_AddsAuthorization()
    {
        var session = Live();

        var result = session.RecordAuthorization("auth-1", "web", offlineAccess: true, Now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        var authorization = Assert.Single(session.Authorizations);
        Assert.Equal("auth-1", authorization.AuthorizationId);
        Assert.Equal("web", authorization.ClientId);
        Assert.True(authorization.OfflineAccess);
        Assert.Equal(Now.AddMinutes(1), authorization.CreatedAt);
    }

    [Fact]
    public void RecordAuthorization_SameAuthorizationIdTwice_IsIdempotent()
    {
        var session = Live();

        session.RecordAuthorization("auth-1", "web", offlineAccess: false, Now.AddMinutes(1));
        var second = session.RecordAuthorization("auth-1", "web", offlineAccess: false, Now.AddMinutes(2));

        Assert.True(second.IsSuccess);
        Assert.Single(session.Authorizations);
    }

    [Fact]
    public void RecordAuthorization_EndedSession_FailsEnded()
    {
        var session = Live();
        session.End(EndReason.UserLogout, Now.AddMinutes(1));

        var result = session.RecordAuthorization("auth-1", "web", offlineAccess: false, Now.AddMinutes(2));

        Assert.True(result.IsFailed);
        Assert.Equal(ProtocolErrorCodes.SessionExpired, ErrorCode(result));
        Assert.Empty(session.Authorizations);
    }

    [Fact]
    public void RecordAuthorization_ExpiredSession_FailsEnded()
    {
        var session = Live();

        var result = session.RecordAuthorization("auth-1", "web", offlineAccess: false, Now.AddDays(1));

        Assert.True(result.IsFailed);
        Assert.Equal(ProtocolErrorCodes.SessionExpired, ErrorCode(result));
    }

    [Fact]
    public void End_SetsFields_AndRaisesEndedEventWithAuthorizationSnapshots()
    {
        var session = Live();
        session.RecordAuthorization("auth-1", "web", offlineAccess: false, Now.AddMinutes(1));
        session.RecordAuthorization("auth-2", "mobile", offlineAccess: true, Now.AddMinutes(2));
        session.ClearDomainEvents();

        var result = session.End(EndReason.PasswordChanged, Now.AddMinutes(10));

        Assert.True(result.IsSuccess);
        Assert.Equal(Now.AddMinutes(10), session.EndedAt);
        Assert.Equal(EndReason.PasswordChanged, session.EndReason);

        var ended = Assert.IsType<SessionEndedDomainEvent>(Assert.Single(session.DomainEvents));
        Assert.Equal(session.Id, ended.Sid);
        Assert.Equal(TenantId, ended.TenantId);
        Assert.Equal(SubjectId, ended.SubjectId);
        Assert.Equal(EndReason.PasswordChanged, ended.Reason);
        Assert.Equal(
            new[] { new SessionAuthorizationSnapshot("auth-1", "web", false), new SessionAuthorizationSnapshot("auth-2", "mobile", true) },
            ended.Authorizations);
    }

    [Fact]
    public void End_ExpiredSession_Succeeds()
    {
        var session = Live();

        Assert.True(session.End(EndReason.AdminEnded, Now.AddDays(2)).IsSuccess);
        Assert.Equal(EndReason.AdminEnded, session.EndReason);
    }

    [Fact]
    public void End_Twice_FailsAlreadyEnded_AndKeepsFirstReason()
    {
        var session = Live();
        session.End(EndReason.UserLogout, Now.AddMinutes(1));
        session.ClearDomainEvents();

        var result = session.End(EndReason.AdminEnded, Now.AddMinutes(2));

        Assert.True(result.IsFailed);
        Assert.Equal(ProtocolErrorCodes.Validation, ErrorCode(result));
        Assert.Equal(EndReason.UserLogout, session.EndReason);
        Assert.Equal(Now.AddMinutes(1), session.EndedAt);
        Assert.Empty(session.DomainEvents);
    }
}
