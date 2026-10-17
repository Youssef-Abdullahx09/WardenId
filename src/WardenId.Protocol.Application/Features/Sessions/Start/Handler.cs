using FluentResults;
using MediatR;
using WardenId.Audit.Integration.Command.Features.Events.Record;
using WardenId.Protocol.Application.Auditing;
using WardenId.Protocol.Application.Features.Authorize;
using WardenId.Protocol.Application.Features.Policies;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Protocol.Domain.Sessions;
using RecordAuditEventCommand = WardenId.Audit.Integration.Command.Features.Events.Record.Command;

namespace WardenId.Protocol.Application.Features.Sessions.Start;

/// <summary>
/// Starts the login session once sign-in is complete (password, plus any MFA step) and audits it as the
/// interactive sign-in success.
/// </summary>
public sealed class Handler(
    IProtocolDbContext dbContext,
    TimeProvider timeProvider,
    ITenantLifetimes tenantLifetimes,
    IAuthAuditTrail audit) : IRequestHandler<Command, Result<StartedSession>>
{
    public async Task<Result<StartedSession>> Handle(Command request, CancellationToken cancellationToken)
    {
        // auth_time is whole seconds in the cookie and tokens; storing the same value keeps all three equal.
        var authTime = DateTimeOffset.FromUnixTimeSeconds(timeProvider.GetUtcNow().ToUnixTimeSeconds());

        // The tenant's absolute lifetime (or the global one), fixed on the row now: a later change doesn't move it.
        var lifetimes = await tenantLifetimes.GetAsync(request.TenantId, cancellationToken);
        var sessionResult = Session.Start(
            request.TenantId, request.UserId, authTime, authTime + lifetimes.SessionAbsolute);
        if (sessionResult.IsFailed) return Result.Fail(sessionResult.Errors);

        var session = sessionResult.Value;
        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync(session.SubjectId, cancellationToken);

        await audit.RecordAsync(new RecordAuditEventCommand(
            SecurityEvent.SessionStarted,
            session.TenantId,
            authTime,
            Actor.User(session.SubjectId),
            [Target.User(session.SubjectId), Target.Session(session.Id)],
            Session: new SessionContext(session.Id, session.AuthTime, request.Amr ?? AuthenticationMethods.PasswordOnly)));

        return Result.Ok(new StartedSession(session.Id, session.AuthTime));
    }
}
