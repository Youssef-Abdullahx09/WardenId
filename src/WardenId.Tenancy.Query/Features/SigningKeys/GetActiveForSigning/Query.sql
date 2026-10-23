-- Zero rows when the tenant doesn't exist; otherwise one row whose key columns are NULL when no key is in
-- @ActiveStatus (a partial unique index allows at most one). EncryptedPrivateKey is the raw bytea, still protected.
SELECT tenant."Slug",
       signing_key."SigningKeyId",
       signing_key."Algorithm",
       signing_key."EncryptedPrivateKey"
FROM "Tenancy"."Tenants" tenant
LEFT JOIN "Tenancy"."SigningKeys" signing_key
       ON signing_key."TenantId" = tenant."Id"
      AND signing_key."Status" = @ActiveStatus
WHERE tenant."Id" = @TenantId
LIMIT 1;
