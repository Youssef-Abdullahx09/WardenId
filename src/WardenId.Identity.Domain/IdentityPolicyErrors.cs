using FluentResults;

namespace WardenId.Identity.Domain;

public static class IdentityPolicyErrors
{
    private static Error Validation(string reason) =>
        new Error(reason).WithMetadata(UserErrorCodes.MetadataKey, UserErrorCodes.Validation);

    public static Error TenantIdRequired() => Validation("Tenant id is required.");

    public static Error PasswordMinLengthOutOfRange() => Validation(
        $"password.minLength must be between {IdentityPolicyBounds.MinPasswordMinLength} and {IdentityPolicyBounds.MaxPasswordMinLength}.");

    public static Error PasswordHistoryCountOutOfRange() => Validation(
        $"password.historyCount must be between {IdentityPolicyBounds.MinPasswordHistoryCount} and {IdentityPolicyBounds.MaxPasswordHistoryCount}.");

    public static Error MaxFailedLoginAttemptsOutOfRange() => Validation(
        $"lockout.maxFailedAttempts must be between {IdentityPolicyBounds.MinMaxFailedLoginAttempts} and {IdentityPolicyBounds.MaxMaxFailedLoginAttempts}.");

    public static Error LockoutDurationOutOfRange() => Validation(
        "lockout.lockoutDuration must be between 1 minute (PT1M) and 24 hours (PT24H).");

    public static Error LockoutDurationInvalidFormat() => Validation(
        "lockout.lockoutDuration must be an ISO 8601 duration, e.g. \"PT15M\".");

    public static Error MfaModeInvalid() => Validation("mfa.mode is not a known MFA mode.");

    public static Error TermsUrlInvalid() => Validation(
        $"terms.url must be an absolute http or https URL of at most {IdentityPolicyBounds.MaxTermsUrlLength} characters.");
}
