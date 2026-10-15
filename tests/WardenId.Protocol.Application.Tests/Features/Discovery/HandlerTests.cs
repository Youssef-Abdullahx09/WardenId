using Microsoft.Extensions.Options;
using WardenId.Protocol.Application.Features.Discovery.Get;
using WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys;
using GetPublicKeysQuery = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys.Query;
using DiscoveryQuery = WardenId.Protocol.Application.Features.Discovery.Get.Query;

namespace WardenId.Protocol.Application.Tests.Features.Discovery;

public class HandlerTests
{
    private static Handler CreateHandler(FakeSender sender, string issuerBaseUri = "https://wardenid.test") =>
        new(sender, Options.Create(new ProtocolTokenOptions { IssuerBaseUri = issuerBaseUri }));

    private static Result<TenantPublicKeys> Servable(string slug) =>
        Result.Ok(new TenantPublicKeys(Guid.NewGuid(), slug, []));

    [Fact]
    public async Task Handle_BuildsDocumentForTenantIssuer()
    {
        var sender = new FakeSender { PublicKeysResponse = Servable("acme") };

        var result = await CreateHandler(sender).Handle(new DiscoveryQuery("acme"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var doc = result.Value;
        Assert.Equal("https://wardenid.test/tenants/acme", doc.Issuer);
        Assert.Equal("https://wardenid.test/tenants/acme/jwks", doc.JwksUri);
        Assert.Equal(new[] { "code" }, doc.ResponseTypesSupported);
        Assert.Equal(new[] { "public" }, doc.SubjectTypesSupported);
        Assert.Equal(new[] { "ES256" }, doc.IdTokenSigningAlgValuesSupported);
        Assert.Equal("https://wardenid.test/tenants/acme/token", doc.TokenEndpoint);
        Assert.Equal(new[] { "authorization_code", "client_credentials", "refresh_token" }, doc.GrantTypesSupported);
        Assert.Equal(new[] { "client_secret_basic", "client_secret_post", "none" }, doc.TokenEndpointAuthMethodsSupported);
        Assert.Equal("https://wardenid.test/tenants/acme/authorize", doc.AuthorizationEndpoint);
        Assert.Equal(new[] { "openid", "profile", "email", "offline_access" }, doc.ScopesSupported);
        Assert.Equal(new[] { "S256" }, doc.CodeChallengeMethodsSupported);
        Assert.True(doc.AuthorizationResponseIssParameterSupported);
        Assert.Equal("https://wardenid.test/tenants/acme/introspect", doc.IntrospectionEndpoint);
        Assert.Equal(new[] { "client_secret_basic", "client_secret_post" }, doc.IntrospectionEndpointAuthMethodsSupported);
        Assert.Equal("https://wardenid.test/tenants/acme/revoke", doc.RevocationEndpoint);
        Assert.Equal(new[] { "client_secret_basic", "client_secret_post", "none" }, doc.RevocationEndpointAuthMethodsSupported);
        Assert.Equal("https://wardenid.test/tenants/acme/userinfo", doc.UserInfoEndpoint);
        Assert.Equal("https://wardenid.test/tenants/acme/endsession", doc.EndSessionEndpoint);
        Assert.True(doc.BackchannelLogoutSupported);
        Assert.True(doc.BackchannelLogoutSessionSupported);
        Assert.Equal("acme", Assert.IsType<GetPublicKeysQuery>(Assert.Single(sender.Sent)).Slug);
    }

    [Fact]
    public async Task Handle_DoesNotDoubleSlash_WhenBaseUriHasTrailingSlash()
    {
        var sender = new FakeSender { PublicKeysResponse = Servable("acme") };

        var result = await CreateHandler(sender, "https://wardenid.test/").Handle(new DiscoveryQuery("acme"), CancellationToken.None);

        Assert.Equal("https://wardenid.test/tenants/acme", result.Value.Issuer);
        Assert.Equal("https://wardenid.test/tenants/acme/jwks", result.Value.JwksUri);
    }

    [Fact]
    public async Task Handle_PropagatesNotFound()
    {
        var notFound = new Error("Tenant not found.").WithMetadata("ErrorCode", "NotFound");
        var sender = new FakeSender { PublicKeysResponse = Result.Fail<TenantPublicKeys>(notFound) };

        var result = await CreateHandler(sender).Handle(new DiscoveryQuery("acme"), CancellationToken.None);

        Assert.True(result.IsFailed);
        Assert.Equal("NotFound", result.Errors[0].Metadata["ErrorCode"]);
    }

    [Fact]
    public void Document_SerializesWithOidcPropertyNames()
    {
        var doc = new DiscoveryDocument(
            "i", "j", ["code"], ["public"], ["ES256"], "t", ["client_credentials"], ["client_secret_basic"],
            "a", ["openid"], ["S256"], true, "in", ["client_secret_basic"], "r", ["none"], "u", "e", true, true);

        var json = System.Text.Json.JsonSerializer.Serialize(doc);

        Assert.Equal(
            """{"issuer":"i","jwks_uri":"j","response_types_supported":["code"],"subject_types_supported":["public"],"id_token_signing_alg_values_supported":["ES256"],"token_endpoint":"t","grant_types_supported":["client_credentials"],"token_endpoint_auth_methods_supported":["client_secret_basic"],"authorization_endpoint":"a","scopes_supported":["openid"],"code_challenge_methods_supported":["S256"],"authorization_response_iss_parameter_supported":true,"introspection_endpoint":"in","introspection_endpoint_auth_methods_supported":["client_secret_basic"],"revocation_endpoint":"r","revocation_endpoint_auth_methods_supported":["none"],"userinfo_endpoint":"u","end_session_endpoint":"e","backchannel_logout_supported":true,"backchannel_logout_session_supported":true}""",
            json);
    }
}
