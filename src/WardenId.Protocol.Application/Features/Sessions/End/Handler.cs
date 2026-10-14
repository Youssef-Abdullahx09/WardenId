using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Protocol.Domain;

namespace WardenId.Protocol.Application.Features.Sessions.End;

public sealed class Handler(
    IProtocolDbContext dbContext,
    TimeProvider timeProvider) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var session = await dbContext.Sessions
            .FirstOrDefaultAsync(s => s.Id == request.Sid && s.TenantId == request.TenantId, cancellationToken);

        if (session is null || session.EndedAt is not null)
            return request.RequireExisting ? Result.Fail(SessionErrors.NotFound()) : Result.Ok();

        var ended = session.End(request.Reason, timeProvider.GetUtcNow());
        if (ended.IsFailed) return ended;

        await dbContext.SaveChangesAsync(session.SubjectId, cancellationToken);
        return Result.Ok();
    }
}
