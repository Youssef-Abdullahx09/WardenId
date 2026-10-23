using System.Xml;
using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Domain.TokenPolicies;
using WardenId.Protocol.Query.Contracts;
using PolicyQuery = WardenId.Protocol.Query.Contracts.Features.Policies.GetTokens.Query;

namespace WardenId.Protocol.Application.Features.Policies.GetTokens;

public sealed class Handler(
    IPoliciesQuery policiesQuery,
    IOptions<ProtocolTokenOptions> tokenOptions,
    IOptions<ProtocolSessionOptions> sessionOptions) : IRequestHandler<Query, Result<TokenPolicy>>
{
    public const string GlobalScope = "global";
    public const string TenantScope = "tenant";

    public async Task<Result<TokenPolicy>> Handle(Query request, CancellationToken cancellationToken)
    {
        // Uncached: an admin reading back what they just saved must see it, whichever node answers.
        var stored = await policiesQuery.GetTokens(new PolicyQuery(request.TenantId), cancellationToken);
        if (stored.IsFailed) return Result.Fail(stored.Errors);

        var overrides = stored.Value is { } row
            ? TenantLifetimeOverrides.FromTrustedSource(
                row.AccessTokenLifetime, row.IdTokenLifetime, row.RefreshTokenLifetime, row.RefreshTokenAbsoluteLifetime,
                row.AuthorizationCodeLifetime, row.SessionCookieLifetime, row.SessionAbsoluteLifetime)
            : null;
        var effective = EffectiveLifetimes.Resolve(tokenOptions.Value, sessionOptions.Value, overrides);
        var tenant = effective.Overrides;

        return Result.Ok(new TokenPolicy(
            XmlConvert.ToString(effective.AccessToken),
            XmlConvert.ToString(effective.IdToken),
            XmlConvert.ToString(effective.RefreshToken),
            XmlConvert.ToString(effective.RefreshTokenAbsolute),
            XmlConvert.ToString(effective.AuthorizationCode),
            XmlConvert.ToString(effective.SessionCookie),
            XmlConvert.ToString(effective.SessionAbsolute),
            effective.IsTenantScoped ? TenantScope : GlobalScope,
            new TokenPolicyOverrides(
                Format(tenant.AccessToken),
                Format(tenant.IdToken),
                Format(tenant.RefreshToken),
                Format(tenant.RefreshTokenAbsolute),
                Format(tenant.AuthorizationCode),
                Format(tenant.SessionCookie),
                Format(tenant.SessionAbsolute))));
    }

    private static string? Format(TimeSpan? value) => value is { } v ? XmlConvert.ToString(v) : null;
}
