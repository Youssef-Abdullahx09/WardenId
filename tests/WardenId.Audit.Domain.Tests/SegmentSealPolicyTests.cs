using WardenId.Audit.Domain.Sealing;

namespace WardenId.Audit.Domain.Tests;

public class SegmentSealPolicyTests
{
    [Fact]
    public void ShouldSeal_NoPendingRows_ReturnsFalse()
    {
        Assert.False(SegmentSealPolicy.ShouldSeal(0, TimeSpan.FromHours(1), maxSegmentSize: 10, maxSegmentAge: TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void ShouldSeal_PendingCountReachesMax_ReturnsTrue_EvenIfFresh()
    {
        Assert.True(SegmentSealPolicy.ShouldSeal(10, TimeSpan.Zero, maxSegmentSize: 10, maxSegmentAge: TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void ShouldSeal_OldestPendingExceedsMaxAge_ReturnsTrue_EvenIfFewRows()
    {
        Assert.True(SegmentSealPolicy.ShouldSeal(1, TimeSpan.FromMinutes(6), maxSegmentSize: 10_000, maxSegmentAge: TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void ShouldSeal_BelowBothThresholds_ReturnsFalse()
    {
        Assert.False(SegmentSealPolicy.ShouldSeal(5, TimeSpan.FromMinutes(1), maxSegmentSize: 10, maxSegmentAge: TimeSpan.FromMinutes(5)));
    }
}
