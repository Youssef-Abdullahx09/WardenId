using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WardenId.Protocol.Presentation.Cors;

namespace WardenId.Protocol.Presentation.Tests;

/// <summary>W17b: which origins may call a tenant's browser-facing protocol endpoints (real handlers, fake directory).</summary>
public class ProtocolCorsPolicyResolverTests : IAsyncLifetime
{
    private ProtocolTestHost _host = null!;

    public async Task InitializeAsync() => _host = await ProtocolTestHost.StartAsync();

    public async Task DisposeAsync() => await _host.DisposeAsync();

    private async Task<Microsoft.AspNetCore.Cors.Infrastructure.CorsPolicy?> ResolveAsync(string path)
    {
        using var scope = _host.App.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Path = path;
        return await scope.ServiceProvider.GetRequiredService<IProtocolCorsPolicyResolver>().ResolveAsync(context);
    }

    [Theory]
    [InlineData("/tenants/acme/token")]
    [InlineData("/tenants/acme/revoke")]
    [InlineData("/tenants/acme/userinfo")]
    public async Task BrowserFacingEndpoint_AllowsTheTenantsClientOrigins(string path)
    {
        var policy = await ResolveAsync(path);

        Assert.NotNull(policy);
        Assert.Equal(new[] { FakeDirectory.SpaOrigin }, policy.Origins);
        Assert.False(policy.AllowAnyOrigin);
        Assert.False(policy.SupportsCredentials);
        Assert.Contains("POST", policy.Methods);
    }

    [Fact]
    public async Task OtherTenant_DoesNotInheritAcmesOrigins()
    {
        var policy = await ResolveAsync("/tenants/globex/token");

        Assert.NotNull(policy);
        Assert.Empty(policy.Origins);
    }

    [Theory]
    [InlineData("/tenants/nope/token")]
    [InlineData("/tenants/frozen/token")]
    public async Task UnknownOrSuspendedTenant_AllowsNoOrigin(string path) =>
        Assert.Empty((await ResolveAsync(path))!.Origins);

    // Introspection needs client authentication (public clients get unauthorized_client); authorize and endsession are
    // top-level navigations; discovery/JWKS carry their own any-origin policy.
    [Theory]
    [InlineData("/tenants/acme/introspect")]
    [InlineData("/tenants/acme/authorize")]
    [InlineData("/tenants/acme/endsession")]
    [InlineData("/tenants/acme/.well-known/openid-configuration")]
    [InlineData("/api/v1/admin/tenants")]
    [InlineData("/token")]
    public async Task OtherPaths_AreNotProtocolCors(string path) =>
        Assert.Null(await ResolveAsync(path));

    [Fact]
    public async Task Origins_AreCachedPerTenant()
    {
        await ResolveAsync("/tenants/acme/token");
        await ResolveAsync("/tenants/acme/userinfo");

        Assert.Equal(1, _host.Directory.CorsOriginLookups);
    }
}
