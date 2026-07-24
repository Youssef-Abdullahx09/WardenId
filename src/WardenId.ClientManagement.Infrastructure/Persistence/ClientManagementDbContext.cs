using Microsoft.EntityFrameworkCore;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Domain;
using WardenId.SharedKernel.Persistence;

namespace WardenId.ClientManagement.Infrastructure.Persistence;

public sealed class ClientManagementDbContext(
    DbContextOptions<ClientManagementDbContext> options) : DbContextBase(options), IClientManagementDbContext
{
    public const string Schema = "ClientManagement";

    public DbSet<Client> Clients => Set<Client>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ClientManagementDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
