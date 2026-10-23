using WardenId.SharedKernel.Domain;

namespace WardenId.Tenancy.Domain;

public static class TenantErrorCodes
{
    public const string MetadataKey = ErrorMetadataKeys.ErrorCode;

    public const string NotFound = "NotFound";
    public const string Validation = "Validation";
}
