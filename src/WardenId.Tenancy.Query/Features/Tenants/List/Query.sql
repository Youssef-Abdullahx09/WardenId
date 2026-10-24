-- One page, each row carrying the total matching count in its trailing "RecordsTotal" column (Dapper splits on it).
-- The window count is taken before LIMIT, so it counts every match, not just the page.
-- @Status is an optional filter (NULL = any). The page is ordered by @SortOption (CreatedAt, Slug or DisplayName),
-- and Id breaks ties in the same direction so pages are stable. Each CASE yields NULL for every row unless its field
-- and direction are the chosen ones.
SELECT tenant."Id",
       tenant."Slug",
       tenant."DisplayName",
       tenant."Status",
       tenant."CreatedAt",
       COUNT(*) OVER () AS "RecordsTotal"
FROM "Tenancy"."Tenants" tenant
WHERE (@Status IS NULL OR tenant."Status" = @Status)
ORDER BY CASE WHEN @SortOption = 'CreatedAt' AND NOT @Descending THEN tenant."CreatedAt" END,
         CASE WHEN @SortOption = 'CreatedAt' AND @Descending THEN tenant."CreatedAt" END DESC,
         CASE WHEN @SortOption = 'Slug' AND NOT @Descending THEN tenant."Slug" END,
         CASE WHEN @SortOption = 'Slug' AND @Descending THEN tenant."Slug" END DESC,
         CASE WHEN @SortOption = 'DisplayName' AND NOT @Descending THEN tenant."DisplayName" END,
         CASE WHEN @SortOption = 'DisplayName' AND @Descending THEN tenant."DisplayName" END DESC,
         CASE WHEN NOT @Descending THEN tenant."Id" END,
         CASE WHEN @Descending THEN tenant."Id" END DESC
LIMIT @PageSize OFFSET @Offset;
