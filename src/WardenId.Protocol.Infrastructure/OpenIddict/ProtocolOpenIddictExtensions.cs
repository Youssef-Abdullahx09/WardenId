using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;
using WardenId.Protocol.Application;
using WardenId.Protocol.Infrastructure.OpenIddict.Handlers;
using WardenId.Protocol.Infrastructure.OpenIddict.Stores;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WardenId.Protocol.Infrastructure.OpenIddict;

public static class ProtocolOpenIddictExtensions
{
    /// <summary>
    /// OpenIddict as protocol plumbing (ADR-01). Expects OpenIddictDbContext to be registered by the
    /// caller (Npgsql in AddProtocolInfrastructure; InMemory in tests). Authorization and token endpoints are
    /// enabled — discovery and JWKS stay WardenId endpoints.
    /// </summary>
    public static void AddProtocolOpenIddict(this IServiceCollection services, IConfiguration configuration)
    {
        var tokenOptions = configuration
            .GetSection(WardenId.Protocol.Application.ConfigurationKeys.ProtocolToken.Section)
            .Get<ProtocolTokenOptions>() ?? new ProtocolTokenOptions();

        services.AddHttpContextAccessor();

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
                       .SetTokenEndpointUris("token");
                options.AllowAuthorizationCodeFlow()
                       .AllowClientCredentialsFlow();
                // Hard-coded OIDC scopes until per-client scopes (FR-CM-04); discovery advertises the same set.
                options.RegisterScopes(Scopes.OpenId, Scopes.Profile, Scopes.Email);
                options.SetAuthorizationCodeLifetime(tokenOptions.AuthorizationCodeLifetime);
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
                       .AddEventHandler(AttachTenantSigningCredentials.Descriptor);

                var aspNetCore = options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough();
                if (configuration.GetValue<bool>(ConfigurationKeys.ProtocolOpenIddict.DisableTransportSecurityRequirement))
                    aspNetCore.DisableTransportSecurityRequirement();
            });
    }
}
