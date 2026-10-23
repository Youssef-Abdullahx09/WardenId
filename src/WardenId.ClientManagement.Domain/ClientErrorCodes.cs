using WardenId.SharedKernel.Domain.Utilities;

namespace WardenId.ClientManagement.Domain;

public static class ClientErrorCodes
{
    public const string MetadataKey = ErrorMetadataKeys.ErrorCode;

    public const string NotFound = "NotFound";
    public const string Validation = "Validation";
    public const string InvalidCredentials = "InvalidCredentials";
}
