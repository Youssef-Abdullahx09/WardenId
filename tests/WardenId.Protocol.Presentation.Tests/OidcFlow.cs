using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;

namespace WardenId.Protocol.Presentation.Tests;

public static partial class OidcFlow
{
    public sealed record Pkce(string Verifier, string Challenge);

    public static Pkce NewPkce()
    {
        var verifier = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return new Pkce(verifier, challenge);
    }

    public static Dictionary<string, string> AuthorizeParameters(
        string clientId, string redirectUri, Pkce? pkce, string scope = "openid profile email")
    {
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = scope,
            ["state"] = "xyz",
            ["nonce"] = "n-123"
        };
        if (pkce is not null)
        {
            parameters["code_challenge"] = pkce.Challenge;
            parameters["code_challenge_method"] = "S256";
        }
        return parameters;
    }

    public static string AuthorizeUrl(string slug, IDictionary<string, string> parameters) =>
        QueryHelpers.AddQueryString($"/tenants/{slug}/authorize", parameters!);

    /// <summary>Follows authorize → login → authorize; returns the final response (302 to the RP on success).</summary>
    public static async Task<HttpResponseMessage> AuthorizeWithLoginAsync(
        BrowserClient browser, string slug, IDictionary<string, string> parameters, string username = "alice")
    {
        var start = await browser.GetAsync(AuthorizeUrl(slug, parameters));
        if (start.StatusCode != HttpStatusCode.Redirect || !start.Headers.Location!.OriginalString.StartsWith($"/tenants/{slug}/login"))
            return start;

        var loginUrl = start.Headers.Location.OriginalString;
        var page = await browser.GetAsync(loginUrl);
        var token = BrowserClient.ReadAntiforgeryToken(await page.Content.ReadAsStringAsync());
        var returnUrl = QueryHelpers.ParseQuery(new Uri("https://x" + loginUrl).Query)["ReturnUrl"].ToString();
        var login = await browser.PostFormAsync(loginUrl, new Dictionary<string, string>
        {
            ["EmailOrUsername"] = username,
            ["Password"] = FakeDirectory.UserPassword,
            ["ReturnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = token
        });
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        return await browser.GetAsync(login.Headers.Location!.OriginalString);
    }

    public static Dictionary<string, string> RedirectQuery(HttpResponseMessage response, string expectedRedirectUri)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!;
        Assert.StartsWith(expectedRedirectUri + "?", location.AbsoluteUri);
        return QueryHelpers.ParseQuery(location.Query).ToDictionary(p => p.Key, p => p.Value.ToString());
    }

    public static async Task<string> CodeAsync(BrowserClient browser, string slug, IDictionary<string, string> parameters, string redirectUri)
    {
        var query = RedirectQuery(await AuthorizeWithLoginAsync(browser, slug, parameters), redirectUri);
        Assert.True(query.ContainsKey("code"), string.Join("&", query.Select(p => $"{p.Key}={p.Value}")));
        return query["code"];
    }

    /// <summary>The OAuth error, whether OpenIddict redirected it to the RP or answered locally.</summary>
    public static async Task<string?> AuthorizeErrorAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location is { IsAbsoluteUri: true } location)
            return QueryHelpers.ParseQuery(location.Query).TryGetValue("error", out var error) ? error.ToString() : null;

        var match = ErrorPattern().Match(await response.Content.ReadAsStringAsync());
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"\berror\b\W+([a-z_]+)")]
    private static partial Regex ErrorPattern();
}
