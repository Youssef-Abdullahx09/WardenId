namespace WardenId.Identity.Query.Contracts.Features.Users;

// Read-side mirror of WardenId.Identity.Domain.UserStatus. Query.Contracts must not depend on Domain, so the read
// model declares its own copy; member names must match the domain enum's (they're what's persisted and parsed).

public enum UserStatus
{
    PendingVerification = 1,
    Active = 2,
    Disabled = 3,
    Locked = 4,
    SoftDeleted = 5
}
