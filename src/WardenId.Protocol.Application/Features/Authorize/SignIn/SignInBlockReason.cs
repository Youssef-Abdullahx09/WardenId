namespace WardenId.Protocol.Application.Features.Authorize.SignIn;

/// <summary>
/// Why a user with the right password still can't sign in. MFA enrollment and accepting the terms are steps,
/// not blocks.
/// </summary>
public enum SignInBlockReason
{
    VerifyEmail = 1,
    ResetPassword = 2,
    PendingVerification = 5
}
