using FluentResults;
using WardenId.Protocol.Domain.Events;
using WardenId.SharedKernel.Domain;

namespace WardenId.Protocol.Domain.Sessions;

/// <summary>
/// A per-tenant browser login session. <see cref="Entity.Id"/> is the OIDC <c>sid</c>. The tenant cookie
/// only references it, so ending this row ends the session for every request presenting the cookie.
/// </summary>
public sealed class Session : AggregateRoot
{
    private readonly List<SessionAuthorization> _authorizations = [];

    // EF binds this constructor by parameter name.
    private Session(Guid tenantId, Guid subjectId, DateTimeOffset authTime, DateTimeOffset expiresAt)
    {
        TenantId = tenantId;
        SubjectId = subjectId;
        AuthTime = authTime;
        ExpiresAt = expiresAt;
    }

    public Guid TenantId { get; private set; }
    public Guid SubjectId { get; private set; }
    public DateTimeOffset AuthTime { get; private set; }

    /// <summary>Absolute cap (<c>ProtocolSession:AbsoluteLifetime</c>); the cookie's sliding lifetime never extends it.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }
    public EndReason? EndReason { get; private set; }
    public IReadOnlyCollection<SessionAuthorization> Authorizations => _authorizations.AsReadOnly();

    public static Result<Session> Start(Guid tenantId, Guid subjectId, DateTimeOffset authTime, DateTimeOffset expiresAt)
    {
        if (expiresAt <= authTime) return Result.Fail(SessionErrors.InvalidLifetime());

        var session = new Session(tenantId, subjectId, authTime, expiresAt);
        session.RaiseDomainEvent(new SessionStartedDomainEvent(session.Id, tenantId, subjectId));
        return Result.Ok(session);
    }

    public bool IsLive(DateTimeOffset now) => EndedAt is null && now < ExpiresAt;

    public Result RecordAuthorization(string authorizationId, string clientId, bool offlineAccess, DateTimeOffset now)
    {
        if (!IsLive(now)) return Result.Fail(SessionErrors.Ended());

        // Idempotent: OpenIddict may replay the sign-in handler for the same authorization.
        if (_authorizations.Any(a => a.AuthorizationId == authorizationId)) return Result.Ok();

        _authorizations.Add(SessionAuthorization.Create(authorizationId, clientId, offlineAccess, now));
        return Result.Ok();
    }

    // An expired-but-not-ended session can still be ended: that revokes its non-offline grants.
    public Result End(EndReason reason, DateTimeOffset now)
    {
        if (EndedAt is not null) return Result.Fail(SessionErrors.AlreadyEnded());

        EndedAt = now;
        EndReason = reason;
        RaiseDomainEvent(new SessionEndedDomainEvent(
            Id,
            TenantId,
            SubjectId,
            reason,
            _authorizations.Select(a => a.ToSnapshot()).ToList()));
        return Result.Ok();
    }
}
