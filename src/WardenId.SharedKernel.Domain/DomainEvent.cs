namespace WardenId.SharedKernel.Domain;

public abstract record DomainEvent()
{
    protected DomainEvent(Guid? transactionId) : this()
    {
        TransactionId = transactionId;
    }

    public Guid EventId { get; } = Guid.NewGuid();

    public DateTimeOffset OccurredOnUtc { get; } = DateTime.UtcNow;
    public Guid UserId { get; private set; }
    public Guid? TransactionId { get; }
    public Guid AggregateId { get; private set; }

    public void SetUserId(Guid userId)
    {
        UserId = userId;
    }

    public void SetAggregateId(Guid aggregateId)
    {
        AggregateId = aggregateId;
    }
}