namespace WardenId.Identity.Integration.Command.Features.Users.Authenticate;

public enum UserStatus
{
    PendingVerification = 1,
    Active = 2,
    Disabled = 3,
    Locked = 4,
    SoftDeleted = 5
}
