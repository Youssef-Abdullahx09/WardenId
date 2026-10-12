namespace WardenId.Protocol.Application.Features.Authorize.SignIn;

public enum SignInBlockReason
{
    VerifyEmail = 1,
    ResetPassword = 2,
    AcceptTos = 3,
    EnrollMfa = 4,
    PendingVerification = 5
}
