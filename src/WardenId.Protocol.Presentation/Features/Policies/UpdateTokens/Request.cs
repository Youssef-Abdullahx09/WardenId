using System.Xml;
using FluentResults;
using WardenId.Protocol.Domain;
using WardenId.Protocol.Domain.TokenPolicies;
using UpdateTokensCommand = WardenId.Protocol.Application.Features.Policies.UpdateTokens.Command;

namespace WardenId.Protocol.Presentation.Features.Policies.UpdateTokens;

/// <summary>
/// Body of <c>PUT .../policies/tokens</c>: the tenant's complete override document. Each field is an ISO 8601
/// duration (e.g. "PT15M", "P30D", same format as the GET response); null or omitted means "use the global
/// default", so sending all nulls clears every override.
/// </summary>
public sealed record Request(
    string? AccessTokenLifetime,
    string? IdTokenLifetime,
    string? RefreshTokenLifetime,
    string? RefreshTokenAbsoluteLifetime,
    string? AuthorizationCodeLifetime,
    string? SessionCookieLifetime,
    string? SessionAbsoluteLifetime)
{
    public Result<UpdateTokensCommand> ToCommand(Guid tenantId, Guid actorId)
    {
        var errors = new List<IError>();
        var accessToken = Parse(TenantLifetimeOverrides.AccessTokenField, AccessTokenLifetime, errors);
        var idToken = Parse(TenantLifetimeOverrides.IdTokenField, IdTokenLifetime, errors);
        var refreshToken = Parse(TenantLifetimeOverrides.RefreshTokenField, RefreshTokenLifetime, errors);
        var refreshTokenAbsolute = Parse(TenantLifetimeOverrides.RefreshTokenAbsoluteField, RefreshTokenAbsoluteLifetime, errors);
        var authorizationCode = Parse(TenantLifetimeOverrides.AuthorizationCodeField, AuthorizationCodeLifetime, errors);
        var sessionCookie = Parse(TenantLifetimeOverrides.SessionCookieField, SessionCookieLifetime, errors);
        var sessionAbsolute = Parse(TenantLifetimeOverrides.SessionAbsoluteField, SessionAbsoluteLifetime, errors);
        if (errors.Count > 0) return Result.Fail<UpdateTokensCommand>(errors);

        return Result.Ok(new UpdateTokensCommand(
            tenantId, accessToken, idToken, refreshToken, refreshTokenAbsolute, authorizationCode, sessionCookie,
            sessionAbsolute, actorId));
    }

    private static TimeSpan? Parse(string field, string? value, List<IError> errors)
    {
        if (value is null) return null;
        try
        {
            // Years and months have no fixed length, so they are refused rather than guessed.
            var timeStart = value.IndexOf('T');
            var datePart = timeStart >= 0 ? value[..timeStart] : value;
            if (datePart.Contains('Y') || datePart.Contains('M')) throw new FormatException();
            return XmlConvert.ToTimeSpan(value);
        }
        catch (Exception e) when (e is FormatException or OverflowException)
        {
            errors.Add(ProtocolErrors.Validation(
                $"{field} must be an ISO 8601 duration in days, hours, minutes or seconds (e.g. \"PT15M\", \"P30D\"), or null for the global default."));
            return null;
        }
    }
}
