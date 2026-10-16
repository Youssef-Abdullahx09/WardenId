namespace WardenId.Identity.Presentation;

/// <summary>
/// Endpoint names (<c>.WithName</c>), which become OpenAPI <c>operationId</c>s and so the
/// function names in generated clients. Unique across the Gateway; renaming one is a breaking
/// change for those clients.
/// </summary>
public static class EndpointNames
{
    public static class Users
    {
        public const string Authenticate = "AuthenticateUser";
        public const string ChangePassword = "ChangeUserPassword";
        public const string Disable = "DisableUser";
        public const string Enable = "EnableUser";
        public const string EnrollMfaFactor = "EnrollUserMfaFactor";
        public const string Get = "GetUser";
        public const string List = "ListUsers";
        public const string Register = "RegisterUser";
        public const string Create = "CreateUser";
        public const string RequestEmailVerification = "RequestUserEmailVerification";
        public const string RequestPasswordReset = "RequestUserPasswordReset";
        public const string SendPasswordReset = "SendUserPasswordReset";
        public const string AddRequiredAction = "AddUserRequiredAction";
        public const string ClearRequiredActions = "ClearUserRequiredActions";
        public const string ResetPassword = "ResetUserPassword";
        public const string RevokeMfaFactor = "RevokeUserMfaFactor";
        public const string SetAttribute = "SetUserAttribute";
        public const string RemoveAttribute = "RemoveUserAttribute";
        public const string Delete = "DeleteUser";
        public const string Restore = "RestoreUser";
        public const string Unlock = "UnlockUser";
        public const string VerifyEmail = "VerifyUserEmail";
        public const string VerifyMfaFactor = "VerifyUserMfaFactor";
    }
}
