using FluentResults;
using MediatR;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Policies.UpdateIdentity;

// Every field is an override: null (or a null section) means "use the global value".
public sealed record PasswordOverrides(
    int? MinLength,
    bool? RequireUppercase,
    bool? RequireDigit,
    bool? RequireSymbol,
    bool? BreachCheckEnabled,
    int? HistoryCount);

/// <param name="LockoutDuration">ISO 8601 duration, e.g. "PT15M".</param>
public sealed record LockoutOverrides(int? MaxFailedAttempts, string? LockoutDuration);

public sealed record MfaOverrides(MfaPolicyMode? Mode);

public sealed record SelfRegistrationOverrides(bool? Enabled);

/// <param name="Url">An absolute http or https URL, at most 2048 characters. Null, empty or blank uses the global value.</param>
public sealed record TermsOverrides(string? Url);

/// <summary>Replaces the tenant's identity policy overrides as a whole.</summary>
public sealed record Command(
    Guid TenantId,
    PasswordOverrides? Password,
    LockoutOverrides? Lockout,
    MfaOverrides? Mfa,
    SelfRegistrationOverrides? SelfRegistration,
    TermsOverrides? Terms,
    Guid ActorUserId) : IRequest<Result>;
