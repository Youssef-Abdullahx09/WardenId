namespace WardenId.SharedKernel.Domain;

public abstract class Entity : IEntity
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
}
