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
        // Mirrors DbContextBase.ApplyValueGeneratedNeverOnId: ids are set at construction, so a new secret
        // added to a tracked client must be inserted, not treated as an existing row.
        modelBuilder.Entity<ClientSecret>().Property(s => s.Id).ValueGeneratedNever();

        modelBuilder.Entity<Client>(builder =>
        {
            builder.Property(c => c.Id).ValueGeneratedNever();
            builder.Ignore(c => c.DomainEvents);
            builder.Property(c => c.ClientId)
                .HasConversion(id => id.Value, value => ClientId.FromTrustedSource(value));
            builder.Property(c => c.BackchannelLogoutUri)
                .HasConversion(uri => uri!.Value, value => BackchannelLogoutUri.FromTrustedSource(value));
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

            builder.OwnsMany(c => c.PostLogoutRedirectUris, b =>
            {
                b.WithOwner().HasForeignKey("OwnerId");
                b.Property(r => r.Value);
                b.HasKey("OwnerId", "Value");
            });
            builder.Navigation(c => c.PostLogoutRedirectUris).UsePropertyAccessMode(PropertyAccessMode.Field);

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
