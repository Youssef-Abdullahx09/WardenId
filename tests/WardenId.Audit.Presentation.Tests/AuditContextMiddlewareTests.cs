using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.JsonWebTokens;
using WardenId.SharedKernel.Application.Utilities;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Audit.Presentation.Tests;

public class AuditContextMiddlewareTests
{
    private static HttpContext MakeHttpContext(ClaimsPrincipal? principal = null, string? ip = null, string? userAgent = null)
    {
        var context = new DefaultHttpContext();
        if (principal is not null) context.User = principal;
        if (ip is not null) context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        if (userAgent is not null) context.Request.Headers.UserAgent = userAgent;
        return context;
    }

    [Fact]
    public async Task NoClaims_ProducesAnonymousPlatformActor()
    {
        var accessor = new AsyncLocalAuditContextAccessor();
        AuditAmbientContext? seen = null;
        var middleware = new AuditContextMiddleware(_ =>
        {
            seen = accessor.Current;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(MakeHttpContext(), accessor);

        Assert.NotNull(seen);
        Assert.Equal(ActorType.Anonymous, seen!.Actor.Type);
        Assert.Null(seen.TenantId);
    }

    [Fact]
    public async Task SubjectClaim_ProducesAuthenticatedUnknownActor()
    {
        var accessor = new AsyncLocalAuditContextAccessor();
        var subject = Guid.NewGuid().ToString();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(JwtRegisteredClaimNames.Sub, subject)]));
        AuditAmbientContext? seen = null;
        var middleware = new AuditContextMiddleware(_ =>
        {
            seen = accessor.Current;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(MakeHttpContext(principal), accessor);

        Assert.Equal(ActorType.AuthenticatedUnknown, seen!.Actor.Type);
        Assert.Equal(subject, seen.Actor.Id);
        Assert.True(seen.Actor.Scope.IsPlatform);
        Assert.Null(seen.TenantId);
    }

    [Fact]
    public async Task TenantIdClaim_ProducesTenantScopedContext()
    {
        var accessor = new AsyncLocalAuditContextAccessor();
        var tenantId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new Claim(CustomClaimTypes.TenantId, tenantId.ToString())
        ]));
        AuditAmbientContext? seen = null;
        var middleware = new AuditContextMiddleware(_ =>
        {
            seen = accessor.Current;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(MakeHttpContext(principal), accessor);

        Assert.Equal(tenantId, seen!.TenantId);
        Assert.False(seen.Actor.Scope.IsPlatform);
        Assert.Equal(tenantId, seen.Actor.Scope.TenantId);
    }

    [Fact]
    public async Task ClientContext_CapturesIpAndUserAgent()
    {
        var accessor = new AsyncLocalAuditContextAccessor();
        AuditAmbientContext? seen = null;
        var middleware = new AuditContextMiddleware(_ =>
        {
            seen = accessor.Current;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(MakeHttpContext(ip: "203.0.113.7", userAgent: "curl/8.0"), accessor);

        Assert.Equal("203.0.113.7", seen!.Client.IpAddress);
        Assert.Equal("curl/8.0", seen.Client.UserAgent);
    }

    [Fact]
    public async Task Scope_IsRestored_AfterMiddlewareCompletes()
    {
        var accessor = new AsyncLocalAuditContextAccessor();
        var middleware = new AuditContextMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(MakeHttpContext(), accessor);

        Assert.Null(accessor.Current);
    }
}
