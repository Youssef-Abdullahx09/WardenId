using System.Reflection;

namespace WardenId.Audit.Domain.Tests;

public class AuditCreationSourceTests
{
    private static IEnumerable<AuditCreationSource> DeclaredFields() =>
        typeof(AuditCreationSource)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(AuditCreationSource))
            .Select(f => (AuditCreationSource)f.GetValue(null)!);

    public static IEnumerable<object[]> AllCatalogEntries() => AuditCreationSource.All.Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(AllCatalogEntries))]
    public void EveryCatalogEntry_IsAcceptedByAuditTargetCreate(AuditCreationSource source)
    {
        var result = AuditTarget.Create(AuditTargetType.Tenant, "some-id", source);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void NoTwoCatalogEntries_ShareTheSameKey()
    {
        var keys = AuditCreationSource.All.Select(s => s.Key).ToArray();

        Assert.Equal(keys.Length, keys.Distinct().Count());
    }

    [Fact]
    public void All_IsNotMissingAnyDeclaredStaticField()
    {
        var declared = DeclaredFields().Except(AuditCreationSource.All).ToArray();

        Assert.Empty(declared);
    }

    [Fact]
    public void FromKey_ResolvesByTaxonomyString()
    {
        Assert.Same(AuditCreationSource.Dcr, AuditCreationSource.FromKey("dcr"));
    }

    [Fact]
    public void FromKey_UnknownKey_ReturnsNull()
    {
        Assert.Null(AuditCreationSource.FromKey("no-such-source"));
    }
}
