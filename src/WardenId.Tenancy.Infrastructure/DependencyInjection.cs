using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WardenId.SharedKernel.Persistence;
using WardenId.Tenancy.Application.Abstractions;
using WardenId.Tenancy.Infrastructure.Persistence;

namespace WardenId.Tenancy.Infrastructure;

public static class DependencyInjection
{
    public static void AddTenancyInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDataProtection()
            .PersistKeysToDbContext<TenancyDbContext>()
            .SetApplicationName("WardenId");

        services.AddScoped<ISigningKeyGenerator, Es256SigningKeyGenerator>();
        services.AddTenancyPersistence(configuration);
    }
    
    public static void AddTenancyPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ITenancyDbContext, TenancyDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString(AppConfigurationConstants.DbConnectionStringKey) ??
                throw new InvalidOperationException(AppConfigurationConstants.NotFoundConnectionStringMessage),
                postgresSql =>
                    postgresSql.MigrationsHistoryTable("__EFMigrationsHistory", TenancyDbContext.Schema));
            options.AddInterceptors(serviceProvider.GetRequiredService<DomainEventsInterceptor>());
        });

        MigrateAsync(services).GetAwaiter().GetResult();
    }

    private static async Task MigrateAsync(IServiceCollection services)
    {
        var context = services.BuildServiceProvider().GetRequiredService<TenancyDbContext>();
        await context.Database.MigrateAsync(CancellationToken.None);
    }
}