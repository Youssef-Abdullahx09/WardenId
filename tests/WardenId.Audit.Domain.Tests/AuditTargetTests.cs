namespace WardenId.Audit.Domain.Tests;

public class AuditTargetTests
{
    [Fact]
    public void Create_WithEmptyId_Fails()
    {
        Assert.True(AuditTarget.Create(AuditTargetType.Tenant, "").IsFailed);
    }

    [Fact]
    public void Create_WithValidTypeAndId_Succeeds()
    {
        var result = AuditTarget.Create(AuditTargetType.Tenant, "tenant-1", AuditCreationSource.Dcr);

        Assert.True(result.IsSuccess);
        Assert.Equal(AuditCreationSource.Dcr, result.Value.CreatedVia);
    }

    [Fact]
    public void Create_WithoutCreatedVia_IsNull()
    {
        var result = AuditTarget.Create(AuditTargetType.Tenant, "tenant-1");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.CreatedVia);
    }
}
