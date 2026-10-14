using FluentResults;

namespace WardenId.Protocol.Domain;

public static class SessionErrors
{
    public static Error NotFound()
        => new Error("Session not found.").WithMetadata(ProtocolErrorCodes.MetadataKey, ProtocolErrorCodes.NotFound);

    public static Error AlreadyEnded()
        => new Error("The session has already ended.").WithMetadata(ProtocolErrorCodes.MetadataKey, ProtocolErrorCodes.Validation);

    // Ended or past its absolute lifetime — either way the session can no longer grant anything.
    public static Error Ended()
        => new Error("The session has ended.").WithMetadata(ProtocolErrorCodes.MetadataKey, ProtocolErrorCodes.SessionExpired);

    public static Error InvalidLifetime()
        => new Error("A session must expire after it starts.").WithMetadata(ProtocolErrorCodes.MetadataKey, ProtocolErrorCodes.Validation);
}
