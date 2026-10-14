using System.Net;
using System.Text.Json;

namespace WardenId.Protocol.Presentation.Tests;

/// <summary>Token acquisition and introspection/revocation calls shared by the protocol flow tests.</summary>
public static class TokenFlow
{
    public static async Task<JsonElement> JsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    public static async Task<JsonElement> CodeTokensAsync(
        ProtocolTestHost host, string clientId = "web-rt", string? secret = FakeDirectory.AcmeSecret,
        string redirectUri = FakeDirectory.WebRedirect, string scope = "openid profile email offline_access",
        BrowserClient? browser = null)
    {
        var pkce = OidcFlow.NewPkce();
        var code = await OidcFlow.CodeAsync(browser ?? host.CreateBrowser(), "acme",
            OidcFlow.AuthorizeParameters(clientId, redirectUri, pkce, scope), redirectUri);
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = pkce.Verifier
        };
        if (secret is not null) form["client_secret"] = secret;
        var response = await host.Client.PostAsync("/tenants/acme/token", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await JsonAsync(response);
    }

    public static async Task<JsonElement> ClientCredentialsAsync(
        ProtocolTestHost host, string slug = "acme", string clientId = "svc", string secret = FakeDirectory.AcmeSecret)
    {
        var response = await host.Client.PostAsync($"/tenants/{slug}/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = secret
        }));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await JsonAsync(response);
    }

    public static Task<HttpResponseMessage> IntrospectAsync(
        ProtocolTestHost host, string token, string clientId = "web-rt", string? secret = FakeDirectory.AcmeSecret,
        string slug = "acme", string? hint = null) =>
        PostAsync(host, $"/tenants/{slug}/introspect", token, clientId, secret, hint);

    public static Task<HttpResponseMessage> RevokeAsync(
        ProtocolTestHost host, string token, string clientId = "web-rt", string? secret = FakeDirectory.AcmeSecret,
        string slug = "acme", string? hint = null) =>
        PostAsync(host, $"/tenants/{slug}/revoke", token, clientId, secret, hint);

    public static Task<HttpResponseMessage> RefreshAsync(
        ProtocolTestHost host, string refreshToken, string clientId = "web-rt", string? secret = FakeDirectory.AcmeSecret,
        string slug = "acme")
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = clientId,
            ["refresh_token"] = refreshToken
        };
        if (secret is not null) form["client_secret"] = secret;
        return host.Client.PostAsync($"/tenants/{slug}/token", new FormUrlEncodedContent(form));
    }

    private static Task<HttpResponseMessage> PostAsync(
        ProtocolTestHost host, string path, string token, string clientId, string? secret, string? hint)
    {
        var form = new Dictionary<string, string> { ["token"] = token, ["client_id"] = clientId };
        if (secret is not null) form["client_secret"] = secret;
        if (hint is not null) form["token_type_hint"] = hint;
        return host.Client.PostAsync(path, new FormUrlEncodedContent(form));
    }
}
