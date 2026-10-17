namespace WardenId.Protocol.Application.Features.Authorize.SignIn;

/// <summary>What the user must still do after the password, in this order, before a session may start.</summary>
public enum SignInStep
{
    /// <summary>A TOTP or recovery code from an enabled factor.</summary>
    SecondFactor = 1,

    /// <summary>Set up an authenticator app (the EnrollMfa required action).</summary>
    EnrollTotp = 2
}
