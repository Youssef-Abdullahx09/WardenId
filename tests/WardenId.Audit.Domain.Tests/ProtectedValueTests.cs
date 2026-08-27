using System.Reflection;

namespace WardenId.Audit.Domain.Tests;

public class ProtectedValueTests
{
    [Fact]
    public void Redact_SameRawValue_ProducesSameHash()
    {
        var first = ProtectedValue.Redact("super-secret-token");
        var second = ProtectedValue.Redact("super-secret-token");

        Assert.Equal(first.Hash, second.Hash);
    }

    [Fact]
    public void Redact_DifferentRawValues_ProduceDifferentHashes()
    {
        var first = ProtectedValue.Redact("super-secret-token-a");
        var second = ProtectedValue.Redact("super-secret-token-b");

        Assert.NotEqual(first.Hash, second.Hash);
    }

    [Fact]
    public void Redact_WithPrefixEnabled_ExposesOnlyAShortPrefix()
    {
        var protectedValue = ProtectedValue.Redact("abcdefghijklmnop");

        Assert.Equal("abcd", protectedValue.Prefix);
        Assert.DoesNotContain("efghijklmnop", protectedValue.Hash);
    }

    [Fact]
    public void Redact_WithPrefixDisabled_HasNoPrefix()
    {
        var protectedValue = ProtectedValue.Redact("abcdefghijklmnop", includePrefix: false);

        Assert.Null(protectedValue.Prefix);
    }

    [Fact]
    public void Redact_ShortValue_HasNoPrefix()
    {
        var protectedValue = ProtectedValue.Redact("ab");

        Assert.Null(protectedValue.Prefix);
    }

    [Fact]
    public void Redact_EmptyRawValue_Throws()
    {
        Assert.Throws<ArgumentException>(() => ProtectedValue.Redact(""));
    }

    [Fact]
    public void FromTrustedSource_RoundTripsHashAndPrefix()
    {
        var original = ProtectedValue.Redact("some-token-value");

        var rehydrated = ProtectedValue.FromTrustedSource(original.Hash, original.Prefix);

        Assert.Equal(original, rehydrated);
    }

    [Fact]
    public void PublicSurface_HasNoMemberThatCanReturnTheRawValue()
    {
        // I9: no accessor may return the raw value — only Hash and Prefix are ever exposed.
        var members = typeof(ProtectedValue).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal(["Hash", "Prefix"], members.OrderBy(n => n));
    }
}
