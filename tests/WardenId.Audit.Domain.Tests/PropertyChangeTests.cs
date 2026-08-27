namespace WardenId.Audit.Domain.Tests;

public class PropertyChangeTests
{
    [Fact]
    public void Create_WithEmptyPropertyName_Fails()
    {
        var result = PropertyChange.Create("", new AuditValue.Plain("a"), new AuditValue.Plain("b"));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public void Create_WithPlainValues_Succeeds()
    {
        var result = PropertyChange.Create("Status", new AuditValue.Plain("Active"), new AuditValue.Plain("Suspended"));

        Assert.True(result.IsSuccess);
        Assert.IsType<AuditValue.Plain>(result.Value.OldValue);
    }

    [Fact]
    public void Create_WithRedactedNewValue_NeverExposesRawValue()
    {
        var protectedValue = ProtectedValue.Redact("new-client-secret");
        var result = PropertyChange.Create("ClientSecret", null, new AuditValue.Redacted(protectedValue));

        Assert.True(result.IsSuccess);
        var redacted = Assert.IsType<AuditValue.Redacted>(result.Value.NewValue);
        Assert.Equal(protectedValue.Hash, redacted.Value.Hash);
    }
}
