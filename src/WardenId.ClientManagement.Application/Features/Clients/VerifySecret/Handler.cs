using FluentResults;
using MediatR;
using WardenId.ClientManagement.Domain;
using WardenId.ClientManagement.Integration.Command.Features.Clients.VerifySecret;
using WardenId.ClientManagement.Query.Contracts;
using ClientsQuery = WardenId.ClientManagement.Query.Contracts.Features.Clients.VerifySecret.Query;

namespace WardenId.ClientManagement.Application.Features.Clients.VerifySecret;

// The sibling namespace WardenId.ClientManagement.Query would otherwise shadow the imported type.
using Query = WardenId.ClientManagement.Integration.Command.Features.Clients.VerifySecret.Query;

/// <summary>
/// Verifies a presented client secret against every non-expired secret of a confidential,
/// enabled, shared-secret client. Every failure returns the same InvalidSecret error.
/// The client and its secret hashes come from the Dapper query layer; eligibility, expiry and matching stay here.
/// </summary>
public sealed class Handler(IClientsQuery clientsQuery) : IRequestHandler<Query, Result>
{
    public async Task<Result> Handle(Query request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrEmpty(request.PresentedSecret))
            return Result.Fail(ClientErrors.InvalidSecret());

        var result = await clientsQuery.VerifySecret(
            new ClientsQuery(request.TenantId, request.ClientId),
            cancellationToken);
        if (result.IsFailed) return Result.Fail(result.Errors);

        var client = result.Value;
        if (client is null
            || !client.Enabled
            || ClientRows.ParseEnum<ClientType>(client.ClientType) != ClientType.Confidential
            || ClientRows.ParseEnum<ClientAuthenticationMethod>(client.ClientAuthenticationMethod)
                != ClientAuthenticationMethod.SharedSecret)
        {
            return Result.Fail(ClientErrors.InvalidSecret());
        }

        var now = DateTimeOffset.UtcNow;
        var liveHashes = client.Secrets
            .Where(s => ClientRows.AsUtc(s.ExpiresAt) is not { } expiresAt || expiresAt > now)
            .Select(s => s.HashedValue);

        return ClientSecretHasher.Matches(request.PresentedSecret, liveHashes)
            ? Result.Ok()
            : Result.Fail(ClientErrors.InvalidSecret());
    }
}
