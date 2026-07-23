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
    
    public bool IsActive { get; private set; } = true;

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

    public void MarkAsActivate(Guid modifiedById)
    {
        IsActive = true;
        LastModificationById = modifiedById;
        LastModificationAt = DateTime.Now;
    }

    public void MarkAsInActive(Guid modifiedById)
    {
        IsActive = false;
        LastModificationById = modifiedById;
        LastModificationAt = DateTime.Now;
    }

    public void InitiateAsActive()
    {
        IsActive = true;
    }

    protected void InitiateAsInActive()
    {
        IsActive = false;
    }
}