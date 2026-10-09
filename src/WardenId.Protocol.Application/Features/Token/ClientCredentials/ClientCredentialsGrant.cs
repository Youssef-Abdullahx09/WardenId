namespace WardenId.Protocol.Application.Features.Token.ClientCredentials;

/// <summary>What goes into a client_credentials access token. No OpenIddict types on purpose.</summary>
public sealed record ClientCredentialsGrant(string Subject, Guid TenantId, string Audience, TimeSpan AccessTokenLifetime);
