using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.EntityFrameworkCore.Models;
using WardenId.Protocol.Infrastructure.OpenIddict.Handlers;
using WardenId.Protocol.Infrastructure.OpenIddict.Stores;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WardenId.Protocol.Infrastructure.OpenIddict;

public static class ProtocolOpenIddictExtensions
{
    /// <summary>
    /// OpenIddict as protocol plumbing (ADR-01). Expects OpenIddictDbContext to be registered by the
    /// caller (Npgsql in AddProtocolInfrastructure; InMemory in tests). Only the token endpoint is
    /// enabled — discovery and JWKS stay WardenId endpoints.
    /// </summary>
    public static void AddProtocolOpenIddict(this IServiceCollection services, IConfiguration configuration)
    {
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
                options.SetTokenEndpointUris("token");
                options.AllowClientCredentialsFlow();
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
                options.Configure(server => server.ClientAuthenticationMethods.Remove(ClientAuthenticationMethods.PrivateKeyJwt));

                options.AddEventHandler(ResolveTenantFromPath.Descriptor)
                       .AddEventHandler(ValidateTenantForIssuance.Descriptor)
                       .AddEventHandler(AttachTenantSigningCredentials.Descriptor);

                var aspNetCore = options.UseAspNetCore().EnableTokenEndpointPassthrough();
                if (configuration.GetValue<bool>(ConfigurationKeys.ProtocolOpenIddict.DisableTransportSecurityRequirement))
                    aspNetCore.DisableTransportSecurityRequirement();
            });
    }
}
