using System.Xml;
using FluentResults;
using MediatR;
using WardenId.Identity.Application.Abstractions;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Policies.UpdateIdentity;

/// <summary>
/// Stores the tenant's overrides and drops its cached policy, so this instance applies them on the
/// next sign-in. Existing passwords aren't re-checked against stricter rules; only new ones are.
/// </summary>
public sealed class Handler(
    IIdentityTenantPolicyRepository tenantPolicies,
    IdentityTenantPolicyCache cache) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var overridesResult = ToOverrides(request);
        if (overridesResult.IsFailed) return Result.Fail(overridesResult.Errors);
        var overrides = overridesResult.Value;

        var existing = await tenantPolicies.GetAsync(request.TenantId, cancellationToken);
        if (existing is null)
        {
            var createResult = IdentityTenantPolicy.Create(request.TenantId, overrides);
            if (createResult.IsFailed) return Result.Fail(createResult.Errors);

            await tenantPolicies.CreateAsync(createResult.Value, request.ActorUserId, cancellationToken);
        }
        else
        {
            var replaceResult = existing.Replace(overrides);
            if (replaceResult.IsFailed) return replaceResult;

            await tenantPolicies.UpdateAsync(existing, request.ActorUserId, cancellationToken);
        }

        cache.Invalidate(request.TenantId);
        return Result.Ok();
    }

    private static Result<IdentityPolicyOverrides> ToOverrides(Command request)
    {
        TimeSpan? lockoutDuration = null;
        if (request.Lockout?.LockoutDuration is { } duration)
        {
            try
            {
                lockoutDuration = XmlConvert.ToTimeSpan(duration);
            }
            catch (Exception e) when (e is FormatException or OverflowException)
            {
                return Result.Fail(IdentityPolicyErrors.LockoutDurationInvalidFormat());
            }
        }

        return Result.Ok(new IdentityPolicyOverrides(
            request.Password?.MinLength,
            request.Password?.RequireUppercase,
            request.Password?.RequireDigit,
            request.Password?.RequireSymbol,
            request.Password?.BreachCheckEnabled,
            request.Password?.HistoryCount,
            request.Lockout?.MaxFailedAttempts,
            lockoutDuration,
            request.Mfa?.Mode,
            request.SelfRegistration?.Enabled));
    }
}
