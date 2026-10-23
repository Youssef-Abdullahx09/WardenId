-- Two result sets: the tenant (zero or one row), then its signing keys' public metadata, newest first
-- (Id breaks ties). Key material (PublicKeyJwk, EncryptedPrivateKey) is deliberately not selected.
SELECT tenant."Id",
       tenant."Slug",
       tenant."DisplayName",
       tenant."Status",
       tenant."SuspendedAtUtc",
       tenant."SuspendedReason",
       tenant."CreatedAt"
FROM "Tenancy"."Tenants" tenant
WHERE tenant."Id" = @TenantId;

SELECT signing_key."SigningKeyId",
       signing_key."Algorithm",
       signing_key."Status",
       signing_key."CreatedAt",
       signing_key."ActivatedAt",
       signing_key."RetiringSince",
       signing_key."RetiredAt"
FROM "Tenancy"."SigningKeys" signing_key
WHERE signing_key."TenantId" = @TenantId
ORDER BY signing_key."CreatedAt" DESC, signing_key."Id" DESC;
