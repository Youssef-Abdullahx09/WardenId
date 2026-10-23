-- Zero or one row: Slug is unique and compared exactly. Status eligibility is decided by the caller.
SELECT tenant."Id",
       tenant."Slug",
       tenant."DisplayName",
       tenant."Status"
FROM "Tenancy"."Tenants" tenant
WHERE tenant."Slug" = @Slug;
