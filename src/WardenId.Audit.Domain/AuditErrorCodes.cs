using WardenId.SharedKernel.Domain;

namespace WardenId.Audit.Domain;

public static class AuditErrorCodes
{
    public const string MetadataKey = ErrorMetadataKeys.ErrorCode;

    public const string NotFound = "NotFound";
    public const string Validation = "Validation";
    public const string Conflict = "Conflict";
}
