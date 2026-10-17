using FluentResults;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Application.Abstractions;
using WardenId.Protocol.Domain.TokenPolicies;

namespace WardenId.Protocol.Application.Features.Policies.UpdateTokens;

public sealed class Handler(
    IProtocolDbContext dbContext,
    ITenantLifetimes tenantLifetimes,
    IOptions<ProtocolTokenOptions> tokenOptions,
    IOptions<ProtocolSessionOptions> sessionOptions,
    IRefreshTokenRetention refreshTokenRetention) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
    {
        var overridesResult = TenantLifetimeOverrides.Create(
            request.AccessTokenLifetime, request.IdTokenLifetime, request.RefreshTokenLifetime,
            request.RefreshTokenAbsoluteLifetime, request.AuthorizationCodeLifetime,
            request.SessionCookieLifetime, request.SessionAbsoluteLifetime);
        if (overridesResult.IsFailed) return Result.Fail(overridesResult.Errors);

        var overrides = overridesResult.Value;
        var effectiveResult = ValidateEffective(overrides);
        if (effectiveResult.IsFailed) return effectiveResult;

        var policy = await dbContext.TenantTokenPolicies
            .SingleOrDefaultAsync(p => p.TenantId == request.TenantId, cancellationToken);
        if (policy is null)
            dbContext.TenantTokenPolicies.Add(TenantTokenPolicy.Create(request.TenantId, overrides));
        else
            policy.Replace(overrides);

        await dbContext.SaveChangesAsync(request.ActorId, cancellationToken);
        tenantLifetimes.Invalidate(request.TenantId);
        return Result.Ok();
    }

    /// <summary>
    /// The pairs that mix the tenant's values with the global defaults: e.g. a tenant refresh lifetime longer than
    /// the global absolute cap is rejected rather than silently clamped.
    /// </summary>
    private Result ValidateEffective(TenantLifetimeOverrides overrides)
    {
        var effective = EffectiveLifetimes.Resolve(tokenOptions.Value, sessionOptions.Value, overrides);

        if (effective.RefreshTokenAbsolute < effective.RefreshToken)
            return Result.Fail(TokenPolicyErrors.ShorterThan(
                TenantLifetimeOverrides.RefreshTokenAbsoluteField, effective.RefreshTokenAbsolute,
                TenantLifetimeOverrides.RefreshTokenField, effective.RefreshToken,
                "no refresh token outlives the absolute cap (the global default applies to any field left null)"));

        if (effective.SessionAbsolute < effective.SessionCookie)
            return Result.Fail(TokenPolicyErrors.ShorterThan(
                TenantLifetimeOverrides.SessionAbsoluteField, effective.SessionAbsolute,
                TenantLifetimeOverrides.SessionCookieField, effective.SessionCookie,
                "sliding the cookie never extends the session past its absolute cap (the global default applies to any field left null)"));

        if (overrides.RefreshTokenAbsolute is { } absolute
            && refreshTokenRetention.MinimumRetention is { } retention
            && absolute > retention)
            return Result.Fail(TokenPolicyErrors.LongerThan(
                TenantLifetimeOverrides.RefreshTokenAbsoluteField, absolute,
                "the refresh-token retention (ProtocolOpenIddict:PruneMinimumAge)", retention,
                "redeemed refresh tokens are pruned after that, so a replay could no longer revoke the token family"));

        return Result.Ok();
    }
}
