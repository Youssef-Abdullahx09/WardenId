-- Two result sets: the total matching count, then one page. The filter is repeated verbatim in both statements.
-- @Status and @SearchPattern are optional filters (NULL = any). @SearchPattern is a LIKE prefix pattern whose own
-- \, % and _ are escaped with \. Emails are stored lowercased; usernames keep their case, so only they are lowered.
-- The page is ordered by @SortField (CreatedAt, Email or Username), and Id breaks ties in the same direction so
-- pages are stable. Each CASE yields NULL for every row unless its field and direction are the chosen ones.
SELECT COUNT(*)
FROM "Identity"."Users" identity_user
WHERE identity_user."TenantId" = @TenantId
  AND (@IncludeDeleted OR identity_user."IsDeleted" = false)
  AND (@Status IS NULL OR identity_user."Status" = @Status)
  AND (@SearchPattern IS NULL
       OR identity_user."Email" LIKE @SearchPattern ESCAPE '\'
       OR lower(identity_user."Username") LIKE @SearchPattern ESCAPE '\');

SELECT identity_user."Id",
       identity_user."Email",
       identity_user."Username",
       identity_user."Status",
       identity_user."CreatedAt"
FROM "Identity"."Users" identity_user
WHERE identity_user."TenantId" = @TenantId
  AND (@IncludeDeleted OR identity_user."IsDeleted" = false)
  AND (@Status IS NULL OR identity_user."Status" = @Status)
  AND (@SearchPattern IS NULL
       OR identity_user."Email" LIKE @SearchPattern ESCAPE '\'
       OR lower(identity_user."Username") LIKE @SearchPattern ESCAPE '\')
ORDER BY CASE WHEN @SortField = 'CreatedAt' AND NOT @Descending THEN identity_user."CreatedAt" END,
         CASE WHEN @SortField = 'CreatedAt' AND @Descending THEN identity_user."CreatedAt" END DESC,
         CASE WHEN @SortField = 'Email' AND NOT @Descending THEN identity_user."Email" END,
         CASE WHEN @SortField = 'Email' AND @Descending THEN identity_user."Email" END DESC,
         CASE WHEN @SortField = 'Username' AND NOT @Descending THEN identity_user."Username" END,
         CASE WHEN @SortField = 'Username' AND @Descending THEN identity_user."Username" END DESC,
         CASE WHEN NOT @Descending THEN identity_user."Id" END,
         CASE WHEN @Descending THEN identity_user."Id" END DESC
LIMIT @PageSize OFFSET @Offset;
