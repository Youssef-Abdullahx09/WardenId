using WardenId.Identity.Domain;
using ContractRequiredAction = WardenId.Identity.Integration.Command.Features.Users.Authenticate.RequiredAction;
using ContractUserStatus = WardenId.Identity.Integration.Command.Features.Users.Authenticate.UserStatus;

namespace WardenId.Identity.Application.Features.Users;

// Identity.Integration.Command must not depend on Identity.Domain (it's the module's public
// wire contract, kept independent so consumers never see internal domain enum changes) — so
// its UserStatus/RequiredAction mirror Domain's members and get mapped explicitly here rather
// than via a blind cast.
internal static class ContractMapping
{
    public static ContractUserStatus MapStatus(UserStatus status) => status switch
    {
        UserStatus.PendingVerification => ContractUserStatus.PendingVerification,
        UserStatus.Active => ContractUserStatus.Active,
        UserStatus.Disabled => ContractUserStatus.Disabled,
        UserStatus.Locked => ContractUserStatus.Locked,
        UserStatus.SoftDeleted => ContractUserStatus.SoftDeleted,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped UserStatus.")
    };

    public static ContractRequiredAction MapRequiredAction(RequiredAction action) => action switch
    {
        RequiredAction.ResetPassword => ContractRequiredAction.ResetPassword,
        RequiredAction.AcceptTos => ContractRequiredAction.AcceptTos,
        RequiredAction.EnrollMfa => ContractRequiredAction.EnrollMfa,
        RequiredAction.VerifyEmail => ContractRequiredAction.VerifyEmail,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unmapped RequiredAction.")
    };
}
