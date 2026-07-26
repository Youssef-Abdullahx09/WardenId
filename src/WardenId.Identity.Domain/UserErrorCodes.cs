namespace WardenId.Identity.Domain;

public static class UserErrorCodes
{
    public const string MetadataKey = "ErrorCode";

    public const string NotFound = "NotFound";
    public const string Validation = "Validation";

    // Matches WardenId.SharedKernel.Presentation.ErrorCodes.AccountLocked verbatim so
    // HandleApiFailure already maps it to 423 with no Presentation-layer changes.
    public const string AccountLocked = "AccountLocked";
}
