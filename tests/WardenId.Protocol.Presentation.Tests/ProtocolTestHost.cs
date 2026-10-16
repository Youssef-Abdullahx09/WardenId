using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.EntityFrameworkCore;
using WardenId.Protocol.Application;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Protocol.Application.Features.BackchannelLogout;
using WardenId.Protocol.Infrastructure.Authentication;
using WardenId.Protocol.Domain.Sessions;
using WardenId.Protocol.Infrastructure;
using WardenId.Protocol.Infrastructure.OpenIddict;
using WardenId.Protocol.Infrastructure.Persistence;
using WardenId.Protocol.Infrastructure.Sessions;
using WardenId.SharedKernel.Presentation;
using ConfigurationKeys = WardenId.Protocol.Infrastructure.ConfigurationKeys;

namespace WardenId.Protocol.Presentation.Tests;

public sealed class ProtocolTestHost : IAsyncDisposable
{
    public const string IssuerBase = "https://wardenid.test";
    public const string AccessTokenProbePath = "/test/access-token";

    private ProtocolTestHost(WebApplication app, FakeDirectory directory)
    {
        App = app;
        Directory = directory;
        Client = app.GetTestClient();
    }

    public WebApplication App { get; }
    public FakeDirectory Directory { get; }
    public HttpClient Client { get; }

    /// <summary>A cookie-carrying "browser" over https (the session cookie is Secure).</summary>
    public BrowserClient CreateBrowser() => new(App.GetTestServer());

    public static async Task<ProtocolTestHost> StartAsync(
        TimeProvider? timeProvider = null, IDictionary<string, string?>? settings = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        var configuration = new Dictionary<string, string?>
        {
            ["ProtocolToken:IssuerBaseUri"] = IssuerBase,
            [ConfigurationKeys.ProtocolOpenIddict.DisableTransportSecurityRequirement] = "true"
        };
        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
            configuration[key] = value;
        builder.Configuration.AddInMemoryCollection(configuration);

        // Before AddProtocolPresentation (TryAdd) — OpenIddict and the grant factory share this clock.
        if (timeProvider is not null)
            builder.Services.AddSingleton(timeProvider);

        var directory = new FakeDirectory();
        builder.Services.AddSingleton(directory);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();

        var databaseName = "openiddict-" + Guid.NewGuid();
        builder.Services.AddDbContext<OpenIddictDbContext>(options =>
        {
            options.UseInMemoryDatabase(databaseName);
            options.UseOpenIddict();
        });

        // OpenIddict revokes token families with ExecuteUpdate, which EF InMemory doesn't support.
        builder.Services.Configure<OpenIddictEntityFrameworkCoreOptions>(options => options.DisableBulkOperations = true);

        // The real session handlers (Protocol.Application) run against InMemory; no shared interceptors,
        // so domain events stay on the aggregate (no CAP in tests).
        var sessionDatabaseName = "protocol-" + Guid.NewGuid();
        builder.Services.AddDbContext<ProtocolDbContext>(options => options.UseInMemoryDatabase(sessionDatabaseName));
        builder.Services.AddScoped<IProtocolDbContext>(sp => sp.GetRequiredService<ProtocolDbContext>());
        // Production registers it with back-channel logout (CAP); the admin grant handlers need it here too.
        builder.Services.AddScoped<ISessionAuthorizationRevoker, OpenIddictSessionAuthorizationRevoker>();

        builder.Services.AddSharedKernelPresentation();
        builder.Services.AddProtocolPresentation(builder.Configuration);
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ProtocolTestHost).Assembly));
        builder.Services.AddSingleton<ITenantSigningCredentialsFactory, TenantSigningCredentialsFactory>();
        builder.Services.AddProtocolOpenIddict(builder.Configuration);
        builder.Services.AddAccessTokenAuthentication();
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        new Features.Token.Endpoint().MapEndpoint(app);
        new Features.Discovery.Get.Endpoint().MapEndpoint(app);
        new Features.Authorize.Endpoint().MapEndpoint(app);
        new Features.UserInfo.Endpoint().MapEndpoint(app);
        new Features.EndSession.Endpoint().MapEndpoint(app);
        app.MapRazorPages();
        // Stands in for a REST endpoint protected by WardenId access tokens.
        app.MapGet(AccessTokenProbePath, (ClaimsPrincipal user) => Results.Ok(new
            {
                sub = user.FindFirstValue("sub"),
                tenant_id = user.FindFirstValue("tenant_id"),
            }))
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(AccessTokenAuthenticationDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser());

        await app.StartAsync();
        return new ProtocolTestHost(app, directory);
    }

    /// <summary>Sends through the app's mediator in a fresh scope (real Protocol handlers, fake directory).</summary>
    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        using var scope = App.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    public async Task<Session?> FindSessionAsync(Guid sid)
    {
        using var scope = App.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ProtocolDbContext>().Sessions
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == sid);
    }

    public async Task<IReadOnlyList<Session>> SessionsOfAsync(Guid userId)
    {
        using var scope = App.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ProtocolDbContext>().Sessions
            .AsNoTracking()
            .Where(s => s.SubjectId == userId)
            .ToListAsync();
    }

    public async ValueTask DisposeAsync() => await App.DisposeAsync();
}
