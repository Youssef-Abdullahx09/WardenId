using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using WardenId.Protocol.Application.Features.Authorize.SignIn;

namespace WardenId.Protocol.Presentation;

/// <summary>An authenticator app being set up: its secret rides in the protected ticket, never in a form field.</summary>
public sealed record PendingTotpEnrollment(Guid FactorId, string Secret, string OtpAuthUri);

/// <summary>
/// A sign-in whose password is checked but whose MFA steps aren't done: no session exists yet. The user id comes
/// only from here, never from the form.
/// </summary>
/// <param name="Steps">Steps left, next first.</param>
/// <param name="ReturnUrl">Already checked with <c>AuthorizeReturnUrl.IsAllowed</c>; checked again before use.</param>
/// <param name="Amr">How the user has authenticated so far.</param>
public sealed record PendingSignInTicket(
    Guid TenantId,
    Guid UserId,
    IReadOnlyList<SignInStep> Steps,
    string? ReturnUrl,
    IReadOnlyList<string> Amr,
    DateTimeOffset ExpiresAt,
    PendingTotpEnrollment? Enrollment = null)
{
    public SignInStep? NextStep => Steps.Count > 0 ? Steps[0] : null;
}

/// <summary>
/// Carries a <see cref="PendingSignInTicket"/> between the login page and the MFA step pages in a short-lived
/// cookie: Data Protection with a per-tenant purpose (a ticket from one tenant doesn't even decrypt for another),
/// an expiry inside the payload checked against <see cref="TimeProvider"/>, HttpOnly, Secure, SameSite=Lax and
/// scoped to /tenants/{slug} like the session cookie.
/// </summary>
public static class PendingSignIn
{
    public const string CookieName = "WardenId.PendingSignIn";
    public const string Purpose = "WardenId.Protocol.PendingSignIn";

    /// <summary>From the password check, and again from the start of each later step.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public static PendingSignInTicket Create(
        HttpContext httpContext, Guid tenantId, Guid userId, IReadOnlyList<SignInStep> steps, string? returnUrl,
        IReadOnlyList<string> amr) =>
        new(tenantId, userId, steps, returnUrl, amr, ExpiresFromNow(httpContext));

    /// <summary>The ticket with the next step done: <paramref name="amr"/> now applies and the clock restarts.</summary>
    public static PendingSignInTicket Advance(HttpContext httpContext, PendingSignInTicket ticket, IReadOnlyList<string> amr) =>
        ticket with
        {
            Steps = ticket.Steps.Skip(1).ToArray(),
            Amr = amr,
            Enrollment = null,
            ExpiresAt = ExpiresFromNow(httpContext)
        };

    /// <summary>The ticket with an enrollment started: the clock restarts so there is time to set up the app.</summary>
    public static PendingSignInTicket WithEnrollment(
        HttpContext httpContext, PendingSignInTicket ticket, PendingTotpEnrollment enrollment) =>
        ticket with { Enrollment = enrollment, ExpiresAt = ExpiresFromNow(httpContext) };

    public static void Issue(HttpContext httpContext, PendingSignInTicket ticket)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(ticket);
        var value = WebEncoders.Base64UrlEncode(Protector(httpContext, ticket.TenantId).Protect(payload));
        httpContext.Response.Cookies.Append(CookieName, value, CookieOptions(httpContext, ticket.ExpiresAt - Now(httpContext)));
    }

    /// <summary>The unexpired ticket for <paramref name="tenantId"/>, or null (missing, tampered, foreign, expired).</summary>
    public static PendingSignInTicket? Read(HttpContext httpContext, Guid tenantId)
    {
        if (!httpContext.Request.Cookies.TryGetValue(CookieName, out var value) || string.IsNullOrEmpty(value))
            return null;

        PendingSignInTicket? ticket;
        try
        {
            var payload = Protector(httpContext, tenantId).Unprotect(WebEncoders.Base64UrlDecode(value));
            ticket = JsonSerializer.Deserialize<PendingSignInTicket>(payload);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException)
        {
            return null;
        }

        if (ticket is null || ticket.TenantId != tenantId) return null;
        return Now(httpContext) < ticket.ExpiresAt ? ticket : null;
    }

    public static void Clear(HttpContext httpContext)
    {
        if (!httpContext.Request.Cookies.ContainsKey(CookieName)) return;
        httpContext.Response.Cookies.Delete(CookieName, CookieOptions(httpContext, maxAge: null));
    }

    private static IDataProtector Protector(HttpContext httpContext, Guid tenantId) =>
        httpContext.RequestServices.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(Purpose, tenantId.ToString("N"));

    private static DateTimeOffset Now(HttpContext httpContext) =>
        httpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();

    private static DateTimeOffset ExpiresFromNow(HttpContext httpContext) => Now(httpContext) + Lifetime;

    // Max-Age, not Expires: relative, so the browser's clock (or a test's fake one) can't keep or drop it early.
    private static CookieOptions CookieOptions(HttpContext httpContext, TimeSpan? maxAge) => new()
    {
        Path = TenantPathCookieBuilder.TenantPath(httpContext.Request.Path) ?? "/",
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        MaxAge = maxAge
    };
}
