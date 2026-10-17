using System.Net;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WardenId.Gateway.Cors;
using WardenId.Protocol.Presentation;
using WardenId.Protocol.Presentation.Cors;
using ListAllowedOriginsQuery = WardenId.Protocol.Application.Features.Cors.ListAllowedOrigins.Query;

namespace WardenId.Gateway.Tests.Cors;

/// <summary>W17b: the Gateway's CORS rules, preflight and actual requests, through the real policy provider.</summary>
public class GatewayCorsTests
{
    private const string Spa = "https://spa.example";
    private const string Evil = "https://evil.example";
    private const string Gatehouse = "https://gatehouse.example";

    private static async Task<WebApplication> StartAsync(params string[] adminOrigins)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        var settings = new Dictionary<string, string?>();
        for (var i = 0; i < adminOrigins.Length; i++)
            settings[$"{GatewayCorsOptions.Section}:{nameof(GatewayCorsOptions.AllowedAdminOrigins)}:{i}"] = adminOrigins[i];
        builder.Configuration.AddInMemoryCollection(settings);

        builder.Services.AddSingleton<ISender>(new OriginsSender(new Dictionary<string, string[]> { ["acme"] = [Spa] }));
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<IProtocolCorsPolicyResolver, ProtocolCorsPolicyResolver>();
        builder.Services.AddCors(options => options.AddPolicy(
            CorsPolicyNames.ProtocolPublicMetadata, policy => policy.AllowAnyOrigin().WithMethods("GET").AllowAnyHeader()));
        builder.Services.AddGatewayCors(builder.Configuration);

        var app = builder.Build();
        app.UseCors();
        app.MapPost("/tenants/{slug}/token", () => Results.Ok());
        app.MapGet("/tenants/{slug}/userinfo", () => Results.Ok());
        app.MapGet("/tenants/{slug}/jwks", () => Results.Ok()).RequireCors(CorsPolicyNames.ProtocolPublicMetadata);
        app.MapGet("/api/v1/admin/tenants", () => Results.Ok());
        app.MapGet("/health/live", () => Results.Ok());
        await app.StartAsync();
        return app;
    }

    private static Task<HttpResponseMessage> PreflightAsync(WebApplication app, string path, string origin, string method = "POST")
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "content-type");
        return app.GetTestClient().SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendAsync(WebApplication app, HttpMethod method, string path, string origin)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("Origin", origin);
        return app.GetTestClient().SendAsync(request);
    }

    private static string? AllowedOrigin(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values) ? values.Single() : null;

    [Theory]
    [InlineData("/tenants/acme/token", "POST")]
    [InlineData("/tenants/acme/userinfo", "GET")]
    public async Task Preflight_FromAClientOriginOfTheTenant_IsAllowed(string path, string method)
    {
        await using var app = await StartAsync();

        var response = await PreflightAsync(app, path, Spa, method);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(Spa, AllowedOrigin(response));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task Preflight_FromAnUnknownOrigin_IsDenied()
    {
        await using var app = await StartAsync();

        var response = await PreflightAsync(app, "/tenants/acme/token", Evil);

        Assert.Null(AllowedOrigin(response));
    }

    // A client origin of acme means nothing at globex.
    [Fact]
    public async Task Preflight_ToAnotherTenant_IsDenied()
    {
        await using var app = await StartAsync();

        var response = await PreflightAsync(app, "/tenants/globex/token", Spa);

        Assert.Null(AllowedOrigin(response));
    }

    [Fact]
    public async Task ActualRequest_FromAClientOrigin_CarriesTheOrigin_AndFromOthersDoesNot()
    {
        await using var app = await StartAsync();

        Assert.Equal(Spa, AllowedOrigin(await SendAsync(app, HttpMethod.Post, "/tenants/acme/token", Spa)));
        Assert.Null(AllowedOrigin(await SendAsync(app, HttpMethod.Post, "/tenants/acme/token", Evil)));
    }

    [Fact]
    public async Task PublicMetadata_StaysAnyOrigin()
    {
        await using var app = await StartAsync();

        var response = await SendAsync(app, HttpMethod.Get, "/tenants/acme/jwks", Evil);

        Assert.Equal("*", AllowedOrigin(response));
    }

    [Fact]
    public async Task AdminApi_AllowsNoOrigin_ByDefault()
    {
        await using var app = await StartAsync();

        Assert.Null(AllowedOrigin(await PreflightAsync(app, "/api/v1/admin/tenants", Gatehouse, "GET")));
        Assert.Null(AllowedOrigin(await SendAsync(app, HttpMethod.Get, "/api/v1/admin/tenants", Gatehouse)));
    }

    [Fact]
    public async Task AdminApi_AllowsOnlyTheConfiguredOrigins()
    {
        await using var app = await StartAsync(Gatehouse);

        var allowed = await PreflightAsync(app, "/api/v1/admin/tenants", Gatehouse, "GET");
        var denied = await PreflightAsync(app, "/api/v1/admin/tenants", Evil, "GET");

        Assert.Equal(Gatehouse, AllowedOrigin(allowed));
        Assert.Null(AllowedOrigin(denied));
    }

    [Fact]
    public async Task OtherPaths_AllowNoOrigin()
    {
        await using var app = await StartAsync(Gatehouse);

        Assert.Null(AllowedOrigin(await SendAsync(app, HttpMethod.Get, "/health/live", Gatehouse)));
    }

    private sealed class OriginsSender(IReadOnlyDictionary<string, string[]> origins) : ISender
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            request is ListAllowedOriginsQuery query
                ? Task.FromResult((TResponse)(object)(IReadOnlyList<string>)(origins.TryGetValue(query.Slug, out var o) ? o : []))
                : throw new NotSupportedException(request.GetType().Name);

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest =>
            throw new NotSupportedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
