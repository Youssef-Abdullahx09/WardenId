using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using WardenId.SharedKernel.Persistence;

namespace WardenId.Audit.Infrastructure.Persistence;

// Used by `dotnet ef` tooling only.
internal sealed class AuditDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), AppConfigurationConstants.PathToGateway))
            .AddJsonFile(AppConfigurationConstants.AppSettingFileName)
            .Build();

        var connectionString = configuration.GetConnectionString(AppConfigurationConstants.DbConnectionStringKey)
            ?? throw new InvalidOperationException(AppConfigurationConstants.NotFoundConnectionStringMessage);

        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new AuditDbContext(options);
    }
}
