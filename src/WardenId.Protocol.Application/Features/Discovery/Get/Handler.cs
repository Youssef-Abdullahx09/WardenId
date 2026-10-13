using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using GetPublicKeysQuery = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys.Query;

namespace WardenId.Protocol.Application.Features.Discovery.Get;

public sealed class Handler(ISender sender, IOptions<ProtocolTokenOptions> options)
    : IRequestHandler<Query, Result<DiscoveryDocument>>
{
    public async Task<Result<DiscoveryDocument>> Handle(Query request, CancellationToken cancellationToken)
    {
        // Keys are unused here — the call only confirms the tenant is servable (Active/Suspended).
        var tenantResult = await sender.Send(new GetPublicKeysQuery(request.Slug), cancellationToken);
        if (tenantResult.IsFailed) return Result.Fail<DiscoveryDocument>(tenantResult.Errors);

        var issuer = IssuerUri.For(options.Value.IssuerBaseUri, tenantResult.Value.Slug);

        return Result.Ok(new DiscoveryDocument(
            Issuer: issuer,
            JwksUri: $"{issuer}/jwks",
            ResponseTypesSupported: ["code"],
            SubjectTypesSupported: ["public"],
            IdTokenSigningAlgValuesSupported: ["ES256"],
            TokenEndpoint: $"{issuer}/token",
            GrantTypesSupported: ["authorization_code", "client_credentials", "refresh_token"],
            TokenEndpointAuthMethodsSupported: ["client_secret_basic", "client_secret_post", "none"],
            AuthorizationEndpoint: $"{issuer}/authorize",
            ScopesSupported: OidcScopes.Supported,
            CodeChallengeMethodsSupported: ["S256"],
            AuthorizationResponseIssParameterSupported: true));
    }
}
