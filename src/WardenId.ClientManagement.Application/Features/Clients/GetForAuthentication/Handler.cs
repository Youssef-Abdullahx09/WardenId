using FluentResults;
using MediatR;
using WardenId.ClientManagement.Domain;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;
using WardenId.ClientManagement.Query.Contracts;
using ClientsQuery = WardenId.ClientManagement.Query.Contracts.Features.Clients.GetForAuthentication.Query;

namespace WardenId.ClientManagement.Application.Features.Clients.GetForAuthentication;

// The sibling namespace WardenId.ClientManagement.Query would otherwise shadow the imported type.
using Query = WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication.Query;

/// <summary>Read side: the client comes from the Dapper query layer (ClientManagement.Query), not the aggregate.</summary>
public sealed class Handler(IClientsQuery clientsQuery)
    : IRequestHandler<Query, Result<ClientForAuthentication>>
{
    public async Task<Result<ClientForAuthentication>> Handle(Query request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId)) return Result.Fail(ClientErrors.NotFound);

        var result = await clientsQuery.GetForAuthentication(
            new ClientsQuery(request.TenantId, request.ClientId),
            cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        var client = result.Value;
        if (client is null) return Result.Fail(ClientErrors.NotFound);

        return Result.Ok(new ClientForAuthentication(
            client.Id,
            client.ClientId,
            ClientRows.ParseEnum<ClientType>(client.ClientType) == ClientType.Confidential
                ? ClientKind.Confidential
                : ClientKind.Public,
            client.Enabled,
            MapGrants(ClientRows.ParseEnum<GrantType>(client.GrantType)),
            ClientRows.ParseEnum<ClientAuthenticationMethod>(client.ClientAuthenticationMethod)
                == ClientAuthenticationMethod.PrivateKeyJwt
                ? ClientAuthMethod.PrivateKeyJwt
                : ClientAuthMethod.SharedSecret,
            client.AccessTokenSeconds,
            client.RefreshTokenSeconds,
            client.RedirectUris,
            client.RequirePkce,
            client.PostLogoutRedirectUris,
            client.BackchannelLogoutUri,
            client.IdTokenSeconds));
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
