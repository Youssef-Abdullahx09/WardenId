using WardenId.Identity.Integration.Command.Features.Users.GetForSignIn;
using WardenId.Protocol.Application.Utilities;

namespace WardenId.Protocol.Application;

/// <summary>
/// The user claims a set of scopes releases — one rule for the ID token and UserInfo.
/// profile → preferred_username, plus given_name, family_name and name for the names the user has
/// (name is "given family", or whichever of the two is set); email → email + email_verified.
/// </summary>
public sealed record ScopedUserClaims(
    string? PreferredUsername,
    string? GivenName,
    string? FamilyName,
    string? Name,
    string? Email,
    bool? EmailVerified)
{
    public static ScopedUserClaims From(SignInUser user, IReadOnlyCollection<string> scopes)
    {
        var profile = scopes.Contains(OidcScopes.Profile);
        var email = scopes.Contains(OidcScopes.Email);
        var fullName = string.Join(' ', new[] { user.FirstName, user.LastName }.OfType<string>());
        return new ScopedUserClaims(
            PreferredUsername: profile ? user.Username : null,
            GivenName: profile ? user.FirstName : null,
            FamilyName: profile ? user.LastName : null,
            Name: profile && fullName.Length > 0 ? fullName : null,
            Email: email ? user.Email : null,
            // Active with no pending actions ⇒ the address was verified (VerifyEmail is what activates).
            EmailVerified: email ? true : null);
    }
}
