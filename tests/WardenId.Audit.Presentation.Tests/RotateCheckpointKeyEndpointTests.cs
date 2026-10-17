using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Asp.Versioning;
using FluentResults;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WardenId.Audit.Application.Abstractions;
using RotateEndpoint = WardenId.Audit.Presentation.Features.CheckpointKeys.Rotate.Endpoint;

namespace WardenId.Audit.Presentation.Tests;

public class RotateCheckpointKeyEndpointTests
{
    private const string Path = "/api/v1/audit/checkpoint-keys/rotate";

    private sealed class FakeRotator(Result<CheckpointKeyRotationResult> result) : IAuditCheckpointKeyRotator
    {
        public AuditEvent? Recorded { get; private set; }

        public Task<Result<CheckpointKeyRotationResult>> RotateAsync(
            Func<string?, string, AuditEvent> buildAuditEvent, CancellationToken cancellationToken = default)
        {
            if (result.IsSuccess) Recorded = buildAuditEvent(result.Value.PreviousKid, result.Value.Kid);
            return Task.FromResult(result);
        }
    }

    private static async Task<WebApplication> StartAsync(FakeRotator rotator)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddApiVersioning(options => options.ApiVersionReader = new UrlSegmentApiVersionReader());
        builder.Services.AddAuditApplication();
        builder.Services.AddSingleton<IAuditContextAccessor, AsyncLocalAuditContextAccessor>();
        builder.Services.AddSingleton<IAuditCheckpointKeyRotator>(rotator);
        builder.Services.AddAuthentication(TestAuthenticationHandler.Scheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.Scheme, null);
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<AuditContextMiddleware>();
        new RotateEndpoint().MapEndpoint(app);
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Post_ReturnsTheNewKid_AndRecordsTheSignedInAdminAsActor()
    {
        var rotator = new FakeRotator(Result.Ok(new CheckpointKeyRotationResult("new-kid", "old-kid")));
        await using var app = await StartAsync(rotator);
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.Header, "admin-42");

        var response = await client.PostAsync(Path, null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("new-kid", body.RootElement.GetProperty("kid").GetString());
        Assert.Equal("admin-42", rotator.Recorded!.Actor.Id);
    }

    [Fact]
    public async Task Post_Returns409_WhenAConcurrentRotationWon()
    {
        await using var app = await StartAsync(new FakeRotator(Result.Fail(AuditErrors.CheckpointKeyRotationConflict())));
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthenticationHandler.Header, "admin-42");

        var response = await client.PostAsync(Path, null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Post_Returns401_WithoutAnAuthenticatedCaller()
    {
        await using var app = await StartAsync(new FakeRotator(Result.Ok(new CheckpointKeyRotationResult("new-kid", null))));

        var response = await app.GetTestClient().PostAsync(Path, null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string Scheme = "Test";
        public const string Header = "X-Test-User";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(Header, out var user))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity([new Claim("sub", user.ToString())], Scheme);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme)));
        }
    }
}
