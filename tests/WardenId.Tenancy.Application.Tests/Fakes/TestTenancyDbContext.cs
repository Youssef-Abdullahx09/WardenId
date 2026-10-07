using Microsoft.EntityFrameworkCore;

namespace WardenId.Tenancy.Application.Tests.Fakes;

public sealed class TestTenancyDbContext(DbContextOptions<TestTenancyDbContext> options)
    : DbContext(options), ITenancyDbContext
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public Task<int> SaveChangesAsync(Guid userId, CancellationToken cancellationToken) =>
        SaveChangesAsync(cancellationToken);

    public Task BeginTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task CommitTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RollbackTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(builder =>
        {
            builder.Ignore(t => t.DomainEvents);

            builder.Property(t => t.Slug)
                .HasConversion(slug => slug.Value, value => Slug.FromTrustedSource(value));

            builder.HasMany(t => t.SigningKeys).WithOne().OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(t => t.SigningKeys).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }

    public static TestTenancyDbContext CreateNew(string databaseName) =>
        new(new DbContextOptionsBuilder<TestTenancyDbContext>().UseInMemoryDatabase(databaseName).Options);
}
