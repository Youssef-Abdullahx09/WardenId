using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WardenId.SharedKernel.Persistence;
using WardenId.SharedKernel.Persistence.Utilities;
using WardenId.Tenancy.Application.Abstractions;
using WardenId.Tenancy.Infrastructure.Persistence;
using WardenId.Tenancy.Infrastructure.Utilities;

namespace WardenId.Tenancy.Infrastructure;

public static class DependencyInjection
{
    public static void AddTenancyInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDataProtection()
            .PersistKeysToDbContext<TenancyDbContext>()
            .SetApplicationName(DataProtectionSettings.ApplicationName);

        services.AddScoped<ISigningKeyGenerator, Es256SigningKeyGenerator>();
        services.AddTenancyPersistence(configuration);
    }
    
    private static void AddTenancyPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ITenancyDbContext, TenancyDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString(AppConfigurationConstants.DbConnectionStringKey) ??
                throw new InvalidOperationException(AppConfigurationConstants.NotFoundConnectionStringMessage),
                postgresSql =>
                    postgresSql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, TenancyDbContext.Schema));
            options.AddSharedInterceptors(serviceProvider);
        });

        services.AddScoped<ITenantRepository, TenantRepository>();
        MigrateAsync(services).GetAwaiter().GetResult();
    }

    private static async Task MigrateAsync(IServiceCollection services)
    {
        var context = services.BuildServiceProvider().GetRequiredService<TenancyDbContext>();
        await context.Database.MigrateAsync(CancellationToken.None);
    }
}