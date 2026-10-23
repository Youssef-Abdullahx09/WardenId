using Microsoft.EntityFrameworkCore;
using WardenId.SharedKernel.Domain;

namespace WardenId.SharedKernel.Persistence.Utilities;

public static class ModelBuilderExtensions
{
    public static void ApplyIndexOnEntityIsDeleted(this ModelBuilder builder)
    {
        var entityTypes = builder.Model.GetEntityTypes()
            .Where(x => typeof(ISoftDeletable).IsAssignableFrom(x.ClrType));

        foreach (var entityType in entityTypes)
            builder.Entity(entityType.ClrType).HasIndex(nameof(ISoftDeletable.IsDeleted));
    }

    // Every Id is assigned client-side (Entity.Id = Guid.CreateVersion7()) before the entity
    // is ever tracked. Without this, EF's default "Guid key = store-generated" convention
    // sees a non-default key value on an entity that's only reachable via navigation fixup
    // (never an explicit DbSet.Add()) — e.g. a new child added to a tracked aggregate's
    // collection — and infers it must already exist, tracking it as Modified instead of
    // Added. That turns its INSERT into a no-op UPDATE, throwing a false-positive
    // DbUpdateConcurrencyException.
    public static void ApplyValueGeneratedNeverOnId(this ModelBuilder builder)
    {
        var entityTypes = builder.Model.GetEntityTypes()
            .Where(x => typeof(IEntity).IsAssignableFrom(x.ClrType));

        foreach (var entityType in entityTypes)
            builder.Entity(entityType.ClrType).Property(nameof(IEntity.Id)).ValueGeneratedNever();
    }
}