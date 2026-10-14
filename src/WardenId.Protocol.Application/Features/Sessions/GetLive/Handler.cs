using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Protocol.Domain;

namespace WardenId.Protocol.Application.Features.Sessions.GetLive;

public sealed class Handler(
    IProtocolDbContext dbContext,
    TimeProvider timeProvider) : IRequestHandler<Query, Result<LiveSession>>
{
    public async Task<Result<LiveSession>> Handle(Query request, CancellationToken cancellationToken)
    {
        var session = await dbContext.Sessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.Sid && s.TenantId == request.TenantId, cancellationToken);

        // Missing, other tenant, ended and expired are indistinguishable to the caller.
        if (session is null || !session.IsLive(timeProvider.GetUtcNow()))
            return Result.Fail(SessionErrors.NotFound());

        return Result.Ok(new LiveSession(session.Id, session.SubjectId, session.AuthTime));
    }
}
