using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using WardenId.Protocol.Infrastructure.OpenIddict;
using WardenId.SharedKernel.Persistence;

namespace WardenId.Protocol.Infrastructure.Persistence;

// Used by `dotnet ef` tooling only.
internal sealed class OpenIddictDbContextDesignTimeFactory : IDesignTimeDbContextFactory<OpenIddictDbContext>
{
    public OpenIddictDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), AppConfigurationConstants.PathToGateway))
            .AddJsonFile(AppConfigurationConstants.AppSettingFileName)
            .Build();

        var connectionString = configuration.GetConnectionString(AppConfigurationConstants.DbConnectionStringKey)
            ?? throw new InvalidOperationException(AppConfigurationConstants.NotFoundConnectionStringMessage);

        var options = new DbContextOptionsBuilder<OpenIddictDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", OpenIddictDbContext.Schema))
            .UseOpenIddict()
            .Options;

        return new OpenIddictDbContext(options);
    }
}
