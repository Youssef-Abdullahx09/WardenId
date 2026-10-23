namespace WardenId.SharedKernel.Domain;

/// <summary>
/// An entity whose row outlives its deletion (to be restored, or to keep its unique name taken).
/// The entity's own domain method sets these; removing it from its DbSet throws instead.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }
    DateTimeOffset? DeletedAt { get; }
}
