-- One page, each row carrying the total matching count in its trailing "RecordsTotal" column (Dapper splits on it).
-- The window count runs after GROUP BY (one row per session) and before LIMIT, so it counts every matching session,
-- not just the page or the session/client pairs. @UserId and @ClientId are optional filters (NULL = any). The page
-- is ordered by @SortOption (AuthTime or ExpiresAt), and Id breaks ties in the same direction so pages are stable.
-- Each CASE yields NULL for every row unless its field and direction are the chosen ones.
SELECT login_session."Id"        AS "Sid",
       login_session."SubjectId" AS "UserId",
       login_session."AuthTime",
       login_session."ExpiresAt",
       login_session."EndedAt",
       login_session."EndReason",
       COALESCE(
           array_agg(DISTINCT session_authorization."ClientId" ORDER BY session_authorization."ClientId")
               FILTER (WHERE session_authorization."ClientId" IS NOT NULL),
           '{}') AS "ClientIds",
       COUNT(*) OVER () AS "RecordsTotal"
FROM "Protocol"."Sessions" login_session
LEFT JOIN "Protocol"."SessionAuthorizations" session_authorization
       ON session_authorization."SessionId" = login_session."Id"
WHERE login_session."TenantId" = @TenantId
  AND (@UserId IS NULL OR login_session."SubjectId" = @UserId)
  AND (@ClientId IS NULL OR EXISTS (
          SELECT 1
          FROM "Protocol"."SessionAuthorizations" participant
          WHERE participant."SessionId" = login_session."Id"
            AND participant."ClientId" = @ClientId))
  AND (@IncludeEnded OR (login_session."EndedAt" IS NULL AND login_session."ExpiresAt" > @Now))
GROUP BY login_session."Id", login_session."SubjectId", login_session."AuthTime", login_session."ExpiresAt",
         login_session."EndedAt", login_session."EndReason"
ORDER BY CASE WHEN @SortOption = 'AuthTime' AND NOT @Descending THEN login_session."AuthTime" END,
         CASE WHEN @SortOption = 'AuthTime' AND @Descending THEN login_session."AuthTime" END DESC,
         CASE WHEN @SortOption = 'ExpiresAt' AND NOT @Descending THEN login_session."ExpiresAt" END,
         CASE WHEN @SortOption = 'ExpiresAt' AND @Descending THEN login_session."ExpiresAt" END DESC,
         CASE WHEN NOT @Descending THEN login_session."Id" END,
         CASE WHEN @Descending THEN login_session."Id" END DESC
LIMIT @PageSize OFFSET @Offset;
