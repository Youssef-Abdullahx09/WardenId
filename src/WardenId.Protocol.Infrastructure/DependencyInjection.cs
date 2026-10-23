using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Application;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Protocol.Infrastructure.Authentication;
using WardenId.Protocol.Infrastructure.BackchannelLogout;
using WardenId.Protocol.Infrastructure.OpenIddict;
using WardenId.Protocol.Infrastructure.Persistence;
using WardenId.Protocol.Infrastructure.Sessions;
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
                    postgresSql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, OpenIddictDbContext.Schema));
            options.UseOpenIddict();
        });

        services.AddDbContext<IProtocolDbContext, ProtocolDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString(AppConfigurationConstants.DbConnectionStringKey) ??
                throw new InvalidOperationException(AppConfigurationConstants.NotFoundConnectionStringMessage),
                postgresSql =>
                    postgresSql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, ProtocolDbContext.Schema));
            options.AddSharedInterceptors(serviceProvider);
        });

        services.AddProtocolOpenIddict(configuration);
        services.AddAccessTokenAuthentication();

        services.AddSingleton<ITenantSigningCredentialsFactory, TenantSigningCredentialsFactory>();

        // Production only (CAP + Postgres outbox); the Presentation test host never ends sessions asynchronously.
        services.AddProtocolBackchannelLogout(configuration);

        services.AddSingleton<IValidateOptions<ProtocolSessionOptions>, ProtocolSessionOptionsValidator>();
        services.AddOptions<ProtocolSessionOptions>().ValidateOnStart();

        // Production only: the test host calls AddProtocolOpenIddict directly and must not prune InMemory.
        services.Configure<ProtocolOpenIddictPruningOptions>(configuration.GetSection(ConfigurationKeys.ProtocolOpenIddict.Section));
        services.AddSingleton<IValidateOptions<ProtocolOpenIddictPruningOptions>, ProtocolOpenIddictPruningOptionsValidator>();
        services.AddOptions<ProtocolOpenIddictPruningOptions>().ValidateOnStart();
        services.AddScoped<IOpenIddictPruner, OpenIddictPruner>();
        services.AddHostedService<OpenIddictPruningService>();
        services.AddScoped<ISessionPruner, SessionPruner>();
        services.AddHostedService<SessionPruningService>();

        MigrateAsync(services).GetAwaiter().GetResult();
    }

    private static async Task MigrateAsync(IServiceCollection services)
    {
        var provider = services.BuildServiceProvider();
        // Order matters on a fresh database: OpenIddict's DropLegacyProtocolSchema migration drops schema
        // "Protocol", which ProtocolDbContext then recreates.
        await provider.GetRequiredService<OpenIddictDbContext>().Database.MigrateAsync(CancellationToken.None);
        await provider.GetRequiredService<ProtocolDbContext>().Database.MigrateAsync(CancellationToken.None);
    }
}
