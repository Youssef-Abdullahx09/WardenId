-- Three result sets: the live (not soft-deleted) user, then its attributes, then its MFA factors.
-- The child statements filter by user id only: the caller ignores them when the first returns no row.
-- RemainingRecoveryCodeCount reproduces User.RemainingRecoveryCodeCount (codes whose UsedAtUtc is unset).
SELECT identity_user."Id",
       identity_user."Email",
       identity_user."Username",
       identity_user."FirstName",
       identity_user."LastName",
       identity_user."Status",
       identity_user."FailedAttemptCount",
       identity_user."LockedUntilUtc",
       identity_user."PendingActions",
       identity_user."TermsAcceptedAtUtc",
       identity_user."CreatedAt",
       (SELECT COUNT(*)::integer
        FROM "Identity"."RecoveryCodes" recovery_code
        WHERE recovery_code."UserId" = identity_user."Id"
          AND recovery_code."UsedAtUtc" IS NULL) AS "RemainingRecoveryCodeCount"
FROM "Identity"."Users" identity_user
WHERE identity_user."Id" = @UserId
  AND identity_user."TenantId" = @TenantId
  AND identity_user."IsDeleted" = false;

SELECT user_attribute."Key",
       user_attribute."Value",
       user_attribute."ValueType"
FROM "Identity"."UserAttributes" user_attribute
WHERE user_attribute."UserId" = @UserId
ORDER BY user_attribute."Id";

SELECT mfa_factor."Id",
       mfa_factor."FactorType",
       mfa_factor."Enabled",
       mfa_factor."EnrolledAtUtc",
       mfa_factor."VerifiedAtUtc",
       mfa_factor."LastUsedAtUtc"
FROM "Identity"."MfaFactors" mfa_factor
WHERE mfa_factor."UserId" = @UserId
ORDER BY mfa_factor."EnrolledAtUtc", mfa_factor."Id";
