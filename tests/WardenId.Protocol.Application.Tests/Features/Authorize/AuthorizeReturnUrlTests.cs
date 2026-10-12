using WardenId.Protocol.Application.Features.Authorize;

namespace WardenId.Protocol.Application.Tests.Features.Authorize;

public class AuthorizeReturnUrlTests
{
    [Theory]
    [InlineData("/tenants/acme/authorize")]
    [InlineData("/tenants/acme/authorize?client_id=web&state=x")]
    public void Allows_SameTenantAuthorizePath(string url) => Assert.True(AuthorizeReturnUrl.IsAllowed(url, "acme"));

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
    public void Rejects_EverythingElse(string? url) => Assert.False(AuthorizeReturnUrl.IsAllowed(url, "acme"));
}
