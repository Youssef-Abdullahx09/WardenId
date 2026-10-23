-- At most one row. A disabled client is returned; a soft-deleted one is not.
SELECT client."Id",
       client."ClientId"
FROM "ClientManagement"."Clients" client
WHERE client."TenantId" = @TenantId
  AND client."Id" = @Id
  AND client."IsDeleted" = false;
