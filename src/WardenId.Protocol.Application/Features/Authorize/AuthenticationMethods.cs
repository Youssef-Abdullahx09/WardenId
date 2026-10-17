namespace WardenId.Protocol.Application.Features.Authorize;

/// <summary>RFC 8176 <c>amr</c> values for how a session was signed in (cookie claim and SessionStarted audit).</summary>
public static class AuthenticationMethods
{
    public const string Password = "pwd";
    public const string OneTimePassword = "otp";
    public const string MultiFactor = "mfa";

    public static readonly IReadOnlyList<string> PasswordOnly = [Password];

    /// <summary>Password, then a code from an authenticator app (the code step or a completed enrollment).</summary>
    public static readonly IReadOnlyList<string> PasswordAndTotp = [Password, OneTimePassword, MultiFactor];

    /// <summary>Password, then a single-use recovery code: still two factors, but not an OTP.</summary>
    public static readonly IReadOnlyList<string> PasswordAndRecoveryCode = [Password, MultiFactor];
}
