using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Application.Features.Clients.Get;

public sealed class Handler(
    IClientManagementDbContext dbContext) : IRequestHandler<Query, Result<ClientDetail>>
{
    public async Task<Result<ClientDetail>> Handle(
        Query request,
        CancellationToken cancellationToken)
    {
        var client = await dbContext.Clients
            .Include(c => c.Secrets)
            .Include(c => c.SigningKeys)
            .Where(c => c.Id == request.ClientId && c.TenantId == request.TenantId && !c.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (client is null) return Result.Fail(ClientErrors.NotFound);

        var detail = new ClientDetail(
            client.Id,
            client.ClientId.Value,
            client.TenantId,
            client.ClientType,
            client.DisplayName,
            client.Description,
            client.Enabled,
            client.IsFirstParty,
            client.GrantType,
            client.RequirePkce,
            client.ClientAuthenticationMethod,
            client.ConsentRequired,
            client.TokenLifetimeOverrides?.AccessTokenSeconds,
            client.TokenLifetimeOverrides?.IdTokenSeconds,
            client.TokenLifetimeOverrides?.RefreshTokenSeconds,
            client.RedirectUris.Select(r => r.Value).ToArray(),
            client.PostLogoutRedirectUris.Select(r => r.Value).ToArray(),
            client.CorsOrigins.Select(o => o.Value).ToArray(),
            client.Secrets.Select(s => new ClientSecretSummary(s.Id, s.Label, s.ExpiresAt)).ToArray(),
            client.SigningKeys.Select(k => new ClientSigningKeySummary(k.Id, k.Kid, k.Algorithm, k.ExpiresAt)).ToArray(),
            client.AssignedScopeIds.ToArray(),
            client.BackchannelLogoutUri?.Value);

        return Result.Ok(detail);
    }
}
