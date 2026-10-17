namespace WardenId.Protocol.Application.Features.Authorize.SignIn;

/// <summary>
/// Correct credentials. BlockedReason set ⇒ no session may be created. Otherwise a session may start once every
/// step in <paramref name="PendingSteps"/> is done (empty ⇒ right away).
/// </summary>
public sealed record SignInOutcome(Guid UserId, SignInBlockReason? BlockedReason, IReadOnlyList<SignInStep> PendingSteps);
