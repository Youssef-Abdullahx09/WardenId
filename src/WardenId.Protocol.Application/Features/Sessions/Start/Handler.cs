using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Protocol.Domain.Sessions;

namespace WardenId.Protocol.Application.Features.Sessions.Start;

public sealed class Handler(
    IProtocolDbContext dbContext,
    TimeProvider timeProvider,
    IOptions<ProtocolSessionOptions> options) : IRequestHandler<Command, Result<StartedSession>>
{
    public async Task<Result<StartedSession>> Handle(Command request, CancellationToken cancellationToken)
    {
        // auth_time is whole seconds in the cookie and tokens; storing the same value keeps all three equal.
        var authTime = DateTimeOffset.FromUnixTimeSeconds(timeProvider.GetUtcNow().ToUnixTimeSeconds());

        var sessionResult = Session.Start(
            request.TenantId, request.UserId, authTime, authTime + options.Value.AbsoluteLifetime);
        if (sessionResult.IsFailed) return Result.Fail(sessionResult.Errors);

        var session = sessionResult.Value;
        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync(session.SubjectId, cancellationToken);

        return Result.Ok(new StartedSession(session.Id, session.AuthTime));
    }
}
