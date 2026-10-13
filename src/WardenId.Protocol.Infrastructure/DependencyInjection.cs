using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Infrastructure.OpenIddict;
using WardenId.SharedKernel.Persistence;

namespace WardenId.Protocol.Infrastructure;

public static class DependencyInjection
{
    public static void AddProtocolInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // No AddSharedInterceptors: OpenIddict rows aren't aggregates (no CAP outbox, no audit capture).
        services.AddDbContext<OpenIddictDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString(AppConfigurationConstants.DbConnectionStringKey) ??
                throw new InvalidOperationException(AppConfigurationConstants.NotFoundConnectionStringMessage),
                postgresSql =>
                    postgresSql.MigrationsHistoryTable("__EFMigrationsHistory", OpenIddictDbContext.Schema));
            options.UseOpenIddict();
        });

        services.AddProtocolOpenIddict(configuration);

        services.AddSingleton<ITenantSigningCredentialsFactory, TenantSigningCredentialsFactory>();

        // Production only: the test host calls AddProtocolOpenIddict directly and must not prune InMemory.
        services.Configure<ProtocolOpenIddictPruningOptions>(configuration.GetSection(ConfigurationKeys.ProtocolOpenIddict.Section));
        services.AddSingleton<IValidateOptions<ProtocolOpenIddictPruningOptions>, ProtocolOpenIddictPruningOptionsValidator>();
        services.AddOptions<ProtocolOpenIddictPruningOptions>().ValidateOnStart();
        services.AddScoped<IOpenIddictPruner, OpenIddictPruner>();
        services.AddHostedService<OpenIddictPruningService>();

        MigrateAsync(services).GetAwaiter().GetResult();
    }

    private static async Task MigrateAsync(IServiceCollection services)
    {
        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<OpenIddictDbContext>().Database.MigrateAsync(CancellationToken.None);
    }
}
