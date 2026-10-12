using FluentResults;

namespace WardenId.Protocol.Domain;

public static class ProtocolErrors
{
    public static Error Validation(string reason)
        => new Error(reason).WithMetadata(ProtocolErrorCodes.MetadataKey, ProtocolErrorCodes.Validation);

    public static Error TokenHashRequired() => Validation("Refresh token hash is required.");

    public static Error RefreshTokenExpiryMustBeFuture() => Validation("Refresh token expiry must be in the future.");

    public static Error TenantNotFound()
        => new Error("Tenant not found.").WithMetadata(ProtocolErrorCodes.MetadataKey, ProtocolErrorCodes.NotFound);

    // One message for wrong password, unknown user, locked and disabled — no account enumeration.
    public static Error InvalidCredentials()
        => new Error("Invalid username or password.").WithMetadata(ProtocolErrorCodes.MetadataKey, ProtocolErrorCodes.InvalidCredentials);

    public static Error InvalidGrant()
        => new Error("The grant is not valid.").WithMetadata(ProtocolErrorCodes.MetadataKey, ProtocolErrorCodes.InvalidGrant);
}
