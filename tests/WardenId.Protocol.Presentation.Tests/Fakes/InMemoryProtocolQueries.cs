using Microsoft.EntityFrameworkCore;
using WardenId.Protocol.Infrastructure.Persistence;
using WardenId.Protocol.Query.Contracts;
using WardenId.SharedKernel.Query.Contracts;
using GetLive = WardenId.Protocol.Query.Contracts.Features.Sessions.GetLive;
using GetTokens = WardenId.Protocol.Query.Contracts.Features.Policies.GetTokens;
using List = WardenId.Protocol.Query.Contracts.Features.Sessions.List;
using ListForUser = WardenId.Protocol.Query.Contracts.Features.Sessions.ListForUser;

namespace WardenId.Protocol.Presentation.Tests.Fakes;

/// <summary>
/// Stands in for the Dapper session queries (Protocol.Query), which can't run on EF InMemory: reads the same rows
/// from the InMemory <see cref="ProtocolDbContext"/> the real write handlers use. Only what the flows need.
/// </summary>
public sealed class InMemorySessionsQuery(ProtocolDbContext dbContext) : ISessionsQuery
{
    public async Task<Result<GetLive.Item?>> GetLive(GetLive.Query query, CancellationToken cancellationToken)
    {
        var session = await dbContext.Sessions
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == query.Sid && s.TenantId == query.TenantId, cancellationToken);

        return session is null
            ? Result.Ok<GetLive.Item?>(null)
            : Result.Ok<GetLive.Item?>(new GetLive.Item
            {
                Sid = session.Id,
                UserId = session.SubjectId,
                AuthTime = session.AuthTime.UtcDateTime,
                ExpiresAt = session.ExpiresAt.UtcDateTime,
                EndedAt = session.EndedAt?.UtcDateTime
            });
    }

    public Task<Result<List<ListForUser.Item>>> ListForUser(ListForUser.Query query, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not used by the protocol flow tests.");

    public Task<Result<PaginationListResponse<List.Item>>> List(List.Query query, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Not used by the protocol flow tests.");
}

/// <summary>Stands in for the Dapper policies query: the tenant's overrides from the InMemory context.</summary>
public sealed class InMemoryPoliciesQuery(ProtocolDbContext dbContext) : IPoliciesQuery
{
    public async Task<Result<GetTokens.Item?>> GetTokens(GetTokens.Query query, CancellationToken cancellationToken)
    {
        var policy = await dbContext.TenantTokenPolicies
            .AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == query.TenantId, cancellationToken);

        return policy is null
            ? Result.Ok<GetTokens.Item?>(null)
            : Result.Ok<GetTokens.Item?>(new GetTokens.Item
            {
                AccessTokenLifetime = policy.AccessTokenLifetime,
                IdTokenLifetime = policy.IdTokenLifetime,
                RefreshTokenLifetime = policy.RefreshTokenLifetime,
                RefreshTokenAbsoluteLifetime = policy.RefreshTokenAbsoluteLifetime,
                AuthorizationCodeLifetime = policy.AuthorizationCodeLifetime,
                SessionCookieLifetime = policy.SessionCookieLifetime,
                SessionAbsoluteLifetime = policy.SessionAbsoluteLifetime
            });
    }
}
