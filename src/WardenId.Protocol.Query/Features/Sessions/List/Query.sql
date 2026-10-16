-- Two result sets: the total matching count, then one page (newest AuthTime first; Id breaks ties so pages are stable).
-- @UserId and @ClientId are optional filters (NULL = any). The filter is repeated verbatim in both statements.
SELECT COUNT(*)
FROM "Protocol"."Sessions" login_session
WHERE login_session."TenantId" = @TenantId
  AND (@UserId IS NULL OR login_session."SubjectId" = @UserId)
  AND (@ClientId IS NULL OR EXISTS (
          SELECT 1
          FROM "Protocol"."SessionAuthorizations" participant
          WHERE participant."SessionId" = login_session."Id"
            AND participant."ClientId" = @ClientId))
  AND (@IncludeEnded OR (login_session."EndedAt" IS NULL AND login_session."ExpiresAt" > @Now));

SELECT login_session."Id"        AS "Sid",
       login_session."SubjectId" AS "UserId",
       login_session."AuthTime",
       login_session."ExpiresAt",
       login_session."EndedAt",
       login_session."EndReason",
       COALESCE(
           array_agg(DISTINCT session_authorization."ClientId" ORDER BY session_authorization."ClientId")
               FILTER (WHERE session_authorization."ClientId" IS NOT NULL),
           '{}') AS "ClientIds"
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
ORDER BY login_session."AuthTime" DESC, login_session."Id" DESC
LIMIT @PageSize OFFSET @Offset;
