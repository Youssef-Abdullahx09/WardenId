SELECT tenant_policy."PasswordMinLength",
       tenant_policy."PasswordRequireUppercase",
       tenant_policy."PasswordRequireDigit",
       tenant_policy."PasswordRequireSymbol",
       tenant_policy."PasswordBreachCheckEnabled",
       tenant_policy."PasswordHistoryCount",
       tenant_policy."MaxFailedLoginAttempts",
       tenant_policy."LockoutDuration",
       tenant_policy."MfaMode",
       tenant_policy."SelfRegistrationEnabled",
       tenant_policy."TermsUrl"
FROM "Identity"."TenantPolicies" tenant_policy
WHERE tenant_policy."TenantId" = @TenantId;
