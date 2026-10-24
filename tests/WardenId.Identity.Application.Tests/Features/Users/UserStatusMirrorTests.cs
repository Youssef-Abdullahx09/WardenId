using ReadUserStatus = WardenId.Identity.Query.Contracts.Features.Users.UserStatus;

namespace WardenId.Identity.Application.Tests.Features.Users;

/// <summary>
/// Query.Contracts mirrors the domain's UserStatus; the read side parses the persisted member name into it, so the
/// two must keep the same members.
/// </summary>
public class UserStatusMirrorTests
{
    [Fact]
    public void ReadSideUserStatus_HasTheDomainsMembers()
    {
        Assert.Equal(
            Enum.GetValues<UserStatus>().Select(s => (s.ToString(), (int)s)),
            Enum.GetValues<ReadUserStatus>().Select(s => (s.ToString(), (int)s)));
    }
}
