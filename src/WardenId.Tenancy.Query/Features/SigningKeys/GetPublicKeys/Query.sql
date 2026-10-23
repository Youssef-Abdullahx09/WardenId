-- Two result sets: the tenant (zero or one row; Slug is unique and compared exactly), then every one of its keys'
-- status and public JWK. Status eligibility (tenant and key) is decided by the caller. Never selects private key material.
SELECT tenant."Id",
       tenant."Slug",
       tenant."Status"
FROM "Tenancy"."Tenants" tenant
WHERE tenant."Slug" = @Slug;

SELECT signing_key."Status",
       signing_key."PublicKeyJwk"::text AS "PublicKeyJwk"
FROM "Tenancy"."SigningKeys" signing_key
JOIN "Tenancy"."Tenants" tenant ON tenant."Id" = signing_key."TenantId"
WHERE tenant."Slug" = @Slug
ORDER BY signing_key."CreatedAt" DESC, signing_key."Id" DESC;
