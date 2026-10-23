-- Two result sets: the total matching count, then one page (newest CreatedAt first; Id breaks ties so pages are stable).
-- @Status is an optional filter (NULL = any). The filter is repeated verbatim in both statements.
SELECT COUNT(*)
FROM "Tenancy"."Tenants" tenant
WHERE (@Status IS NULL OR tenant."Status" = @Status);

SELECT tenant."Id",
       tenant."Slug",
       tenant."DisplayName",
       tenant."Status",
       tenant."CreatedAt"
FROM "Tenancy"."Tenants" tenant
WHERE (@Status IS NULL OR tenant."Status" = @Status)
ORDER BY tenant."CreatedAt" DESC, tenant."Id" DESC
LIMIT @PageSize OFFSET @Offset;
