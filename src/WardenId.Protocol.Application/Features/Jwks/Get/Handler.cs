using System.Text.Json;
using FluentResults;
using MediatR;
using GetPublicKeysQuery = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys.Query;

namespace WardenId.Protocol.Application.Features.Jwks.Get;

public sealed class Handler(ISender sender) : IRequestHandler<Query, Result<JsonWebKeySetDocument>>
{
    // Private-key members across JWK key types (EC/OKP: d; RSA: d, p, q, dp, dq, qi, oth; oct: k).
    private static readonly string[] PrivateMembers = ["d", "p", "q", "dp", "dq", "qi", "oth", "k"];

    public async Task<Result<JsonWebKeySetDocument>> Handle(Query request, CancellationToken cancellationToken)
    {
        var tenantResult = await sender.Send(new GetPublicKeysQuery(request.Slug), cancellationToken);
        if (tenantResult.IsFailed) return Result.Fail<JsonWebKeySetDocument>(tenantResult.Errors);

        var keys = tenantResult.Value.PublicKeyJwks.Select(ParsePublicJwk).ToList();

        return Result.Ok(new JsonWebKeySetDocument(keys));
    }

    // A stored JWK that is unparseable or carries private material is an invariant violation:
    // throw (→ 500) rather than skip — a silently missing key causes unexplained RP validation
    // failures, and a leaked private member is a key compromise.
    private static JsonElement ParsePublicJwk(string jwk)
    {
        using var document = JsonDocument.Parse(jwk);
        var root = document.RootElement;

        foreach (var member in PrivateMembers)
        {
            if (root.TryGetProperty(member, out _))
                throw new InvalidOperationException(
                    $"Stored signing key JWK contains private member '{member}' and must never be published.");
        }

        return root.Clone();
    }
}
