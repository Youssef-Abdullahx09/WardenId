using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIddict.Server;
using WardenId.Protocol.Application;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Protocol.Application.Features.Grants;
using WardenId.Protocol.Infrastructure.Grants;
using WardenId.Protocol.Infrastructure.OpenIddict.Handlers;
using WardenId.Protocol.Infrastructure.OpenIddict.Stores;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WardenId.Protocol.Infrastructure.OpenIddict;

public static class ProtocolOpenIddictExtensions
{
    /// <summary>
    /// OpenIddict as protocol plumbing (ADR-01). Expects OpenIddictDbContext to be registered by the
    /// caller (Npgsql in AddProtocolInfrastructure; InMemory in tests). Authorization, token, introspection, revocation, userinfo and end-session
    /// endpoints are enabled — discovery and JWKS stay WardenId endpoints.
    /// </summary>
    public static void AddProtocolOpenIddict(this IServiceCollection services, IConfiguration configuration)
    {
        var tokenOptions = configuration
            .GetSection(WardenId.Protocol.Application.ConfigurationKeys.ProtocolToken.Section)
            .Get<ProtocolTokenOptions>() ?? new ProtocolTokenOptions();

        services.AddHttpContextAccessor();
        // The admin grant endpoints read authorizations through OpenIddict's manager, registered below.
        services.AddScoped<IUserGrantStore, OpenIddictUserGrantStore>();
        // W12: revoke a client's rows by owner stamp (needs ISessionAuthorizationRevoker, registered by the host).
        services.AddScoped<IClientGrantRevoker, OpenIddictClientGrantRevoker>();
        // A tenant's refresh-token absolute lifetime may not outlive the pruning age (UpdateTokens policy).
        services.AddSingleton<IRefreshTokenRetention, OpenIddictRefreshTokenRetention>();

        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore().UseDbContext<OpenIddictDbContext>();
                options.ReplaceApplicationStore<OpenIddictEntityFrameworkCoreApplication, WardenApplicationStore>();
                options.ReplaceApplicationManager<OpenIddictEntityFrameworkCoreApplication, WardenApplicationManager>();
                // Applications are answered per tenant; never let a cached entity outlive the request.
                options.DisableEntityCaching();
            })
            .AddServer(options =>
            {
                // Relative: resolved per request against BaseUri = tenant issuer (ResolveTenantFromPath).
                options.SetAuthorizationEndpointUris("authorize")
                       .SetTokenEndpointUris("token")
                       .SetIntrospectionEndpointUris("introspect")
                       .SetRevocationEndpointUris("revoke")
                       .SetUserInfoEndpointUris("userinfo")
                       .SetEndSessionEndpointUris("endsession");
                options.AllowAuthorizationCodeFlow()
                       .AllowClientCredentialsFlow()
                       .AllowRefreshTokenFlow();
                // Hard-coded OIDC scopes until per-client scopes (FR-CM-04); discovery advertises the same set.
                // offline_access is honoured only for clients allowed the RefreshToken grant (Authorize endpoint).
                options.RegisterScopes(Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.OfflineAccess);
                // Global default only: /authorize sets each code's lifetime from the tenant's policy.
                options.SetAuthorizationCodeLifetime(tokenOptions.AuthorizationCodeLifetime);
                // Rolling (default) + sliding: each redemption issues a new refresh token; the Token endpoint
                // sets each token's lifetime, clamped to the absolute cap. This is only the default ceiling.
                options.SetRefreshTokenLifetime(tokenOptions.RefreshTokenLifetime);
                // Strict reuse detection (FR-PC-03, ADR-01): any second redemption revokes the whole family.
                options.SetRefreshTokenReuseLeeway(TimeSpan.Zero);
                // WardenId owns discovery and JWKS (ADR-01/D4); clear OpenIddict's default relative
                // configuration/JWKS endpoints so they are not served under every tenant path.
                options.SetConfigurationEndpointUris(Array.Empty<Uri>());
                options.SetJsonWebKeySetEndpointUris(Array.Empty<Uri>());

                // Placeholders: OpenIddict requires server keys at startup. Access/ID tokens are signed
                // with tenant keys (AttachTenantSigningCredentials); internal tokens use Data Protection.
                options.AddEphemeralSigningKey().AddEphemeralEncryptionKey();
                options.DisableAccessTokenEncryption();
                options.UseDataProtection().PreferDefaultAccessTokenFormat();

                // Only shared-secret client auth for now (discovery advertises exactly these).
                options.Configure(server =>
                {
                    server.ClientAuthenticationMethods.Remove(ClientAuthenticationMethods.PrivateKeyJwt);
                    // OAuth 2.1: S256 only. PKCE is required per client (public / RequirePkce) via
                    // the application's requirements, not globally.
                    server.CodeChallengeMethods.Remove(CodeChallengeMethods.Plain);
                });

                options.AddEventHandler(ResolveTenantFromPath.Descriptor)
                       .AddEventHandler(ValidateTenantForIssuance.Descriptor)
                       .AddEventHandler(AttachTenantSigningCredentials.Descriptor)
                       .AddEventHandler(AttachTenantValidationKeys.Descriptor)
                       .AddEventHandler(ValidateTokenTenant.Descriptor)
                       .AddEventHandler(RejectRefreshScopeBeyondGrant.Descriptor)
                       .AddEventHandler(RevokeTokenFamily.Descriptor)
                       .AddEventHandler(RecordSessionAuthorization.Descriptor)
                       .AddEventHandler(StampAuthorizationOwner.Descriptor)
                       .AddEventHandler(StampClientCredentialsTokenOwner.Descriptor)
                       .AddEventHandler(InferClientIdFromHint.Descriptor)
                       // Audit trail (W24): token issuance, revocation and refresh-token replay.
                       .AddEventHandler(RecordTokenIssuance.Descriptor)
                       .AddEventHandler(RecordTokenRevocation.Descriptor)
                       .AddEventHandler(RecordRefreshTokenReplay.Descriptor);

                // OpenIddict's authorize-time grant check rejects offline_access (no refresh permission) locally
                // with invalid_request; the Authorize endpoint redirects invalid_scope instead (Duende-style).
                // Code flow stays gated by ValidateEndpointPermissions + ValidateResponseTypePermissions, and the
                // token endpoint's own grant-type check still enforces refresh_token at redemption.
                options.RemoveEventHandler(OpenIddictServerHandlers.Authentication.ValidateGrantTypePermissions.Descriptor);

                // Introspection and revocation are answered by OpenIddict itself; userinfo passes through
                // to Protocol.Presentation/Features/UserInfo so claims come fresh from Identity; end session
                // passes through to Features/EndSession (session lookup, confirmation page, cookie sign-out).
                var aspNetCore = options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough();
                if (configuration.GetValue<bool>(ConfigurationKeys.ProtocolOpenIddict.DisableTransportSecurityRequirement))
                    aspNetCore.DisableTransportSecurityRequirement();
            });
    }
}
