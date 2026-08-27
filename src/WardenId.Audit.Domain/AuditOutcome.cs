namespace WardenId.Audit.Domain;

public sealed record AuditOutcome
{
    public AuditResult Status { get; }
    public AuditOutcomeReason? Reason { get; }

    private AuditOutcome(AuditResult status, AuditOutcomeReason? reason)
    {
        Status = status;
        Reason = reason;
    }

    public static AuditOutcome Success(AuditOutcomeReason? reason = null) => new(AuditResult.Success, reason);

    public static AuditOutcome Failure(AuditOutcomeReason reason) => new(AuditResult.Failure, reason);
}
