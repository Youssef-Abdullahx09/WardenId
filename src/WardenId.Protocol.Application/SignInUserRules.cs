using WardenId.Identity.Integration.Command.Features.Users.Authenticate;
using WardenId.Identity.Integration.Command.Features.Users.GetForSignIn;

namespace WardenId.Protocol.Application;

internal static class SignInUserRules
{
    /// <summary>Active with no pending required actions — the only state that may get or redeem a code.</summary>
    public static bool IsUsable(SignInUser user) => user.Status == UserStatus.Active && user.PendingActions.Count == 0;
}
