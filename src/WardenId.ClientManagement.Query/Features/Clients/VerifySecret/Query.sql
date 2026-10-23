-- Two result sets: the client row (no row = unknown client), then every secret hash of it, expired ones included —
-- eligibility and expiry are checked by the caller, alongside the constant-time hash comparison.
SELECT client."Enabled",
       client."ClientType",
       client."ClientAuthenticationMethod"
FROM "ClientManagement"."Clients" client
WHERE client."TenantId" = @TenantId
  AND client."ClientId" = @ClientId
  AND client."IsDeleted" = false;

SELECT client_secret."HashedValue",
       client_secret."ExpiresAt"
FROM "ClientManagement"."ClientSecrets" client_secret
JOIN "ClientManagement"."Clients" client
  ON client."Id" = client_secret."ClientId"
WHERE client."TenantId" = @TenantId
  AND client."ClientId" = @ClientId
  AND client."IsDeleted" = false;
