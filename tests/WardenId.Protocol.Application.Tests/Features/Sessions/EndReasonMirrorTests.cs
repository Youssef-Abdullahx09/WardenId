using WardenId.Protocol.Domain.Sessions;
using ReadEndReason = WardenId.Protocol.Query.Contracts.Features.Sessions.EndReason;

namespace WardenId.Protocol.Application.Tests.Features.Sessions;

/// <summary>
/// Query.Contracts mirrors the domain's EndReason; the read side parses the persisted member name into it, so the
/// two must keep the same members.
/// </summary>
public class EndReasonMirrorTests
{
    [Fact]
    public void ReadSideEndReason_HasTheDomainsMembers()
    {
        Assert.Equal(
            Enum.GetValues<EndReason>().Select(r => (r.ToString(), (int)r)),
            Enum.GetValues<ReadEndReason>().Select(r => (r.ToString(), (int)r)));
    }
}
