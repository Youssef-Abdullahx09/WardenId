-- Three result sets: the client row (no row = not found), then its secrets, then its signing keys.
-- Owned collections (redirect URIs, post-logout redirect URIs, CORS origins) live in their own tables and are folded
-- into arrays; TokenLifetimeOverrides is owned in-row (all three columns NULL = no overrides).
SELECT client."Id",
       client."ClientId",
       client."TenantId",
       client."ClientType",
       client."DisplayName",
       client."Description",
       client."Enabled",
       client."IsFirstParty",
       client."GrantType",
       client."RequirePkce",
       client."ClientAuthenticationMethod",
       client."ConsentRequired",
       client."AccessTokenSeconds",
       client."IdTokenSeconds",
       client."RefreshTokenSeconds",
       ARRAY(SELECT redirect_uri."Value"
             FROM "ClientManagement"."ClientRedirectUris" redirect_uri
             WHERE redirect_uri."ClientId" = client."Id"
             ORDER BY redirect_uri."Value")              AS "RedirectUris",
       ARRAY(SELECT post_logout_redirect_uri."Value"
             FROM "ClientManagement"."ClientPostLogoutRedirectUris" post_logout_redirect_uri
             WHERE post_logout_redirect_uri."ClientId" = client."Id"
             ORDER BY post_logout_redirect_uri."Value")  AS "PostLogoutRedirectUris",
       ARRAY(SELECT cors_origin."Value"
             FROM "ClientManagement"."ClientCorsOrigins" cors_origin
             WHERE cors_origin."ClientId" = client."Id"
             ORDER BY cors_origin."Value")               AS "CorsOrigins",
       client."AssignedScopeIds",
       client."BackchannelLogoutUri"
FROM "ClientManagement"."Clients" client
WHERE client."Id" = @Id
  AND client."TenantId" = @TenantId
  AND client."IsDeleted" = false;

SELECT client_secret."Id",
       client_secret."Label",
       client_secret."ExpiresAt"
FROM "ClientManagement"."ClientSecrets" client_secret
JOIN "ClientManagement"."Clients" client
  ON client."Id" = client_secret."ClientId"
WHERE client."Id" = @Id
  AND client."TenantId" = @TenantId
  AND client."IsDeleted" = false
ORDER BY client_secret."Id";

SELECT client_signing_key."Id",
       client_signing_key."Kid",
       client_signing_key."Algorithm",
       client_signing_key."ExpiresAt"
FROM "ClientManagement"."ClientSigningKeys" client_signing_key
JOIN "ClientManagement"."Clients" client
  ON client."Id" = client_signing_key."ClientId"
WHERE client."Id" = @Id
  AND client."TenantId" = @TenantId
  AND client."IsDeleted" = false
ORDER BY client_signing_key."Id";
