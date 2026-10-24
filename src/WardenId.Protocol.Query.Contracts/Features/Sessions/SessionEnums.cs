namespace WardenId.Protocol.Query.Contracts.Features.Sessions;

// Read-side mirror of WardenId.Protocol.Domain.Sessions.EndReason. Query.Contracts must not depend on Domain, so the
// read model declares its own copy; member names must match the domain enum's (they're what's persisted and parsed).

public enum EndReason
{
    UserLogout = 1,
    PasswordChanged = 2,
    UserDisabled = 3,
    UserDeleted = 4,
    AdminEnded = 5,
    AccountLocked = 6,
    MfaChanged = 7
}
