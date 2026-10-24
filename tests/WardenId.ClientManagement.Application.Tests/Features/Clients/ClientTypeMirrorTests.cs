using ReadClientType = WardenId.ClientManagement.Query.Contracts.Features.Clients.ClientType;

namespace WardenId.ClientManagement.Application.Tests.Features.Clients;

/// <summary>
/// Query.Contracts mirrors the domain's ClientType; the read side parses the persisted member name into it, so the
/// two must keep the same members.
/// </summary>
public class ClientTypeMirrorTests
{
    [Fact]
    public void ReadSideClientType_HasTheDomainsMembers()
    {
        Assert.Equal(
            Enum.GetValues<ClientType>().Select(t => (t.ToString(), (int)t)),
            Enum.GetValues<ReadClientType>().Select(t => (t.ToString(), (int)t)));
    }
}
