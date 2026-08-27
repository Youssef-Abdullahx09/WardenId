using System.Reflection;

namespace WardenId.Audit.Domain.Tests;

public class AuditOutcomeReasonTests
{
    private static IEnumerable<AuditOutcomeReason> DeclaredFields() =>
        typeof(AuditOutcomeReason)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(AuditOutcomeReason))
            .Select(f => (AuditOutcomeReason)f.GetValue(null)!);

    public static IEnumerable<object[]> AllCatalogEntries() => AuditOutcomeReason.All.Select(r => new object[] { r });

    [Theory]
    [MemberData(nameof(AllCatalogEntries))]
    public void EveryCatalogEntry_IsAcceptedByAuditOutcomeFailure(AuditOutcomeReason reason)
    {
        var outcome = AuditOutcome.Failure(reason);

        Assert.Equal(reason, outcome.Reason);
    }

    [Fact]
    public void NoTwoCatalogEntries_ShareTheSameKey()
    {
        var keys = AuditOutcomeReason.All.Select(r => r.Key).ToArray();

        Assert.Equal(keys.Length, keys.Distinct().Count());
    }

    [Fact]
    public void All_IsNotMissingAnyDeclaredStaticField()
    {
        var declared = DeclaredFields().Except(AuditOutcomeReason.All).ToArray();

        Assert.Empty(declared);
    }

    [Fact]
    public void FromKey_ResolvesByTaxonomyString()
    {
        Assert.Same(AuditOutcomeReason.InvalidGrant, AuditOutcomeReason.FromKey("invalid_grant"));
    }

    [Fact]
    public void FromKey_UnknownKey_ReturnsNull()
    {
        Assert.Null(AuditOutcomeReason.FromKey("no_such_reason"));
    }
}
