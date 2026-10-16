using FluentResults;
using MediatR;
using WardenId.Protocol.Application.Features.BackchannelLogout;

namespace WardenId.Protocol.Application.Features.Grants.RevokeForUser;

/// <summary>
/// Revokes every valid grant of the user in the tenant, offline ones included, with their tokens. Login sessions are
/// left alone (ending them is Sessions/EndForUser). Idempotent: succeeds when there is nothing to revoke.
/// </summary>
public sealed class Handler(IUserGrantStore store, ISessionAuthorizationRevoker revoker) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var grants = await store.ListBySubjectAsync(request.UserId.ToString(), cancellationToken);

        var authorizationIds = grants
            .Where(grant => grant.BelongsTo(request.TenantId, request.UserId) && grant.Status == GrantStatus.Valid)
            .Select(grant => grant.AuthorizationId)
            .ToList();

        if (authorizationIds.Count > 0)
            await revoker.RevokeAsync(authorizationIds, cancellationToken);

        return Result.Ok();
    }
}
