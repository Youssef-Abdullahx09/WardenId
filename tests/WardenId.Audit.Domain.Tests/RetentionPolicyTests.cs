using WardenId.Audit.Domain.Retention;

namespace WardenId.Audit.Domain.Tests;

public class RetentionPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsPurgeable_NotExported_ReturnsFalse_EvenIfVeryOld()
    {
        var occurredAt = Now - TimeSpan.FromDays(3650);
        Assert.False(RetentionPolicy.IsPurgeable(occurredAt, Now, TimeSpan.FromDays(1), isExported: false));
    }

    [Fact]
    public void IsPurgeable_ExportedButWithinRetentionWindow_ReturnsFalse()
    {
        var occurredAt = Now - TimeSpan.FromDays(1) + TimeSpan.FromMinutes(1);
        Assert.False(RetentionPolicy.IsPurgeable(occurredAt, Now, TimeSpan.FromDays(1), isExported: true));
    }

    [Fact]
    public void IsPurgeable_ExportedAndPastRetentionWindow_ReturnsTrue()
    {
        var occurredAt = Now - TimeSpan.FromDays(2);
        Assert.True(RetentionPolicy.IsPurgeable(occurredAt, Now, TimeSpan.FromDays(1), isExported: true));
    }

    [Fact]
    public void IsPurgeable_ExportedAndExactlyAtRetentionWindow_ReturnsTrue()
    {
        var occurredAt = Now - TimeSpan.FromDays(1);
        Assert.True(RetentionPolicy.IsPurgeable(occurredAt, Now, TimeSpan.FromDays(1), isExported: true));
    }
}
