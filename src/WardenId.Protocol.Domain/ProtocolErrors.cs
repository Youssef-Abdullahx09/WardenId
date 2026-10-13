using FluentResults;

namespace WardenId.Protocol.Domain;

public static class ProtocolErrors
{
    public static Error Validation(string reason)
        => new Error(reason).WithMetadata(ProtocolErrorCodes.MetadataKey, ProtocolErrorCodes.Validation);

    public static Error TenantNotFound()
        => new Error("Tenant not found.").WithMetadata(ProtocolErrorCodes.MetadataKey, ProtocolErrorCodes.NotFound);

    // One message for wrong password, unknown user, locked and disabled — no account enumeration.
    public static Error InvalidCredentials()
        => new Error("Invalid username or password.").WithMetadata(ProtocolErrorCodes.MetadataKey, ProtocolErrorCodes.InvalidCredentials);

    public static Error InvalidGrant()
        => new Error("The grant is not valid.").WithMetadata(ProtocolErrorCodes.MetadataKey, ProtocolErrorCodes.InvalidGrant);

    // Refresh/code redemption past ProtocolToken:RefreshTokenAbsoluteLifetime (from auth_time).
    public static Error SessionExpired()
        => new Error("The session has expired.").WithMetadata(ProtocolErrorCodes.MetadataKey, ProtocolErrorCodes.SessionExpired);
}
