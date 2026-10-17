using System.Xml;
using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Policies.GetIdentity;

/// <summary>Reads the tenant's overrides from the database, not the sign-in cache, so an admin sees a save at once.</summary>
public sealed class Handler(
    IIdentityTenantPolicyRepository tenantPolicies,
    IOptions<IdentityPolicyOptions> options) : IRequestHandler<Query, Result<IdentityPolicy>>
{
    public const string GlobalScope = "global";
    public const string TenantScope = "tenant";

    public async Task<Result<IdentityPolicy>> Handle(Query request, CancellationToken cancellationToken)
    {
        var overrides = await tenantPolicies.GetOverridesAsync(request.TenantId, cancellationToken);

        var effectiveResult = EffectiveIdentityPolicy.Merge(options.Value, overrides);
        if (effectiveResult.IsFailed) return Result.Fail(effectiveResult.Errors);
        var effective = effectiveResult.Value;
        var password = effective.Password;
        var overridden = OverriddenFields(effective.Overrides);

        return Result.Ok(new IdentityPolicy(
            new PasswordSettings(
                password.MinLength,
                password.RequireUppercase,
                password.RequireDigit,
                password.RequireSymbol,
                password.BreachCheckEnabled,
                password.HistoryCount),
            new LockoutSettings(effective.MaxFailedLoginAttempts, XmlConvert.ToString(effective.LockoutDuration)),
            new MfaSettings(effective.Mfa.Mode),
            new SelfRegistrationSettings(effective.SelfRegistrationEnabled),
            overridden.Count > 0 ? TenantScope : GlobalScope,
            overridden));
    }

    private static List<string> OverriddenFields(IdentityPolicyOverrides overrides)
    {
        var fields = new List<string>();
        void Add(bool isSet, string path) { if (isSet) fields.Add(path); }

        Add(overrides.PasswordMinLength is not null, "password.minLength");
        Add(overrides.PasswordRequireUppercase is not null, "password.requireUppercase");
        Add(overrides.PasswordRequireDigit is not null, "password.requireDigit");
        Add(overrides.PasswordRequireSymbol is not null, "password.requireSymbol");
        Add(overrides.PasswordBreachCheckEnabled is not null, "password.breachCheckEnabled");
        Add(overrides.PasswordHistoryCount is not null, "password.historyCount");
        Add(overrides.MaxFailedLoginAttempts is not null, "lockout.maxFailedAttempts");
        Add(overrides.LockoutDuration is not null, "lockout.lockoutDuration");
        Add(overrides.MfaMode is not null, "mfa.mode");
        Add(overrides.SelfRegistrationEnabled is not null, "selfRegistration.enabled");
        return fields;
    }
}
