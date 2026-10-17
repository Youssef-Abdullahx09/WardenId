namespace WardenId.Protocol.Domain.Sessions;

/// <summary>Why a session ended. Persisted by name.</summary>
public enum EndReason
{
    UserLogout = 1,
    PasswordChanged = 2,
    UserDisabled = 3,
    UserDeleted = 4,
    AdminEnded = 5,

    /// <summary>Identity locked the account after too many failed sign-ins.</summary>
    AccountLocked = 6,

    /// <summary>The user replaced or removed their authenticator: their other sessions end, the one they used stays.</summary>
    MfaChanged = 7
}
