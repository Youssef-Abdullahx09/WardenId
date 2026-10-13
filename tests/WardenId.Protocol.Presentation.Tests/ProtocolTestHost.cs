using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.EntityFrameworkCore;
using WardenId.Protocol.Application;
using WardenId.Protocol.Infrastructure;
using WardenId.Protocol.Infrastructure.OpenIddict;
using WardenId.SharedKernel.Presentation;
using ConfigurationKeys = WardenId.Protocol.Infrastructure.ConfigurationKeys;

namespace WardenId.Protocol.Presentation.Tests;

public sealed class ProtocolTestHost : IAsyncDisposable
{
    public const string IssuerBase = "https://wardenid.test";

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

        builder.Services.AddSharedKernelPresentation();
        builder.Services.AddProtocolPresentation(builder.Configuration);
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ProtocolTestHost).Assembly));
        builder.Services.AddSingleton<ITenantSigningCredentialsFactory, TenantSigningCredentialsFactory>();
        builder.Services.AddProtocolOpenIddict(builder.Configuration);

        var app = builder.Build();
        app.UseCors();
        app.UseAuthentication();
        new Features.Token.Endpoint().MapEndpoint(app);
        new Features.Discovery.Get.Endpoint().MapEndpoint(app);
        new Features.Authorize.Endpoint().MapEndpoint(app);
        new Features.UserInfo.Endpoint().MapEndpoint(app);
        app.MapRazorPages();

        await app.StartAsync();
        return new ProtocolTestHost(app, directory);
    }

    public async ValueTask DisposeAsync() => await App.DisposeAsync();
}
