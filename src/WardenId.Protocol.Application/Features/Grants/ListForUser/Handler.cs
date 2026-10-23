using FluentResults;
using MediatR;
using WardenId.Protocol.Application.Utilities;

namespace WardenId.Protocol.Application.Features.Grants.ListForUser;

/// <summary>Every status is listed (revoked ones until OpenIddict pruning removes them), newest first.</summary>
public sealed class Handler(IUserGrantStore store) : IRequestHandler<Query, Result<IReadOnlyList<UserGrant>>>
{
    public async Task<Result<IReadOnlyList<UserGrant>>> Handle(Query request, CancellationToken cancellationToken)
    {
        var grants = await store.ListBySubjectAsync(request.UserId.ToString(), cancellationToken);

        IReadOnlyList<UserGrant> userGrants = grants
            .Where(grant => grant.BelongsTo(request.TenantId, request.UserId))
            .OrderByDescending(grant => grant.CreatedAt)
            .Select(grant => new UserGrant(
                grant.AuthorizationId,
                grant.Owner!.ClientId,
                grant.CreatedAt,
                grant.Scopes,
                grant.Scopes.Contains(OidcScopes.OfflineAccess),
                grant.Status))
            .ToList();

        return Result.Ok(userGrants);
    }
}
