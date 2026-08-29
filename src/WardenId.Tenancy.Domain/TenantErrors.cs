using FluentResults;

namespace WardenId.Tenancy.Domain;

public static class TenantErrors
{
    public static Error NotFound =>
        new Error($"Tenant not found.")
            .WithMetadata(TenantErrorCodes.MetadataKey, TenantErrorCodes.NotFound);

    public static Error Validation(string reason)
        => new Error(reason)
            .WithMetadata(TenantErrorCodes.MetadataKey, TenantErrorCodes.Validation);

    public static Error TenantSlugEmpty()
        => Validation("Tenant slug is required.");

    public static Error TenantSlugInvalidLength(int minLength, int maxLength)
        => Validation($"Tenant slug must be between {minLength} and {maxLength} characters.");

    public static Error TenantSlugMustBeLowercase()
        => Validation("Tenant slug must be lowercase.");

    public static Error TenantSlugConsecutiveHyphens()
        => Validation("Tenant slug cannot contain consecutive hyphens.");

    public static Error TenantSlugInvalidFormat()
        => Validation("Tenant slug may only contain lowercase letters, digits, and hyphens, " +
                      "and cannot start or end with a hyphen.");

    public static Error TenantSlugReserved(string candidate)
        => Validation($"'{candidate}' is reserved and cannot be used as a tenant slug.");

    public static Error TenantDisplayNameRequired()
        => Validation("Tenant display name is required.");

    public static Error TenantDisplayNameTooLong()
        => Validation("Tenant display name cannot exceed 200 characters.");

    public static Error TenantSigningKeyAlreadyAssigned()
        => Validation("Tenant already has a signing key; use a rotation operation instead.");

    public static Error TenantInvalidTransition(Status status)
        => Validation($"Transition not allowed a tenant in '{status}' status.");

    public static Error TenantNoActiveSigningKey()
        => Validation("Tenant cannot be activated without an active signing key.");

    public static Error NoActiveSigningKeyForSigning()
        => Validation("Tenant has no active signing key.");

    public static Error TenantSuspendReasonRequired()
        => Validation("A reason is required to suspend a tenant.");
    public static Error TenantAlreadyArchived()
        => Validation("Tenant is already archived.");
    public static Error TenantSlugAlreadyExists(Slug slug)
        => Validation($"enant slug '{slug}' is already in use.");
}