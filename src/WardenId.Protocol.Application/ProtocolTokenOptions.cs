using WardenId.Protocol.Application.Utilities;

namespace WardenId.Protocol.Application;

public sealed class ProtocolTokenOptions
{
    /// <summary>The placeholder in <see cref="DefaultAudience"/> replaced with the tenant issuer.</summary>
    public const string IssuerPlaceholder = "{issuer}";

    public string IssuerBaseUri { get; init; } = "https://wardenid.local";

    /// <summary>Access-token audience template. "{issuer}" is replaced with the tenant issuer.</summary>
    public string DefaultAudience { get; init; } = IssuerPlaceholder + "/api";

    /// <summary>Access-token audience for a tenant: DefaultAudience with "{issuer}" replaced.</summary>
    public string AudienceFor(string slug) =>
        DefaultAudience.Replace(IssuerPlaceholder, IssuerUri.For(IssuerBaseUri, slug), StringComparison.Ordinal);

    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan IdTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);
    /// <summary>Sliding: each rotated refresh token gets this (or the client override), never past the absolute cap.</summary>
    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(30);

    /// <summary>Hard session limit measured from auth_time; no refresh token outlives it.</summary>
    public TimeSpan RefreshTokenAbsoluteLifetime { get; init; } = TimeSpan.FromDays(90);
    public TimeSpan AuthorizationCodeLifetime { get; init; } = TimeSpan.FromMinutes(5);
}
