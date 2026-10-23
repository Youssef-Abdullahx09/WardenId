using FluentResults;
using MediatR;
using WardenId.ClientManagement.Domain;
using WardenId.ClientManagement.Query.Contracts;
using ClientsQuery = WardenId.ClientManagement.Query.Contracts.Features.Clients.Get.Query;

namespace WardenId.ClientManagement.Application.Features.Clients.Get;

/// <summary>Read side: the client comes from the Dapper query layer (ClientManagement.Query), not the aggregate.</summary>
public sealed class Handler(
    IClientsQuery clientsQuery) : IRequestHandler<Query, Result<ClientDetail>>
{
    public async Task<Result<ClientDetail>> Handle(
        Query request,
        CancellationToken cancellationToken)
    {
        var result = await clientsQuery.Get(
            new ClientsQuery(request.TenantId, request.ClientId),
            cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        var client = result.Value;
        if (client is null) return Result.Fail(ClientErrors.NotFound);

        var detail = new ClientDetail(
            client.Id,
            client.ClientId,
            client.TenantId,
            ClientRows.ParseEnum<ClientType>(client.ClientType),
            client.DisplayName,
            client.Description,
            client.Enabled,
            client.IsFirstParty,
            ClientRows.ParseEnum<GrantType>(client.GrantType),
            client.RequirePkce,
            ClientRows.ParseEnum<ClientAuthenticationMethod>(client.ClientAuthenticationMethod),
            client.ConsentRequired,
            client.AccessTokenSeconds,
            client.IdTokenSeconds,
            client.RefreshTokenSeconds,
            client.RedirectUris,
            client.PostLogoutRedirectUris,
            client.CorsOrigins,
            client.Secrets
                .Select(s => new ClientSecretSummary(s.Id, s.Label, ClientRows.AsUtc(s.ExpiresAt)))
                .ToArray(),
            client.SigningKeys
                .Select(k => new ClientSigningKeySummary(k.Id, k.Kid, k.Algorithm, ClientRows.AsUtc(k.ExpiresAt)))
                .ToArray(),
            client.AssignedScopeIds,
            client.BackchannelLogoutUri);

        return Result.Ok(detail);
    }
}
