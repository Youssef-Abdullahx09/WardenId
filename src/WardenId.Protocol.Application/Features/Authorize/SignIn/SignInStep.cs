namespace WardenId.Protocol.Application.Features.Authorize.SignIn;

/// <summary>
/// What the user must still do after the password before a session may start. The handler orders them
/// (SecondFactor, AcceptTerms, EnrollTotp); the values only identify a step in the pending ticket.
/// </summary>
public enum SignInStep
{
    /// <summary>A TOTP or recovery code from an enabled factor.</summary>
    SecondFactor = 1,

    /// <summary>Set up an authenticator app (the EnrollMfa required action).</summary>
    EnrollTotp = 2,

    /// <summary>Accept the tenant's terms of service (the AcceptTos required action).</summary>
    AcceptTerms = 3
}
