-- At most one row: "TenantId" is unique.
SELECT token_policy."AccessTokenLifetime",
       token_policy."IdTokenLifetime",
       token_policy."RefreshTokenLifetime",
       token_policy."RefreshTokenAbsoluteLifetime",
       token_policy."AuthorizationCodeLifetime",
       token_policy."SessionCookieLifetime",
       token_policy."SessionAbsoluteLifetime"
FROM "Protocol"."TenantTokenPolicies" token_policy
WHERE token_policy."TenantId" = @TenantId;
