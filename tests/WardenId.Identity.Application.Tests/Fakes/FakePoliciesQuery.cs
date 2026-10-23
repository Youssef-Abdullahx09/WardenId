using WardenId.Identity.Query.Contracts;
using GetIdentity = WardenId.Identity.Query.Contracts.Features.Policies.GetIdentity;

namespace WardenId.Identity.Application.Tests.Fakes;

/// <summary>Stores tenants' overrides and returns them as the query layer's raw row (the MFA mode by name).</summary>
public sealed class FakePoliciesQuery : IPoliciesQuery
{
    private readonly Dictionary<Guid, IdentityPolicyOverrides> _byTenant = new();

    public int GetIdentityCallCount { get; private set; }

    public FakePoliciesQuery With(Guid tenantId, IdentityPolicyOverrides overrides)
    {
        _byTenant[tenantId] = overrides;
        return this;
    }

    public Task<Result<GetIdentity.Item?>> GetIdentity(GetIdentity.Query query, CancellationToken cancellationToken)
    {
        GetIdentityCallCount++;
        var item = _byTenant.TryGetValue(query.TenantId, out var o)
            ? new GetIdentity.Item
            {
                PasswordMinLength = o.PasswordMinLength,
                PasswordRequireUppercase = o.PasswordRequireUppercase,
                PasswordRequireDigit = o.PasswordRequireDigit,
                PasswordRequireSymbol = o.PasswordRequireSymbol,
                PasswordBreachCheckEnabled = o.PasswordBreachCheckEnabled,
                PasswordHistoryCount = o.PasswordHistoryCount,
                MaxFailedLoginAttempts = o.MaxFailedLoginAttempts,
                LockoutDuration = o.LockoutDuration,
                MfaMode = o.MfaMode?.ToString(),
                SelfRegistrationEnabled = o.SelfRegistrationEnabled,
                TermsUrl = o.TermsUrl
            }
            : null;
        return Task.FromResult(Result.Ok(item));
    }
}
