using ReadAuditChannel = WardenId.Audit.Query.Contracts.Features.AuditEvents.AuditChannel;
using ReadAuditResult = WardenId.Audit.Query.Contracts.Features.AuditEvents.AuditResult;

namespace WardenId.Audit.Application.Tests.Features.Events;

/// <summary>
/// Query.Contracts mirrors the domain's AuditChannel and AuditResult; the read side parses the persisted member names
/// into them and the list handler converts the decoded outcome by cast, so each pair must keep the same members.
/// </summary>
public class AuditEventEnumsMirrorTests
{
    [Fact]
    public void ReadSideAuditChannel_HasTheDomainsMembers()
    {
        Assert.Equal(
            Enum.GetValues<AuditChannel>().Select(c => (c.ToString(), (int)c)),
            Enum.GetValues<ReadAuditChannel>().Select(c => (c.ToString(), (int)c)));
    }

    [Fact]
    public void ReadSideAuditResult_HasTheDomainsMembers()
    {
        Assert.Equal(
            Enum.GetValues<AuditResult>().Select(r => (r.ToString(), (int)r)),
            Enum.GetValues<ReadAuditResult>().Select(r => (r.ToString(), (int)r)));
    }
}
