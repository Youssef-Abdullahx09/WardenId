namespace WardenId.Protocol.Application.Features.Authorize.SignIn;

/// <summary>Why a user with the right password still can't sign in. MFA enrollment is a step, not a block.</summary>
public enum SignInBlockReason
{
    VerifyEmail = 1,
    ResetPassword = 2,
    AcceptTos = 3,
    PendingVerification = 5
}
