using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Protocol.Infrastructure.Persistence;
using WardenId.SharedKernel.Persistence;

namespace WardenId.Protocol.Infrastructure;

public static class DependencyInjection
{
    public static void AddProtocolInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ProtocolDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString(AppConfigurationConstants.DbConnectionStringKey) ??
                throw new InvalidOperationException(AppConfigurationConstants.NotFoundConnectionStringMessage),
                postgresSql =>
                    postgresSql.MigrationsHistoryTable("__EFMigrationsHistory", ProtocolDbContext.Schema));
            options.AddSharedInterceptors(serviceProvider);
        });

        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IJwtIssuer, JwtIssuer>();

        MigrateAsync(services).GetAwaiter().GetResult();
    }

    private static async Task MigrateAsync(IServiceCollection services)
    {
        var context = services.BuildServiceProvider().GetRequiredService<ProtocolDbContext>();
        await context.Database.MigrateAsync(CancellationToken.None);
    }
}
