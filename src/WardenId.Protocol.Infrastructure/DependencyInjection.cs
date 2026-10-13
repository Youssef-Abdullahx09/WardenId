using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        MigrateAsync(services).GetAwaiter().GetResult();
    }

    private static async Task MigrateAsync(IServiceCollection services)
    {
        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<OpenIddictDbContext>().Database.MigrateAsync(CancellationToken.None);
    }
}
