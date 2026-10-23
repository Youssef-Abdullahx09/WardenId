using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using WardenId.SharedKernel.Persistence.Utilities;

namespace WardenId.Protocol.Infrastructure.Persistence;

// Used by `dotnet ef` tooling only.
internal sealed class ProtocolDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ProtocolDbContext>
{
    public ProtocolDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), AppConfigurationConstants.PathToGateway))
            .AddJsonFile(AppConfigurationConstants.AppSettingFileName)
            .Build();

        var connectionString = configuration.GetConnectionString(AppConfigurationConstants.DbConnectionStringKey)
            ?? throw new InvalidOperationException(AppConfigurationConstants.NotFoundConnectionStringMessage);

        var options = new DbContextOptionsBuilder<ProtocolDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, ProtocolDbContext.Schema))
            .Options;

        return new ProtocolDbContext(options);
    }
}
