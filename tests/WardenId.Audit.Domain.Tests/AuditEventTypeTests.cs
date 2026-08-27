using System.Reflection;

namespace WardenId.Audit.Domain.Tests;

public class AuditEventTypeTests
{
    private static IEnumerable<AuditEventType> DeclaredFields() =>
        typeof(AuditEventType)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(AuditEventType))
            .Select(f => (AuditEventType)f.GetValue(null)!);

    public static IEnumerable<object[]> AllCatalogEntries() => AuditEventType.All.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(AllCatalogEntries))]
    public void EveryCatalogEntry_IsAcceptedByAuditEventCreate(AuditEventType eventType)
    {
        var result = AuditEvent.Create(
            Guid.NewGuid(), AuditChannel.AuthnInteractive, eventType, 1, DateTimeOffset.UtcNow,
            AuditSeverity.Info, Actor.Anonymous(ActorScope.Platform()), AuditOutcome.Success(),
            ClientContext.Unknown(), AuthContext.None(), Correlation.None());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void NoTwoCatalogEntries_ShareTheSameKey()
    {
        var keys = AuditEventType.All.Select(t => t.Key).ToArray();

        Assert.Equal(keys.Length, keys.Distinct().Count());
    }

    [Fact]
    public void All_IsNotMissingAnyDeclaredStaticField()
    {
        // Guards against forgetting to add a new catalog entry to the All list.
        var declared = DeclaredFields().Except(AuditEventType.All).ToArray();

        Assert.Empty(declared);
    }

    [Fact]
    public void FromValue_ResolvesTheSameInstanceAsTheStaticField()
    {
        // Exercises Enumeration<TEnum>'s reflection-built lookup dictionary for real for the
        // first time in this codebase — nothing else derives from Enumeration<TEnum> yet.
        Assert.Same(AuditEventType.UserSessionStart, AuditEventType.FromValue(1));
    }

    [Fact]
    public void FromName_ResolvesByCSharpIdentifier()
    {
        Assert.Same(AuditEventType.UserSessionStart, AuditEventType.FromName(nameof(AuditEventType.UserSessionStart)));
    }

    [Fact]
    public void FromKey_ResolvesByTaxonomyString()
    {
        Assert.Same(AuditEventType.UserSessionStart, AuditEventType.FromKey("user.session.start"));
    }

    [Fact]
    public void FromKey_UnknownKey_ReturnsNull()
    {
        Assert.Null(AuditEventType.FromKey("no.such.event"));
    }
}
