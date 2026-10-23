using System.Security.Claims;
using System.Text.Encodings.Web;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WardenId.Protocol.Application;
using WardenId.SharedKernel.Application;
using WardenId.SharedKernel.Domain;
using static OpenIddict.Abstractions.OpenIddictConstants;
using GetPublicKeysQuery = WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetPublicKeys.Query;

namespace WardenId.Protocol.Infrastructure.Authentication;

/// <summary>
/// Validates a <c>Bearer</c> access token issued by one of WardenId's own tenants. The issuer
/// names the tenant (<c>{IssuerBaseUri}/tenants/{slug}</c>); the token must be signed with that
/// tenant's keys (JWKS set, rotation overlap included), unexpired, ES256, and carry
/// <c>token_use=access</c> (identity tokens don't), <c>sub</c> and <c>tenant_id</c>.
/// <para>
/// Authentication only: which callers may use an endpoint is the authorization policy's call.
/// Revocation isn't checked; access tokens are JWTs and stay valid until they expire.
/// </para>
/// </summary>
public sealed class AccessTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISender sender,
    IOptions<ProtocolTokenOptions> tokenOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    private readonly JsonWebTokenHandler _tokens = new() { MapInboundClaims = false };

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? authorization = Request.Headers.Authorization;
        if (authorization is null
            || !authorization.StartsWith(AccessTokenAuthenticationDefaults.BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var token = authorization[AccessTokenAuthenticationDefaults.BearerPrefix.Length..].Trim();
        if (token.Length == 0)
        {
            return AuthenticateResult.NoResult();
        }

        // Read unvalidated only to pick the tenant; the signature check below binds the issuer.
        if (ReadIssuer(token) is not { } issuer)
        {
            return AuthenticateResult.Fail("The access token is malformed.");
        }

        if (TenantSlugOf(issuer) is not { } slug)
        {
            return AuthenticateResult.Fail("The access token was not issued by a WardenId tenant.");
        }

        var keys = await sender.Send(new GetPublicKeysQuery(slug), Context.RequestAborted);
        if (keys.IsFailed)
        {
            return AuthenticateResult.Fail("The access token's tenant has no signing keys.");
        }

        var result = await _tokens.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = issuer,
            // An unparseable stored JWK is an invariant violation (as in /jwks): JsonWebKey throws → 500.
            IssuerSigningKeys = keys.Value.PublicKeyJwks.Select(jwk => new JsonWebKey(jwk)).ToList(),
            ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
            // Access tokens carry the requesting client's resources, not one API audience, so the
            // tenant signature and token_use are what tie a token to this API.
            ValidateAudience = false,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = ClockSkew,
            AuthenticationType = Scheme.Name,
            NameClaimType = Claims.Subject,
            RoleClaimType = Claims.Role,
        });
        if (!result.IsValid)
        {
            return AuthenticateResult.Fail(result.Exception ?? new SecurityTokenException("The access token is not valid."));
        }

        var identity = result.ClaimsIdentity;
        if (identity.FindFirst(CustomClaimTypes.TokenUse)?.Value != TokenUse.Access
            // sub is a user id, or the client id for client-credentials tokens.
            || string.IsNullOrEmpty(identity.FindFirst(Claims.Subject)?.Value)
            || !Guid.TryParse(identity.FindFirst(CustomClaimTypes.TenantId)?.Value, out _))
        {
            return AuthenticateResult.Fail("The token is not a WardenId access token.");
        }

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = AccessTokenAuthenticationDefaults.BearerPrefix.TrimEnd();
        return Task.CompletedTask;
    }

    /// <summary>The unvalidated <c>iss</c>, or null when the token isn't a readable JWT.</summary>
    private string? ReadIssuer(string token)
    {
        if (!_tokens.CanReadToken(token))
        {
            return null;
        }

        try
        {
            return _tokens.ReadJsonWebToken(token).Issuer;
        }
        catch (ArgumentException)
        {
            // Three dot-separated parts that aren't Base64Url JSON (SecurityTokenMalformedException).
            return null;
        }
    }

    /// <summary>The slug when <paramref name="issuer"/> is exactly a tenant issuer, else null.</summary>
    private string? TenantSlugOf(string issuer)
    {
        var prefix = IssuerUri.For(tokenOptions.Value.IssuerBaseUri, string.Empty);
        if (!issuer.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var slug = issuer[prefix.Length..];
        return TenantSlugFormat.IsValid(slug) && IssuerUri.For(tokenOptions.Value.IssuerBaseUri, slug) == issuer
            ? slug
            : null;
    }
}
