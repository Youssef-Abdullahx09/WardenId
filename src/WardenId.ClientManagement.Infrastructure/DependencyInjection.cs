using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Infrastructure.Persistence;
using WardenId.SharedKernel.Persistence;

namespace WardenId.ClientManagement.Infrastructure;

public static class DependencyInjection
{
    public static void AddClientManagementInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<IClientManagementDbContext, ClientManagementDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString(AppConfigurationConstants.DbConnectionStringKey) ??
                throw new InvalidOperationException(AppConfigurationConstants.NotFoundConnectionStringMessage),
                postgresSql =>
                    postgresSql.MigrationsHistoryTable("__EFMigrationsHistory", ClientManagementDbContext.Schema));
            options.AddInterceptors(serviceProvider.GetRequiredService<DomainEventsInterceptor>());
        });

        services.AddScoped<IClientRepository, ClientRepository>();
        MigrateAsync(services).GetAwaiter().GetResult();
    }

    private static async Task MigrateAsync(IServiceCollection services)
    {
        var context = services.BuildServiceProvider().GetRequiredService<ClientManagementDbContext>();
        await context.Database.MigrateAsync(CancellationToken.None);
    }
}
