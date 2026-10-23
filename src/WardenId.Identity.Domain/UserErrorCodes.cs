using WardenId.SharedKernel.Domain.Utilities;

namespace WardenId.Identity.Domain;

public static class UserErrorCodes
{
    public const string MetadataKey = ErrorMetadataKeys.ErrorCode;

    public const string NotFound = "NotFound";
    public const string Validation = "Validation";
    public const string InvalidCredentials = "InvalidCredentials";

    // Matches WardenId.SharedKernel.Presentation.Utilities.ErrorCodes.AccountLocked verbatim so
    // HandleApiFailure already maps it to 423 with no Presentation-layer changes.
    public const string AccountLocked = "AccountLocked";

    // Matches WardenId.SharedKernel.Presentation.Utilities.ErrorCodes.Forbidden, so HandleApiFailure maps it to 403.
    public const string Forbidden = "Forbidden";

    // Matches WardenId.SharedKernel.Presentation.Utilities.ErrorCodes.Conflict, so HandleApiFailure maps it to 409.
    public const string Conflict = "Conflict";
}
