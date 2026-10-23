-- Two result sets: the total matching count, then one page (newest CreatedAt first; Id breaks ties so pages are stable).
-- @Enabled and @ClientType are optional filters (NULL = any). The filter is repeated verbatim in both statements.
SELECT COUNT(*)
FROM "ClientManagement"."Clients" client
WHERE client."TenantId" = @TenantId
  AND client."IsDeleted" = false
  AND (@Enabled IS NULL OR client."Enabled" = @Enabled)
  AND (@ClientType IS NULL OR client."ClientType" = @ClientType);

SELECT client."Id",
       client."ClientId",
       client."DisplayName",
       client."ClientType",
       client."Enabled",
       client."IsFirstParty",
       client."CreatedAt"
FROM "ClientManagement"."Clients" client
WHERE client."TenantId" = @TenantId
  AND client."IsDeleted" = false
  AND (@Enabled IS NULL OR client."Enabled" = @Enabled)
  AND (@ClientType IS NULL OR client."ClientType" = @ClientType)
ORDER BY client."CreatedAt" DESC, client."Id" DESC
LIMIT @PageSize OFFSET @Offset;
