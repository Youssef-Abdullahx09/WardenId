using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.Protocol.Application.Abstractions;

namespace WardenId.Protocol.Application.Features.Sessions.EndForUser;

public sealed class Handler(
    IProtocolDbContext dbContext,
    TimeProvider timeProvider) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var query = dbContext.Sessions
            .Where(s => s.SubjectId == request.UserId && s.EndedAt == null);
        if (request.TenantId is { } tenantId)
            query = query.Where(s => s.TenantId == tenantId);

        var sessions = await query.ToListAsync(cancellationToken);

        // Expired-but-unended sessions are ended too so their authorizations get revoked. A user with nothing
        // left to end is a normal outcome, not a failure.
        var anyEnded = false;
        foreach (var session in sessions)
            anyEnded |= session.End(request.Reason, now).IsSuccess;

        if (anyEnded)
            await dbContext.SaveChangesAsync(request.UserId, cancellationToken);

        return Result.Ok();
    }
}
