using FluentResults;

namespace WardenId.Identity.Domain;

public static class UserErrors
{
    public static Error NotFound =>
        new Error("User not found.")
            .WithMetadata(UserErrorCodes.MetadataKey, UserErrorCodes.NotFound);

    public static Error Validation(string reason)
        => new Error(reason)
            .WithMetadata(UserErrorCodes.MetadataKey, UserErrorCodes.Validation);

    public static Error AccountLocked(DateTimeOffset lockedUntilUtc)
        => new Error($"Account is locked until {lockedUntilUtc:O}.")
            .WithMetadata(UserErrorCodes.MetadataKey, UserErrorCodes.AccountLocked);

    public static Error EmailRequired() => Validation("Email is required.");
    public static Error EmailInvalidFormat() => Validation("Email is not a valid email address.");
    public static Error EmailTooLong() => Validation($"Email cannot exceed {Email.MaxLength} characters.");

    public static Error UsernameRequired() => Validation("Username is required.");
    public static Error UsernameInvalidLength() => Validation($"Username must be between {Username.MinLength} and {Username.MaxLength} characters.");
    public static Error UsernameInvalidFormat() => Validation("Username may only contain letters, digits, dots, underscores and hyphens.");

    public static Error PasswordPolicyMinLengthInvalid() => Validation("Password policy minimum length must be at least 1.");
    public static Error PasswordPolicyHistoryCountInvalid() => Validation("Password policy history count cannot be negative.");

    public static Error PasswordTooShort(int minLength) => Validation($"Password must be at least {minLength} characters.");
    public static Error PasswordMissingUppercase() => Validation("Password must contain an uppercase letter.");
    public static Error PasswordMissingDigit() => Validation("Password must contain a digit.");
    public static Error PasswordMissingSymbol() => Validation("Password must contain a symbol.");
    public static Error PasswordBreached() => Validation("Password appears in a known data breach and cannot be used.");
    public static Error PasswordReused() => Validation("Password matches a previously used password.");

    public static Error TokenInvalid() => Validation("Token is invalid.");
    public static Error TokenExpired() => Validation("Token has expired.");
    public static Error TokenAlreadyConsumed() => Validation("Token has already been used.");

    public static Error EmailAlreadyVerified() => Validation("Email is already verified.");
    public static Error InvalidStatusTransition() => Validation("User is not in a valid state for this operation.");
    public static Error DisableReasonRequired() => Validation("A reason is required to disable a user.");

    public static Error RecoveryCodeAlreadyUsed() => Validation("Recovery code has already been used.");
    public static Error RecoveryCodeInvalid() => Validation("Recovery code is invalid.");

    public static Error MfaFactorNotFound() => Validation("MFA factor not found.");
    public static Error MfaFactorAlreadyVerified() => Validation("MFA factor is already verified.");
    public static Error CannotRevokeLastRequiredFactor() => Validation("Cannot revoke the last MFA factor while MFA is required for this account.");

    public static Error AttributeKeyRequired() => Validation("Attribute key is required.");
    public static Error AttributeNotFound(string key) =>
        new Error($"User has no attribute '{key}'.")
            .WithMetadata(UserErrorCodes.MetadataKey, UserErrorCodes.NotFound);
    public static Error EmailAlreadyInUse() => Validation("Email is already in use.");
    public static Error UsernameAlreadyInUse() => Validation("Username is already in use.");
    public static Error InvalidCredentials() => new Error("Invalid credentials.")
        .WithMetadata(UserErrorCodes.MetadataKey, UserErrorCodes.InvalidCredentials);
}
