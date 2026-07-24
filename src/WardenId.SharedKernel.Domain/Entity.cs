namespace WardenId.SharedKernel.Domain;

public abstract class Entity : IEntity
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    
    public Guid CreatedById { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    
    public Guid? LastModificationById { get; private set; }
    public DateTimeOffset? LastModificationAt { get; private set; }
    
    public Guid? DeletedById { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public bool IsDeleted { get; private set; }

    public void MarkAsCreated(Guid userId)
    {
        CreatedById = userId;
        CreatedAt = DateTime.Now;
    }

    public void MarkAsModified(Guid userId)
    {
        LastModificationById = userId;
        LastModificationAt = DateTime.Now;
    }

    public void MarkAsDeleted(Guid userId)
    {
        IsDeleted = true;
        DeletedById = userId;
        DeletedAt = DateTime.Now;
    }
}