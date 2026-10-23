-- At most one row (TenantId + ClientId is unique). A disabled client is returned; a soft-deleted one is not.
SELECT client."Id",
       client."ClientId",
       client."ClientType",
       client."Enabled",
       client."GrantType",
       client."ClientAuthenticationMethod",
       client."AccessTokenSeconds",
       client."IdTokenSeconds",
       client."RefreshTokenSeconds",
       ARRAY(SELECT redirect_uri."Value"
             FROM "ClientManagement"."ClientRedirectUris" redirect_uri
             WHERE redirect_uri."ClientId" = client."Id"
             ORDER BY redirect_uri."Value")              AS "RedirectUris",
       client."RequirePkce",
       ARRAY(SELECT post_logout_redirect_uri."Value"
             FROM "ClientManagement"."ClientPostLogoutRedirectUris" post_logout_redirect_uri
             WHERE post_logout_redirect_uri."ClientId" = client."Id"
             ORDER BY post_logout_redirect_uri."Value")  AS "PostLogoutRedirectUris",
       client."BackchannelLogoutUri"
FROM "ClientManagement"."Clients" client
WHERE client."TenantId" = @TenantId
  AND client."ClientId" = @ClientId
  AND client."IsDeleted" = false;
