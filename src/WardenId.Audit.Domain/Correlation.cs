namespace WardenId.Audit.Domain;

public sealed record Correlation
{
    public string? RequestId { get; }
    public string? TraceId { get; }
    public Guid? TransactionId { get; }

    private Correlation(string? requestId, string? traceId, Guid? transactionId)
    {
        RequestId = requestId;
        TraceId = traceId;
        TransactionId = transactionId;
    }

    public static Correlation Create(string? requestId = null, string? traceId = null, Guid? transactionId = null)
        => new(requestId, traceId, transactionId);

    public static Correlation None() => new(null, null, null);
}
