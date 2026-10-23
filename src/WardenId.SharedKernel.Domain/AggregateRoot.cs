namespace WardenId.SharedKernel.Domain;

public abstract class AggregateRoot : Entity, IAggregateRoot
{
    public List<DomainEvent> DomainEvents { get; private set; } = [];

    protected void RaiseDomainEvent(DomainEvent @event)
    {
        DomainEvents.Add(@event);
    }

    public List<DomainEvent> GetDomainEvents(Guid actorId)
    {
        DomainEvents.ForEach(@event =>
        {
            @event.SetUserId(actorId);
            @event.SetAggregateId(Id);
        });
        return DomainEvents;
    }

    public void ClearDomainEvents()
    {
        DomainEvents.Clear();
    }
}