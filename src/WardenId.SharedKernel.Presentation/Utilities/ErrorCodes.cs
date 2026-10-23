using WardenId.SharedKernel.Domain.Utilities;

namespace WardenId.SharedKernel.Presentation.Utilities;

public static class ErrorCodes
{
    public const string MetadataKey = ErrorMetadataKeys.ErrorCode;

    public const string InvalidCredentials = "InvalidCredentials";
    public const string AccountLocked = "AccountLocked";
    public const string AccountDeactivated = "AccountDeactivated";
    public const string InvalidToken = "InvalidToken";
    public const string DuplicateIdentity = "DuplicateIdentity";
    public const string NotFound = "NotFound";
    public const string PasswordPolicy = "PasswordPolicy";
    public const string Forbidden = "Forbidden";
    public const string Conflict = "Conflict";
}