using Microsoft.AspNetCore.Http;

namespace WardenId.Protocol.Presentation.Tests;

public class TenantPathCookieBuilderTests
{
    [Theory]
    [InlineData("/tenants/acme/login", "/tenants/acme")]
    [InlineData("/tenants/acme/authorize", "/tenants/acme")]
    [InlineData("/tenants/acme", "/tenants/acme")]
    public void TenantPath_IsSlugScoped(string requestPath, string expected) =>
        Assert.Equal(expected, TenantPathCookieBuilder.TenantPath(new PathString(requestPath)));

    [Theory]
    [InlineData("/")]
    [InlineData("/api/v1/tenants")]
    [InlineData("/tenants/ACME/login")]
    [InlineData("/tenants/a/login")]
    public void TenantPath_IsNull_OutsideATenant(string requestPath) =>
        Assert.Null(TenantPathCookieBuilder.TenantPath(new PathString(requestPath)));

    // Review focus: sliding renewal re-issues the cookie through Build(), not SignInAsync.
    [Fact]
    public void Build_UsesTenantPath_ForAnyCookieWrite()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/tenants/acme/authorize";

        var options = new TenantPathCookieBuilder().Build(context, DateTimeOffset.UtcNow);

        Assert.Equal("/tenants/acme", options.Path);
        Assert.True(options.HttpOnly);
        Assert.True(options.Secure);
        Assert.Equal(SameSiteMode.Lax, options.SameSite);
    }
}
