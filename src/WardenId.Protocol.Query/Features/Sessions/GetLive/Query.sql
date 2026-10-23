-- The session regardless of state; liveness (not ended, ExpiresAt in the future) is checked by the caller.
SELECT login_session."Id"        AS "Sid",
       login_session."SubjectId" AS "UserId",
       login_session."AuthTime",
       login_session."ExpiresAt",
       login_session."EndedAt"
FROM "Protocol"."Sessions" login_session
WHERE login_session."Id" = @Sid
  AND login_session."TenantId" = @TenantId;
