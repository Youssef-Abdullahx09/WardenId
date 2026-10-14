using Microsoft.EntityFrameworkCore;
using WardenId.Protocol.Domain.Sessions;

namespace WardenId.Protocol.Application.Tests.Fakes;

public sealed class TestProtocolDbContext(DbContextOptions<TestProtocolDbContext> options)
    : DbContext(options), IProtocolDbContext
{
    public DbSet<Session> Sessions => Set<Session>();

    public Task<int> SaveChangesAsync(Guid userId, CancellationToken cancellationToken) =>
        SaveChangesAsync(cancellationToken);

    public Task BeginTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task CommitTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RollbackTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Session>(builder =>
        {
            builder.Ignore(s => s.DomainEvents);
            builder.OwnsMany(s => s.Authorizations, b =>
            {
                b.WithOwner().HasForeignKey("SessionId");
                b.Property(a => a.AuthorizationId);
                b.Property(a => a.ClientId);
                b.Property(a => a.OfflineAccess);
                b.Property(a => a.CreatedAt);
                b.HasKey("SessionId", nameof(SessionAuthorization.AuthorizationId));
            });
            builder.Navigation(s => s.Authorizations).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }

    public static TestProtocolDbContext CreateNew(string databaseName) =>
        new(new DbContextOptionsBuilder<TestProtocolDbContext>().UseInMemoryDatabase(databaseName).Options);
}
