using WardenId.Audit.Domain.Sealing;

namespace WardenId.Audit.Domain.Tests;

public class CheckpointDuePolicyTests
{
    [Fact]
    public void IsDue_NoSegmentsSinceLastCheckpoint_ReturnsFalse()
    {
        Assert.False(CheckpointDuePolicy.IsDue(0, TimeSpan.FromDays(1), maxSegments: 10, maxInterval: TimeSpan.FromHours(1)));
    }

    [Fact]
    public void IsDue_SegmentCountMet_ButIntervalNotYetElapsed_ReturnsFalse()
    {
        Assert.False(CheckpointDuePolicy.IsDue(10, TimeSpan.FromMinutes(1), maxSegments: 10, maxInterval: TimeSpan.FromHours(1)));
    }

    [Fact]
    public void IsDue_IntervalElapsed_ButSegmentCountNotMet_ReturnsFalse()
    {
        Assert.False(CheckpointDuePolicy.IsDue(1, TimeSpan.FromHours(2), maxSegments: 10, maxInterval: TimeSpan.FromHours(1)));
    }

    [Fact]
    public void IsDue_BothThresholdsMet_ReturnsTrue()
    {
        Assert.True(CheckpointDuePolicy.IsDue(10, TimeSpan.FromHours(2), maxSegments: 10, maxInterval: TimeSpan.FromHours(1)));
    }
}
