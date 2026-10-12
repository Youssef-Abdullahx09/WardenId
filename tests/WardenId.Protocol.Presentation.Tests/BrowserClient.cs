using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.TestHost;

namespace WardenId.Protocol.Presentation.Tests;

/// <summary>Minimal browser: https base, cookie jar honouring Path, no automatic redirects.</summary>
public sealed partial class BrowserClient(TestServer server)
{
    private static readonly Uri BaseAddress = new("https://wardenid.test/");
    private readonly HttpMessageInvoker _invoker = new(server.CreateHandler());

    public CookieContainer Cookies { get; } = new();

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        request.RequestUri = new Uri(BaseAddress, request.RequestUri!);
        var cookieHeader = Cookies.GetCookieHeader(request.RequestUri);
        if (cookieHeader.Length > 0) request.Headers.Add("Cookie", cookieHeader);

        var response = await _invoker.SendAsync(request, CancellationToken.None);
        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var setCookie in setCookies) Cookies.SetCookies(request.RequestUri, setCookie);
        }
        return response;
    }

    public Task<HttpResponseMessage> GetAsync(string url) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, url));

    public Task<HttpResponseMessage> PostFormAsync(string url, IEnumerable<KeyValuePair<string, string>> form) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) });

    public static string ReadAntiforgeryToken(string html)
    {
        var match = AntiforgeryPattern().Match(html);
        Assert.True(match.Success, "No antiforgery token in page.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryPattern();
}
