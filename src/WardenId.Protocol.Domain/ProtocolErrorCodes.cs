using WardenId.SharedKernel.Domain.Utilities;

namespace WardenId.Protocol.Domain;

public static class ProtocolErrorCodes
{
    public const string MetadataKey = ErrorMetadataKeys.ErrorCode;

    public const string Validation = "Validation";
    public const string NotFound = "NotFound";
    public const string InvalidCredentials = "InvalidCredentials";
    public const string InvalidGrant = "InvalidGrant";
    public const string SessionExpired = "SessionExpired";
}
