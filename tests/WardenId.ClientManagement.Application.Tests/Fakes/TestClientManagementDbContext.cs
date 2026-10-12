using Microsoft.EntityFrameworkCore;

namespace WardenId.ClientManagement.Application.Tests.Fakes;

public sealed class TestClientManagementDbContext(DbContextOptions<TestClientManagementDbContext> options)
    : DbContext(options), IClientManagementDbContext
{
    public DbSet<Client> Clients => Set<Client>();

    public Task<int> SaveChangesAsync(Guid userId, CancellationToken cancellationToken) =>
        SaveChangesAsync(cancellationToken);

    public Task BeginTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task CommitTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RollbackTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Client>(builder =>
        {
            builder.Ignore(c => c.DomainEvents);
            builder.Property(c => c.ClientId)
                .HasConversion(id => id.Value, value => ClientId.FromTrustedSource(value));
            builder.OwnsOne(c => c.TokenLifetimeOverrides, o =>
            {
                o.Property(t => t.AccessTokenSeconds);
                o.Property(t => t.IdTokenSeconds);
                o.Property(t => t.RefreshTokenSeconds);
            });

            builder.OwnsMany(c => c.RedirectUris, b =>
            {
                b.WithOwner().HasForeignKey("OwnerId");
                b.Property(r => r.Value);
                b.HasKey("OwnerId", "Value");
            });
            builder.Navigation(c => c.RedirectUris).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Ignore(c => c.PostLogoutRedirectUris);
            builder.Ignore(c => c.CorsOrigins);
            builder.Ignore(c => c.SigningKeys);
            builder.Ignore(c => c.AssignedScopeIds);

            builder.HasMany(c => c.Secrets).WithOne().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(c => c.Secrets).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }

    public static TestClientManagementDbContext CreateNew(string databaseName) =>
        new(new DbContextOptionsBuilder<TestClientManagementDbContext>().UseInMemoryDatabase(databaseName).Options);
}
