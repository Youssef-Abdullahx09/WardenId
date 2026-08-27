using System.Reflection;

namespace WardenId.Audit.Domain.Tests;

public class AuditTargetTypeTests
{
    private static IEnumerable<AuditTargetType> DeclaredFields() =>
        typeof(AuditTargetType)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(AuditTargetType))
            .Select(f => (AuditTargetType)f.GetValue(null)!);

    public static IEnumerable<object[]> AllCatalogEntries() => AuditTargetType.All.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(AllCatalogEntries))]
    public void EveryCatalogEntry_IsAcceptedByAuditTargetCreate(AuditTargetType targetType)
    {
        var result = AuditTarget.Create(targetType, "some-id");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void NoTwoCatalogEntries_ShareTheSameKey()
    {
        var keys = AuditTargetType.All.Select(t => t.Key).ToArray();

        Assert.Equal(keys.Length, keys.Distinct().Count());
    }

    [Fact]
    public void All_IsNotMissingAnyDeclaredStaticField()
    {
        var declared = DeclaredFields().Except(AuditTargetType.All).ToArray();

        Assert.Empty(declared);
    }

    [Fact]
    public void FromKey_ResolvesByTaxonomyString()
    {
        Assert.Same(AuditTargetType.Tenant, AuditTargetType.FromKey("tenant"));
    }

    [Fact]
    public void FromKey_UnknownKey_ReturnsNull()
    {
        Assert.Null(AuditTargetType.FromKey("no-such-target"));
    }
}
