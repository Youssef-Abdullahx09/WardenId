using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Domain;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;

namespace WardenId.ClientManagement.Application.Features.Clients.GetForAuthentication;

public sealed class Handler(IClientManagementDbContext dbContext)
    : IRequestHandler<Query, Result<ClientForAuthentication>>
{
    public async Task<Result<ClientForAuthentication>> Handle(Query request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId)) return Result.Fail(ClientErrors.NotFound);

        var clientId = ClientId.FromTrustedSource(request.ClientId);

        var client = await dbContext.Clients
            .AsNoTracking()
            .Where(c => c.TenantId == request.TenantId && c.ClientId == clientId && !c.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (client is null) return Result.Fail(ClientErrors.NotFound);

        return Result.Ok(new ClientForAuthentication(
            client.Id,
            client.ClientId.Value,
            client.ClientType == ClientType.Confidential ? ClientKind.Confidential : ClientKind.Public,
            client.Enabled,
            MapGrants(client.GrantType),
            client.ClientAuthenticationMethod == ClientAuthenticationMethod.PrivateKeyJwt
                ? ClientAuthMethod.PrivateKeyJwt
                : ClientAuthMethod.SharedSecret,
            client.TokenLifetimeOverrides?.AccessTokenSeconds,
            client.TokenLifetimeOverrides?.RefreshTokenSeconds,
            client.RedirectUris.Select(r => r.Value).ToArray(),
            client.RequirePkce));
    }

    private static List<AllowedGrant> MapGrants(GrantType grants)
    {
        var allowed = new List<AllowedGrant>();
        if (grants.HasFlag(GrantType.AuthorizationCode)) allowed.Add(AllowedGrant.AuthorizationCode);
        if (grants.HasFlag(GrantType.ClientCredentials)) allowed.Add(AllowedGrant.ClientCredentials);
        if (grants.HasFlag(GrantType.RefreshToken)) allowed.Add(AllowedGrant.RefreshToken);
        if (grants.HasFlag(GrantType.DeviceCode)) allowed.Add(AllowedGrant.DeviceCode);
        return allowed;
    }
}
