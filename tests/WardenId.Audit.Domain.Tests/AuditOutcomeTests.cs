namespace WardenId.Audit.Domain.Tests;

public class AuditOutcomeTests
{
    [Fact]
    public void Success_AllowsNoReason()
    {
        var outcome = AuditOutcome.Success();

        Assert.Equal(AuditResult.Success, outcome.Status);
        Assert.Null(outcome.Reason);
    }

    [Fact]
    public void Success_CanCarryAReasonToo()
    {
        // e.g. a successful action still worth annotating, not just failures.
        var outcome = AuditOutcome.Success(AuditOutcomeReason.PolicyDenied);

        Assert.Equal(AuditOutcomeReason.PolicyDenied, outcome.Reason);
    }

    [Fact]
    public void Failure_CarriesTheGivenReason()
    {
        var outcome = AuditOutcome.Failure(AuditOutcomeReason.InvalidGrant);

        Assert.Equal(AuditResult.Failure, outcome.Status);
        Assert.Equal(AuditOutcomeReason.InvalidGrant, outcome.Reason);
    }
}
