SELECT identity_user."Id",
       identity_user."Email",
       identity_user."Username",
       identity_user."FirstName",
       identity_user."LastName",
       identity_user."Status",
       identity_user."PendingActions"
FROM "Identity"."Users" identity_user
WHERE identity_user."Id" = @UserId
  AND identity_user."TenantId" = @TenantId
  AND identity_user."IsDeleted" = false;
