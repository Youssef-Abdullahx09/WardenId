namespace WardenId.SharedKernel.Domain;

public interface IAudit
{
    Guid CreatedById { get; }
    DateTimeOffset CreatedAt { get; }
    
    Guid? LastModificationById { get; }
    DateTimeOffset? LastModificationAt { get; }
    
    public Guid? DeletedById { get; }
    public DateTime? DeletedAt { get; }
    public bool IsDeleted { get; }
    public bool IsActive { get; }
}