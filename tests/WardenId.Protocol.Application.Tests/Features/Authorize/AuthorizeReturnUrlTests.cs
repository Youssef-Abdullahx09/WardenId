using WardenId.Protocol.Application.Features.Authorize;

namespace WardenId.Protocol.Application.Tests.Features.Authorize;

public class AuthorizeReturnUrlTests
{
    [Theory]
    [InlineData("/tenants/acme/authorize")]
    [InlineData("/tenants/acme/authorize?client_id=web&state=x")]
    public void Allows_SameTenantAuthorizePath(string url)
    {
        Assert.True(AuthorizeReturnUrl.IsAllowed(url, "acme"));
        Assert.True(AuthorizeReturnUrl.IsAuthorizeRequest(url, "acme"));
        Assert.False(AuthorizeReturnUrl.IsAccountPage(url, "acme"));
    }

    [Theory]
    [InlineData("/tenants/acme/account")]
    [InlineData("/tenants/acme/account/password")]
    [InlineData("/tenants/acme/account/authenticator/replace")]
    [InlineData("/tenants/acme/account/two-step/remove")]
    public void Allows_SameTenantAccountPages(string url)
    {
        Assert.True(AuthorizeReturnUrl.IsAllowed(url, "acme"));
        Assert.True(AuthorizeReturnUrl.IsAccountPage(url, "acme"));
        Assert.False(AuthorizeReturnUrl.IsAuthorizeRequest(url, "acme"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://evil.example/tenants/acme/authorize")]
    [InlineData("//evil.example/tenants/acme/authorize")]
    [InlineData("/\\evil.example")]
    [InlineData("/tenants/acme/authorize\\..\\x")]
    [InlineData("/tenants/globex/authorize?client_id=web")]
    [InlineData("/tenants/acme/token")]
    [InlineData("/tenants/acme/authorizeX")]
    [InlineData("/tenants/acme/authorize/../token")]
    [InlineData("/tenants/acme/authorize?x=1\r\nSet-Cookie: a=b")]
    [InlineData("/tenants/globex/account")]
    [InlineData("/tenants/globex/account/password")]
    [InlineData("https://evil.example/tenants/acme/account")]
    [InlineData("//evil.example/tenants/acme/account")]
    [InlineData("/tenants/acme/accounts")]
    [InlineData("/tenants/acme/account/")]
    [InlineData("/tenants/acme/account//evil.example")]
    [InlineData("/tenants/acme/account/../token")]
    [InlineData("/tenants/acme/account/./password")]
    [InlineData("/tenants/acme/account?x=1")]
    [InlineData("/tenants/acme/account/password?x=1")]
    [InlineData("/tenants/acme/account/Password")]
    [InlineData("/tenants/acme/account/%2e%2e")]
    [InlineData("/tenants/acme/account\\evil")]
    [InlineData("/tenants/acme/account/x\r\nSet-Cookie: a=b")]
    [InlineData("/tenants/acme/login")]
    [InlineData("/tenants/acme/endsession")]
    public void Rejects_EverythingElse(string? url) => Assert.False(AuthorizeReturnUrl.IsAllowed(url, "acme"));
}
