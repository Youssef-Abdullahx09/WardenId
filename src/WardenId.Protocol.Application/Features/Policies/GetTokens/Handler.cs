using System.Xml;
using FluentResults;
using MediatR;
using Microsoft.Extensions.Options;
using WardenId.Protocol.Application.Abstractions;

namespace WardenId.Protocol.Application.Features.Policies.GetTokens;

public sealed class Handler(
    IProtocolDbContext dbContext,
    IOptions<ProtocolTokenOptions> tokenOptions,
    IOptions<ProtocolSessionOptions> sessionOptions) : IRequestHandler<Query, Result<TokenPolicy>>
{
    public const string GlobalScope = "global";
    public const string TenantScope = "tenant";

    public async Task<Result<TokenPolicy>> Handle(Query request, CancellationToken cancellationToken)
    {
        // Uncached: an admin reading back what they just saved must see it, whichever node answers.
        var overrides = await TenantLifetimes.LoadAsync(dbContext, request.TenantId, cancellationToken);
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
