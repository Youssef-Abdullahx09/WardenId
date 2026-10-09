using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WardenId.Protocol.Application;
using WardenId.Protocol.Infrastructure;
using WardenId.Protocol.Infrastructure.OpenIddict;
using WardenId.SharedKernel.Presentation;
using ConfigurationKeys = WardenId.Protocol.Infrastructure.ConfigurationKeys;

namespace WardenId.Protocol.Presentation.Tests;

public sealed class TokenTestHost : IAsyncDisposable
{
    public const string IssuerBase = "https://wardenid.test";

    private TokenTestHost(WebApplication app, FakeDirectory directory)
    {
        App = app;
        Directory = directory;
        Client = app.GetTestClient();
    }

    public WebApplication App { get; }
    public FakeDirectory Directory { get; }
    public HttpClient Client { get; }

    public static async Task<TokenTestHost> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ProtocolToken:IssuerBaseUri"] = IssuerBase,
            [ConfigurationKeys.ProtocolOpenIddict.DisableTransportSecurityRequirement] = "true"
        });

        var directory = new FakeDirectory();
        builder.Services.AddSingleton(directory);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();

        var databaseName = "openiddict-" + Guid.NewGuid();
        builder.Services.AddDbContext<OpenIddictDbContext>(options =>
        {
            options.UseInMemoryDatabase(databaseName);
            options.UseOpenIddict();
        });

        builder.Services.AddSharedKernelPresentation();
        builder.Services.AddProtocolApplication(builder.Configuration);
        builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(TokenTestHost).Assembly));
        builder.Services.AddSingleton<ITenantSigningCredentialsFactory, TenantSigningCredentialsFactory>();
        builder.Services.AddProtocolOpenIddict(builder.Configuration);

        builder.Services.AddCors();

        var app = builder.Build();
        app.UseCors();
        app.UseAuthentication();
        new Features.Token.Endpoint().MapEndpoint(app);
        new Features.Discovery.Get.Endpoint().MapEndpoint(app);

        await app.StartAsync();
        return new TokenTestHost(app, directory);
    }

    public async ValueTask DisposeAsync() => await App.DisposeAsync();
}
