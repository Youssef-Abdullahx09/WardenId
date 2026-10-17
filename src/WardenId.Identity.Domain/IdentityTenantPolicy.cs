using FluentResults;
using WardenId.Identity.Domain.Events;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

/// <summary>
/// One tenant's overrides of the global identity policy (password, lockout, MFA mode,
/// self-registration, terms of service link). At most one per tenant. Each field is null when the tenant uses the global
/// value. Tightening the password rules doesn't touch existing passwords: they're checked only
/// when a password is set (registration, change, reset).
/// </summary>
public sealed class IdentityTenantPolicy : AggregateRoot, IAuditableAggregate
{
    // Policy values are configuration, not secrets: audited in plain text on admin.config.
    private static readonly IReadOnlySet<string> NoSensitiveProperties = new HashSet<string>();

    public string AuditTargetTypeKey => "identity_policy";
    public AuditableChannel AuditChannel => AuditableChannel.AdminConfig;
    public IReadOnlySet<string> AuditSensitivePropertyNames => NoSensitiveProperties;

    /// <summary>One policy per tenant, so the tenant id identifies it in the audit log.</summary>
    public string? AuditTargetId => TenantId.ToString();

    private IdentityTenantPolicy(Guid tenantId) => TenantId = tenantId;

    public Guid TenantId { get; private set; }
    public int? PasswordMinLength { get; private set; }
    public bool? PasswordRequireUppercase { get; private set; }
    public bool? PasswordRequireDigit { get; private set; }
    public bool? PasswordRequireSymbol { get; private set; }
    public bool? PasswordBreachCheckEnabled { get; private set; }
    public int? PasswordHistoryCount { get; private set; }
    public int? MaxFailedLoginAttempts { get; private set; }
    public TimeSpan? LockoutDuration { get; private set; }
    public MfaPolicyMode? MfaMode { get; private set; }
    public bool? SelfRegistrationEnabled { get; private set; }
    public string? TermsUrl { get; private set; }

    public IdentityPolicyOverrides Overrides => new(
        PasswordMinLength,
        PasswordRequireUppercase,
        PasswordRequireDigit,
        PasswordRequireSymbol,
        PasswordBreachCheckEnabled,
        PasswordHistoryCount,
        MaxFailedLoginAttempts,
        LockoutDuration,
        MfaMode,
        SelfRegistrationEnabled,
        TermsUrl);

    public static Result<IdentityTenantPolicy> Create(Guid tenantId, IdentityPolicyOverrides overrides)
    {
        if (tenantId == Guid.Empty) return Result.Fail(IdentityPolicyErrors.TenantIdRequired());

        var validation = overrides.Validate();
        if (validation.IsFailed) return Result.Fail(validation.Errors);

        var policy = new IdentityTenantPolicy(tenantId);
        policy.Apply(overrides);
        policy.RaiseDomainEvent(new IdentityTenantPolicyChangedDomainEvent(tenantId));
        return Result.Ok(policy);
    }

    /// <summary>Replaces every override: a null field goes back to the global value.</summary>
    public Result Replace(IdentityPolicyOverrides overrides)
    {
        var validation = overrides.Validate();
        if (validation.IsFailed) return validation;
        if (overrides == Overrides) return Result.Ok();

        Apply(overrides);
        RaiseDomainEvent(new IdentityTenantPolicyChangedDomainEvent(TenantId));
        return Result.Ok();
    }

    private void Apply(IdentityPolicyOverrides overrides)
    {
        PasswordMinLength = overrides.PasswordMinLength;
        PasswordRequireUppercase = overrides.PasswordRequireUppercase;
        PasswordRequireDigit = overrides.PasswordRequireDigit;
        PasswordRequireSymbol = overrides.PasswordRequireSymbol;
        PasswordBreachCheckEnabled = overrides.PasswordBreachCheckEnabled;
        PasswordHistoryCount = overrides.PasswordHistoryCount;
        MaxFailedLoginAttempts = overrides.MaxFailedLoginAttempts;
        LockoutDuration = overrides.LockoutDuration;
        MfaMode = overrides.MfaMode;
        SelfRegistrationEnabled = overrides.SelfRegistrationEnabled;
        TermsUrl = overrides.TermsUrl;
    }
}
