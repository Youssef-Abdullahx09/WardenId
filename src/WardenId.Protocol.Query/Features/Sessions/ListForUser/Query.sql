SELECT login_session."Id"        AS "Sid",
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
  AND login_session."SubjectId" = @UserId
  AND (@IncludeEnded OR (login_session."EndedAt" IS NULL AND login_session."ExpiresAt" > @Now))
GROUP BY login_session."Id", login_session."AuthTime", login_session."ExpiresAt",
         login_session."EndedAt", login_session."EndReason"
ORDER BY login_session."AuthTime" DESC;
