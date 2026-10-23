namespace WardenId.Identity.Application.Features.Policies.GetIdentity;

/// <summary>
/// Paths into the <see cref="IdentityPolicy"/> response, listed in its <c>overrides</c> for the fields the tenant sets.
/// Part of the response contract: renaming one is a breaking change.
/// </summary>
public static class IdentityPolicyFieldPaths
{
    public static class Password
    {
        public const string MinLength = "password.minLength";
        public const string RequireUppercase = "password.requireUppercase";
        public const string RequireDigit = "password.requireDigit";
        public const string RequireSymbol = "password.requireSymbol";
        public const string BreachCheckEnabled = "password.breachCheckEnabled";
        public const string HistoryCount = "password.historyCount";
    }

    public static class Lockout
    {
        public const string MaxFailedAttempts = "lockout.maxFailedAttempts";
        public const string LockoutDuration = "lockout.lockoutDuration";
    }

    public static class Mfa
    {
        public const string Mode = "mfa.mode";
    }

    public static class SelfRegistration
    {
        public const string Enabled = "selfRegistration.enabled";
    }

    public static class Terms
    {
        public const string Url = "terms.url";
    }
}
