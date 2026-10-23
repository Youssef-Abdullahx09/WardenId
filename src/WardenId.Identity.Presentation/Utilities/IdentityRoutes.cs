using WardenId.SharedKernel.Presentation.Utilities;

namespace WardenId.Identity.Presentation.Utilities;

/// <summary>Route templates of the Identity REST endpoints. Changing one is a breaking change for callers.</summary>
internal static class IdentityRoutes
{
    private const string AdminTenant = ApiRoutes.VersionedPrefix + "/admin/tenants/{tenantId:guid}";
    private const string Auth = ApiRoutes.VersionedPrefix + "/tenants/{tenantId:guid}/auth";

    public static class Users
    {
        public const string Collection = AdminTenant + "/users";
        public const string Item = Collection + "/{userId:guid}";
        public const string ChangePassword = Item + "/change-password";
        public const string Disable = Item + "/disable";
        public const string Enable = Item + "/enable";
        public const string Restore = Item + "/restore";
        public const string Unlock = Item + "/unlock";
        public const string Attributes = Item + "/attributes";
        public const string Attribute = Attributes + "/{key}";
        public const string RequiredActions = Item + "/required-actions";
        public const string RequiredAction = RequiredActions + "/{action}";
        public const string ResetMfa = Item + "/mfa/reset";
        public const string MfaFactor = Item + "/mfa-factors/{factorId:guid}";
        public const string SendPasswordReset = Item + "/password-reset";

        /// <summary>The <c>Location</c> of a created user (API version 1).</summary>
        public static string Location(Guid tenantId, Guid userId) => $"{ApiRoutes.V1Prefix}/admin/tenants/{tenantId}/users/{userId}";
    }

    public static class UserAuth
    {
        public const string Authenticate = Auth + "/authenticate";
        public const string Register = Auth + "/register";
        public const string RequestEmailVerification = Auth + "/email-verification/{userId:guid}/request";
        public const string VerifyEmail = Auth + "/email-verification/{userId:guid}/verify";
        public const string RequestPasswordReset = Auth + "/password-reset/request";
        public const string ConfirmPasswordReset = Auth + "/password-reset/confirm";
    }

    public static class Policies
    {
        public const string Identity = AdminTenant + "/policies/identity";
    }
}
