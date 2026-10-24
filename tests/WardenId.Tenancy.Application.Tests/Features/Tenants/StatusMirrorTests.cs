using ReadStatus = WardenId.Tenancy.Query.Contracts.Features.Tenants.Status;

namespace WardenId.Tenancy.Application.Tests.Features.Tenants;

/// <summary>
/// Query.Contracts mirrors the domain's tenant Status; the read side parses the persisted member name into it, so the
/// two must keep the same members.
/// </summary>
public class StatusMirrorTests
{
    [Fact]
    public void ReadSideStatus_HasTheDomainsMembers()
    {
        Assert.Equal(
            Enum.GetValues<Status>().Select(s => (s.ToString(), (int)s)),
            Enum.GetValues<ReadStatus>().Select(s => (s.ToString(), (int)s)));
    }
}
