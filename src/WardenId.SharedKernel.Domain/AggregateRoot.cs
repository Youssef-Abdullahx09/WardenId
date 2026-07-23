namespace WardenId.SharedKernel.Domain;

public abstract class AggregateRoot : Entity, IAggregateRoot
{
    public List<DomainEvent> DomainEvents { get; private set; } = [];

    protected void AddDomainEvent(DomainEvent @event)
    {
        DomainEvents.Add(@event);
    }

    public List<DomainEvent> GetDomainEvents()
    {
        DomainEvents.ForEach(@event =>
        {
            var userId = LastModificationById ?? CreatedById;
            @event.SetUserId(userId);
            @event.SetAggregateId(Id);
        });
        return DomainEvents;
    }

    public void ClearDomainEvents()
    {
        DomainEvents.Clear();
    }
}