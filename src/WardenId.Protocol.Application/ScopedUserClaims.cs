using WardenId.Identity.Integration.Command.Features.Users.GetForSignIn;

namespace WardenId.Protocol.Application;

/// <summary>
/// The user claims a set of scopes releases — one rule for the ID token and UserInfo.
/// profile → preferred_username; email → email + email_verified.
/// </summary>
public sealed record ScopedUserClaims(string? PreferredUsername, string? Email, bool? EmailVerified)
{
    public static ScopedUserClaims From(SignInUser user, IReadOnlyCollection<string> scopes)
    {
        var email = scopes.Contains(OidcScopes.Email);
        return new ScopedUserClaims(
            PreferredUsername: scopes.Contains(OidcScopes.Profile) ? user.Username : null,
            Email: email ? user.Email : null,
            // Active with no pending actions ⇒ the address was verified (VerifyEmail is what activates).
            EmailVerified: email ? true : null);
    }
}
