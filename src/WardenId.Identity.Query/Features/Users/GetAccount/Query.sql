-- Two result sets: the live (not soft-deleted) user, then its enabled MFA factors (pending enrollments left out).
-- The factor statement filters by user id only: the caller ignores it when the first returns no row.
-- RemainingRecoveryCodeCount reproduces User.RemainingRecoveryCodeCount (codes whose UsedAtUtc is unset).
SELECT identity_user."Email",
       identity_user."Username",
       identity_user."TermsAcceptedAtUtc",
       (SELECT COUNT(*)::integer
        FROM "Identity"."RecoveryCodes" recovery_code
        WHERE recovery_code."UserId" = identity_user."Id"
          AND recovery_code."UsedAtUtc" IS NULL) AS "RemainingRecoveryCodeCount"
FROM "Identity"."Users" identity_user
WHERE identity_user."Id" = @UserId
  AND identity_user."TenantId" = @TenantId
  AND identity_user."IsDeleted" = false;

SELECT mfa_factor."Id",
       mfa_factor."FactorType",
       mfa_factor."EnrolledAtUtc",
       mfa_factor."LastUsedAtUtc"
FROM "Identity"."MfaFactors" mfa_factor
WHERE mfa_factor."UserId" = @UserId
  AND mfa_factor."Enabled" = true
ORDER BY mfa_factor."EnrolledAtUtc", mfa_factor."Id";
