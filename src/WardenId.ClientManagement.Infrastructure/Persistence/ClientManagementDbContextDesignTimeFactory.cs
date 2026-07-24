using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using WardenId.SharedKernel.Persistence;

namespace WardenId.ClientManagement.Infrastructure.Persistence;

// Used by `dotnet ef` tooling only.
internal sealed class ClientManagementDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ClientManagementDbContext>
{
    public ClientManagementDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), AppConfigurationConstants.PathToGateway))
            .AddJsonFile(AppConfigurationConstants.AppSettingFileName)
            .Build();

        var connectionString = configuration.GetConnectionString(AppConfigurationConstants.DbConnectionStringKey)
            ?? throw new InvalidOperationException(AppConfigurationConstants.NotFoundConnectionStringMessage);

        var options = new DbContextOptionsBuilder<ClientManagementDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new ClientManagementDbContext(options);
    }
}
