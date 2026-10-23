-- Disabled and soft-deleted clients are left out. Final (ordinal) ordering is the caller's.
SELECT DISTINCT cors_origin."Value"
FROM "ClientManagement"."ClientCorsOrigins" cors_origin
JOIN "ClientManagement"."Clients" client
  ON client."Id" = cors_origin."ClientId"
WHERE client."TenantId" = @TenantId
  AND client."Enabled" = true
  AND client."IsDeleted" = false;
