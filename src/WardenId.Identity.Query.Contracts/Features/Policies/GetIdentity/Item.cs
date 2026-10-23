namespace WardenId.Identity.Query.Contracts.Features.Policies.GetIdentity;

/// <summary>One row of Features/Policies/GetIdentity/Query.sql. Every column is null when the tenant uses the global value.</summary>
public sealed class Item
{
    public int? PasswordMinLength { get; init; }
    public bool? PasswordRequireUppercase { get; init; }
    public bool? PasswordRequireDigit { get; init; }
    public bool? PasswordRequireSymbol { get; init; }
    public bool? PasswordBreachCheckEnabled { get; init; }
    public int? PasswordHistoryCount { get; init; }
    public int? MaxFailedLoginAttempts { get; init; }

    /// <summary>Postgres interval, which Npgsql reads as <see cref="TimeSpan"/>.</summary>
    public TimeSpan? LockoutDuration { get; init; }

    /// <summary>The <c>MfaPolicyMode</c> member name, as persisted.</summary>
    public string? MfaMode { get; init; }

    public bool? SelfRegistrationEnabled { get; init; }
    public string? TermsUrl { get; init; }
}
