using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Protocol.Domain;

namespace WardenId.Protocol.Application.Features.Sessions.RecordAuthorization;

public sealed class Handler(
    IProtocolDbContext dbContext,
    TimeProvider timeProvider) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var session = await dbContext.Sessions
            .FirstOrDefaultAsync(s => s.Id == request.Sid && s.TenantId == request.TenantId, cancellationToken);
        if (session is null) return Result.Fail(SessionErrors.NotFound());

        var recorded = session.RecordAuthorization(
            request.AuthorizationId, request.ClientId, request.OfflineAccess, timeProvider.GetUtcNow());
        if (recorded.IsFailed) return recorded;

        await dbContext.SaveChangesAsync(session.SubjectId, cancellationToken);
        return Result.Ok();
    }
}
