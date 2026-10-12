namespace WardenId.Protocol.Application.Features.Authorize.SignIn;

/// <summary>Correct credentials. BlockedReason set ⇒ no session may be created.</summary>
public sealed record SignInOutcome(Guid UserId, SignInBlockReason? BlockedReason);
