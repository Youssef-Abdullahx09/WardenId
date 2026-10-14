using System.Text.Json;
using OpenIddict.EntityFrameworkCore.Models;
using WardenId.ClientManagement.Integration.Command.Features.Clients.GetForAuthentication;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace WardenId.Protocol.Infrastructure.OpenIddict.Stores;

/// <summary>ClientManagement's view of a client → the transient entity OpenIddict validates against.</summary>
public static class ClientApplicationMapper
{
    public static OpenIddictEntityFrameworkCoreApplication Map(ClientForAuthentication client)
    {
        var permissions = new List<string> { Permissions.Endpoints.Token };
        var confidential = client.Kind == ClientKind.Confidential;
        if (client.AllowedGrants.Contains(AllowedGrant.ClientCredentials))
            permissions.Add(Permissions.GrantTypes.ClientCredentials);
        if (client.AllowedGrants.Contains(AllowedGrant.AuthorizationCode))
        {
            // "openid" needs no permission; profile/email are the hard-coded scopes until FR-CM-04.
            // End session travels with the code flow: only clients holding an ID token can log a user out.
            permissions.AddRange(
            [
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Email
            ]);
        }

        if (client.AllowedGrants.Contains(AllowedGrant.RefreshToken))
            permissions.Add(Permissions.GrantTypes.RefreshToken);

        // Every client may revoke its own tokens (RFC 7009 — public clients by client_id alone). Only
        // confidential clients may introspect: RFC 7662 requires the caller to authenticate.
        permissions.Add(Permissions.Endpoints.Revocation);
        if (confidential)
            permissions.Add(Permissions.Endpoints.Introspection);

        string[] requirements = !confidential || client.RequirePkce
            ? [Requirements.Features.ProofKeyForCodeExchange]
            : [];

        return new OpenIddictEntityFrameworkCoreApplication
        {
            Id = client.Id.ToString(),
            ClientId = client.ClientId,
            ClientType = confidential ? ClientTypes.Confidential : ClientTypes.Public,
            // Non-null marks the client as having a secret; the real check is WardenApplicationManager.
            ClientSecret = confidential ? "managed-by-ClientManagement" : null,
            Permissions = JsonSerializer.Serialize(permissions),
            RedirectUris = JsonSerializer.Serialize(client.RedirectUris),
            // OpenIddict exact-matches post_logout_redirect_uri against this list (ValidatePostLogoutRedirectUriAsync).
            PostLogoutRedirectUris = JsonSerializer.Serialize(client.PostLogoutRedirectUris),
            Requirements = requirements.Length > 0 ? JsonSerializer.Serialize(requirements) : null,
            ConcurrencyToken = client.Id.ToString()
        };
    }
}
