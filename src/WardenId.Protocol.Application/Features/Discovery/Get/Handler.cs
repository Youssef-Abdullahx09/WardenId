using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.SharedKernel.Domain;
using GetPublicKeysQuery = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys.Query;

namespace WardenId.Protocol.Application.Features.Discovery.Get;

public sealed class Handler(ISender sender, IOptions<ProtocolTokenOptions> options)
    : IRequestHandler<Query, Result<DiscoveryDocument>>
{
    private static readonly string[] SecretAuthMethods =
        [OidcValues.ClientAuthenticationMethods.ClientSecretBasic, OidcValues.ClientAuthenticationMethods.ClientSecretPost];

    private static readonly string[] AllAuthMethods = [.. SecretAuthMethods, OidcValues.ClientAuthenticationMethods.None];

    public async Task<Result<DiscoveryDocument>> Handle(Query request, CancellationToken cancellationToken)
    {
        // Keys are unused here — the call only confirms the tenant is servable (Active/Suspended).
        var tenantResult = await sender.Send(new GetPublicKeysQuery(request.Slug), cancellationToken);
        if (tenantResult.IsFailed) return Result.Fail<DiscoveryDocument>(tenantResult.Errors);

        var issuer = IssuerUri.For(options.Value.IssuerBaseUri, tenantResult.Value.Slug);

        return Result.Ok(new DiscoveryDocument(
            Issuer: issuer,
            JwksUri: $"{issuer}/{ProtocolPaths.Endpoints.Jwks}",
            ResponseTypesSupported: [OidcValues.ResponseTypes.Code],
            SubjectTypesSupported: [OidcValues.SubjectTypes.Public],
            IdTokenSigningAlgValuesSupported: [JsonWebKeyValues.Algorithms.Es256],
            TokenEndpoint: $"{issuer}/{ProtocolPaths.Endpoints.Token}",
            GrantTypesSupported: [
                OidcValues.GrantTypes.AuthorizationCode,
                OidcValues.GrantTypes.ClientCredentials,
                OidcValues.GrantTypes.RefreshToken
            ],
            TokenEndpointAuthMethodsSupported: AllAuthMethods,
            AuthorizationEndpoint: $"{issuer}/{ProtocolPaths.Endpoints.Authorize}",
            ScopesSupported: OidcScopes.Supported,
            CodeChallengeMethodsSupported: [OidcValues.CodeChallengeMethods.S256],
            AuthorizationResponseIssParameterSupported: true,
            IntrospectionEndpoint: $"{issuer}/{ProtocolPaths.Endpoints.Introspect}",
            // Confidential clients only (RFC 7662 requires caller authentication).
            IntrospectionEndpointAuthMethodsSupported: SecretAuthMethods,
            RevocationEndpoint: $"{issuer}/{ProtocolPaths.Endpoints.Revoke}",
            RevocationEndpointAuthMethodsSupported: AllAuthMethods,
            UserInfoEndpoint: $"{issuer}/{ProtocolPaths.Endpoints.UserInfo}",
            EndSessionEndpoint: $"{issuer}/{ProtocolPaths.Endpoints.EndSession}",
            BackchannelLogoutSupported: true,
            // logout_token always carries sid (no per-client backchannel_logout_session_required flag).
            BackchannelLogoutSessionSupported: true));
    }
}
