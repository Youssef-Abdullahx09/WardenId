using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;
using WardenId.Identity.Infrastructure.Persistence;
using WardenId.SharedKernel.Persistence;

namespace WardenId.Identity.Infrastructure;

public static class DependencyInjection
{
    private static readonly Uri HaveIBeenPwnedRangeApiBaseAddress = new("https://api.pwnedpasswords.com/");

    public static void AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<IIdentityDbContext, IdentityDbContext>((serviceProvider, options) =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString(AppConfigurationConstants.DbConnectionStringKey) ??
                throw new InvalidOperationException(AppConfigurationConstants.NotFoundConnectionStringMessage),
                postgresSql =>
                    postgresSql.MigrationsHistoryTable("__EFMigrationsHistory", IdentityDbContext.Schema));
            options.AddInterceptors(serviceProvider.GetRequiredService<DomainEventsInterceptor>());
        });

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddHttpClient<IBreachedPasswordChecker, HaveIBeenPwnedBreachedPasswordChecker>(client =>
        {
            client.BaseAddress = HaveIBeenPwnedRangeApiBaseAddress;
        });

        MigrateAsync(services).GetAwaiter().GetResult();
    }

    private static async Task MigrateAsync(IServiceCollection services)
    {
        var context = services.BuildServiceProvider().GetRequiredService<IdentityDbContext>();
        await context.Database.MigrateAsync(CancellationToken.None);
    }
}
