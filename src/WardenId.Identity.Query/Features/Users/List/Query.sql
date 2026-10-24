-- One page, each row carrying the total matching count in its trailing "RecordsTotal" column (Dapper splits on it).
-- The window count is taken before LIMIT, so it counts every match, not just the page.
-- @Status and @SearchPattern are optional filters (NULL = any). @SearchPattern is a lowercased LIKE prefix pattern
-- whose own \, % and _ are escaped with \. Emails are stored lowercased; usernames and names keep their case, so
-- only they are lowered. A name matches by its first name, its last name, or "first last" (so a full-name prefix
-- finds it). The page is ordered by @SortOption (CreatedAt, Email, Username, FirstName or LastName), and Id breaks
-- ties in the same direction so pages are stable. Each CASE yields NULL for every row unless its field and direction
-- are the chosen ones. Users without the sorted name come last in either direction.
SELECT identity_user."Id",
       identity_user."Email",
       identity_user."Username",
       identity_user."FirstName",
       identity_user."LastName",
       identity_user."Status",
       identity_user."CreatedAt",
       COUNT(*) OVER () AS "RecordsTotal"
FROM "Identity"."Users" identity_user
WHERE identity_user."TenantId" = @TenantId
  AND (@IncludeDeleted OR identity_user."IsDeleted" = false)
  AND (@Status IS NULL OR identity_user."Status" = @Status)
  AND (@SearchPattern IS NULL
       OR identity_user."Email" LIKE @SearchPattern ESCAPE '\'
       OR lower(identity_user."Username") LIKE @SearchPattern ESCAPE '\'
       OR lower(identity_user."FirstName") LIKE @SearchPattern ESCAPE '\'
       OR lower(identity_user."LastName") LIKE @SearchPattern ESCAPE '\'
       OR lower(concat_ws(' ', identity_user."FirstName", identity_user."LastName")) LIKE @SearchPattern ESCAPE '\')
ORDER BY CASE WHEN @SortOption = 'CreatedAt' AND NOT @Descending THEN identity_user."CreatedAt" END,
         CASE WHEN @SortOption = 'CreatedAt' AND @Descending THEN identity_user."CreatedAt" END DESC,
         CASE WHEN @SortOption = 'Email' AND NOT @Descending THEN identity_user."Email" END,
         CASE WHEN @SortOption = 'Email' AND @Descending THEN identity_user."Email" END DESC,
         CASE WHEN @SortOption = 'Username' AND NOT @Descending THEN identity_user."Username" END,
         CASE WHEN @SortOption = 'Username' AND @Descending THEN identity_user."Username" END DESC,
         CASE WHEN @SortOption = 'FirstName' AND NOT @Descending THEN identity_user."FirstName" END NULLS LAST,
         CASE WHEN @SortOption = 'FirstName' AND @Descending THEN identity_user."FirstName" END DESC NULLS LAST,
         CASE WHEN @SortOption = 'LastName' AND NOT @Descending THEN identity_user."LastName" END NULLS LAST,
         CASE WHEN @SortOption = 'LastName' AND @Descending THEN identity_user."LastName" END DESC NULLS LAST,
         CASE WHEN NOT @Descending THEN identity_user."Id" END,
         CASE WHEN @Descending THEN identity_user."Id" END DESC
LIMIT @PageSize OFFSET @Offset;
