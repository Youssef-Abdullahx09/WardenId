using System.Xml;
using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Identity.Application.Utilities;
using WardenId.Identity.Domain;
using WardenId.Identity.Query.Contracts;
using PolicyQuery = WardenId.Identity.Query.Contracts.Features.Policies.GetIdentity.Query;

namespace WardenId.Identity.Application.Features.Policies.GetIdentity;

/// <summary>
/// Reads the tenant's overrides from the database (the Dapper query layer, Identity.Query), not the sign-in cache,
/// so an admin sees a save at once.
/// </summary>
public sealed class Handler(
    IPoliciesQuery policiesQuery,
    IOptions<IdentityPolicyOptions> options) : IRequestHandler<Query, Result<IdentityPolicy>>
{
    public const string GlobalScope = "global";
    public const string TenantScope = "tenant";

    public async Task<Result<IdentityPolicy>> Handle(Query request, CancellationToken cancellationToken)
    {
        var overridesResult = await policiesQuery.GetIdentity(new PolicyQuery(request.TenantId), cancellationToken);
        if (overridesResult.IsFailed) return Result.Fail(overridesResult.Errors);
        var overrides = overridesResult.Value is { } row
            ? new IdentityPolicyOverrides(
                row.PasswordMinLength,
                row.PasswordRequireUppercase,
                row.PasswordRequireDigit,
                row.PasswordRequireSymbol,
                row.PasswordBreachCheckEnabled,
                row.PasswordHistoryCount,
                row.MaxFailedLoginAttempts,
                row.LockoutDuration,
                QueryColumns.ParseOptionalEnum<MfaPolicyMode>(row.MfaMode),
                row.SelfRegistrationEnabled,
                row.TermsUrl)
            : null;

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
            new TermsSettings(effective.TermsUrl),
            overridden.Count > 0 ? TenantScope : GlobalScope,
            overridden));
    }

    private static List<string> OverriddenFields(IdentityPolicyOverrides overrides)
    {
        var fields = new List<string>();
        void Add(bool isSet, string path) { if (isSet) fields.Add(path); }

        Add(overrides.PasswordMinLength is not null, IdentityPolicyFieldPaths.Password.MinLength);
        Add(overrides.PasswordRequireUppercase is not null, IdentityPolicyFieldPaths.Password.RequireUppercase);
        Add(overrides.PasswordRequireDigit is not null, IdentityPolicyFieldPaths.Password.RequireDigit);
        Add(overrides.PasswordRequireSymbol is not null, IdentityPolicyFieldPaths.Password.RequireSymbol);
        Add(overrides.PasswordBreachCheckEnabled is not null, IdentityPolicyFieldPaths.Password.BreachCheckEnabled);
        Add(overrides.PasswordHistoryCount is not null, IdentityPolicyFieldPaths.Password.HistoryCount);
        Add(overrides.MaxFailedLoginAttempts is not null, IdentityPolicyFieldPaths.Lockout.MaxFailedAttempts);
        Add(overrides.LockoutDuration is not null, IdentityPolicyFieldPaths.Lockout.LockoutDuration);
        Add(overrides.MfaMode is not null, IdentityPolicyFieldPaths.Mfa.Mode);
        Add(overrides.SelfRegistrationEnabled is not null, IdentityPolicyFieldPaths.SelfRegistration.Enabled);
        Add(overrides.TermsUrl is not null, IdentityPolicyFieldPaths.Terms.Url);
        return fields;
    }
}
