-- One page, each row carrying the total matching count in its trailing "RecordsTotal" column (Dapper splits on it).
-- The window count is taken before LIMIT, so it counts every match, not just the page.
-- @Enabled and @ClientType are optional filters (NULL = any); @ClientType is the persisted member name.
-- The page is ordered by @SortOption (CreatedAt, ClientId or DisplayName), and Id breaks ties in the same direction
-- so pages are stable. Each CASE yields NULL for every row unless its field and direction are the chosen ones.
SELECT client."Id",
       client."ClientId",
       client."DisplayName",
       client."ClientType",
       client."Enabled",
       client."IsFirstParty",
       client."CreatedAt",
       COUNT(*) OVER () AS "RecordsTotal"
FROM "ClientManagement"."Clients" client
WHERE client."TenantId" = @TenantId
  AND client."IsDeleted" = false
  AND (@Enabled IS NULL OR client."Enabled" = @Enabled)
  AND (@ClientType IS NULL OR client."ClientType" = @ClientType)
ORDER BY CASE WHEN @SortOption = 'CreatedAt' AND NOT @Descending THEN client."CreatedAt" END,
         CASE WHEN @SortOption = 'CreatedAt' AND @Descending THEN client."CreatedAt" END DESC,
         CASE WHEN @SortOption = 'ClientId' AND NOT @Descending THEN client."ClientId" END,
         CASE WHEN @SortOption = 'ClientId' AND @Descending THEN client."ClientId" END DESC,
         CASE WHEN @SortOption = 'DisplayName' AND NOT @Descending THEN client."DisplayName" END,
         CASE WHEN @SortOption = 'DisplayName' AND @Descending THEN client."DisplayName" END DESC,
         CASE WHEN NOT @Descending THEN client."Id" END,
         CASE WHEN @Descending THEN client."Id" END DESC
LIMIT @PageSize OFFSET @Offset;
