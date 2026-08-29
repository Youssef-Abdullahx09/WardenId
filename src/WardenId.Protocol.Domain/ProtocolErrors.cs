using FluentResults;

namespace WardenId.Protocol.Domain;

public static class ProtocolErrors
{
    public static Error Validation(string reason)
        => new Error(reason).WithMetadata(ProtocolErrorCodes.MetadataKey, ProtocolErrorCodes.Validation);

    public static Error TokenHashRequired() => Validation("Refresh token hash is required.");

    public static Error RefreshTokenExpiryMustBeFuture() => Validation("Refresh token expiry must be in the future.");
}
