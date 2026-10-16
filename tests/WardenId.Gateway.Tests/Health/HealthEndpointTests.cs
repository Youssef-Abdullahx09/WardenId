using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using WardenId.Gateway.Health;

namespace WardenId.Gateway.Tests.Health;

public class HealthEndpointTests
{
    private static async Task<WebApplication> StartAsync(HealthStatus readyStatus)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddHealthChecks()
            .AddCheck("database", () => new HealthCheckResult(readyStatus, "secret detail"), [HealthCheckTags.Ready]);
        // Every endpoint requires a signed-in caller unless it opts out: the health endpoints must opt out.
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGatewayHealthChecks();
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Live_Returns200_WithoutCallingDependencyChecks()
    {
        await using var app = await StartAsync(HealthStatus.Unhealthy);

        var response = await app.GetTestClient().GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(0, body.RootElement.GetProperty("checks").GetArrayLength());
    }

    [Fact]
    public async Task Ready_Returns200_WhenEveryReadyCheckIsHealthy()
    {
        await using var app = await StartAsync(HealthStatus.Healthy);

        var response = await app.GetTestClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
        var check = Assert.Single(body.RootElement.GetProperty("checks").EnumerateArray());
        Assert.Equal("database", check.GetProperty("name").GetString());
        Assert.Equal("Healthy", check.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Ready_Returns503_WhenAReadyCheckIsUnhealthy_WithoutLeakingCheckDetail()
    {
        await using var app = await StartAsync(HealthStatus.Unhealthy);

        var response = await app.GetTestClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(content);
        Assert.Equal("Unhealthy", body.RootElement.GetProperty("status").GetString());
        Assert.DoesNotContain("secret detail", content);
    }

    [Fact]
    public async Task Ready_Returns200_WhenAReadyCheckIsOnlyDegraded()
    {
        await using var app = await StartAsync(HealthStatus.Degraded);

        var response = await app.GetTestClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Degraded", body.RootElement.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoints_AreAnonymous_AndExcludedFromOpenApi(string path)
    {
        await using var app = await StartAsync(HealthStatus.Healthy);

        var endpoint = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(e => e.RoutePattern.RawText == path);

        Assert.NotNull(endpoint.Metadata.GetMetadata<IAllowAnonymous>());
        Assert.True(endpoint.Metadata.GetMetadata<IExcludeFromDescriptionMetadata>()?.ExcludeFromDescription);
    }
}
