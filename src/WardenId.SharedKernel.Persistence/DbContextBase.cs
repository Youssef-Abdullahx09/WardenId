using Microsoft.EntityFrameworkCore;
using WardenId.SharedKernel.Application.Abstractions;
using WardenId.SharedKernel.Domain;
using WardenId.SharedKernel.Persistence.Utilities;

namespace WardenId.SharedKernel.Persistence;

public abstract class DbContextBase(DbContextOptions options) : DbContext(options), IDbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyIndexOnEntityIsDeleted();
        modelBuilder.ApplyValueGeneratedNeverOnId();

        // Deliberately no ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly())
        // here: GetExecutingAssembly() resolves to THIS assembly (SharedKernel.Persistence),
        // not the derived DbContext's — so it would silently apply every config living here
        // (e.g. AuditEventConfiguration) to every module's context, not just the ones that
        // opt in. Each context applies its own configs explicitly instead.

        base.OnModelCreating(modelBuilder);
    }

    public async Task<int> SaveChangesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        CheckAndUpdateEntities();
        SavingActorId = userId;
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            SavingActorId = Guid.Empty;
        }
    }

    /// <summary>The user saving right now, read by DomainEventsInterceptor as the actor of the events it publishes.</summary>
    internal Guid SavingActorId { get; private set; }

    public Task BeginTransactionAsync(CancellationToken cancellationToken) =>
        Database.BeginTransactionAsync(cancellationToken);

    public Task CommitTransactionAsync(CancellationToken cancellationToken) =>
        Database.CommitTransactionAsync(cancellationToken);

    public Task RollbackTransactionAsync(CancellationToken cancellationToken) =>
        Database.RollbackTransactionAsync(cancellationToken);

    private void CheckAndUpdateEntities()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<ICreationTracked>().Where(e => e.State == EntityState.Added))
            entry.Property(nameof(ICreationTracked.CreatedAt)).CurrentValue = now;

        var removed = ChangeTracker.Entries<ISoftDeletable>().FirstOrDefault(e => e.State == EntityState.Deleted);
        if (removed is not null)
            throw new InvalidOperationException(
                $"{removed.Metadata.ClrType.Name} is soft-deletable; delete it through its domain method.");
    }
}
