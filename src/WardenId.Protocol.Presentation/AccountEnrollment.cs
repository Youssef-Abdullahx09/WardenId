using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace WardenId.Protocol.Presentation;

public enum AccountEnrollmentKind
{
    /// <summary>First authenticator app, from the account page.</summary>
    Setup = 1,

    /// <summary>A new authenticator app replacing the current one.</summary>
    Replacement = 2
}

/// <summary>An authenticator app being set up from the account page. Lives only inside the protected form value.</summary>
public sealed record AccountEnrollmentState(
    Guid TenantId,
    Guid UserId,
    AccountEnrollmentKind Kind,
    Guid FactorId,
    string Secret,
    string OtpAuthUri,
    DateTimeOffset ExpiresAt)
{
    public PendingTotpEnrollment Enrollment => new(FactorId, Secret, OtpAuthUri);
}

/// <summary>
/// Round-trips an <see cref="AccountEnrollmentState"/> through a hidden form field, so the secret and factor id are
/// never posted in the clear and never taken from the user: Data Protection with a purpose bound to the tenant and
/// the session's user (a value from another tenant or user doesn't even decrypt), the kind checked, and an expiry
/// inside the payload checked against <see cref="TimeProvider"/>.
/// </summary>
public static class AccountEnrollment
{
    public const string Purpose = "WardenId.Protocol.AccountEnrollment";

    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public static AccountEnrollmentState Create(
        HttpContext httpContext, Guid tenantId, Guid userId, AccountEnrollmentKind kind, PendingTotpEnrollment enrollment) =>
        new(tenantId, userId, kind, enrollment.FactorId, enrollment.Secret, enrollment.OtpAuthUri, Now(httpContext) + Lifetime);

    public static string Protect(HttpContext httpContext, AccountEnrollmentState state) =>
        WebEncoders.Base64UrlEncode(
            Protector(httpContext, state.TenantId, state.UserId).Protect(JsonSerializer.SerializeToUtf8Bytes(state)));

    /// <summary>The unexpired state of this kind for this tenant and user, or null (missing, tampered, foreign, expired).</summary>
    public static AccountEnrollmentState? Read(
        HttpContext httpContext, string? value, Guid tenantId, Guid userId, AccountEnrollmentKind kind)
    {
        if (string.IsNullOrEmpty(value)) return null;

        AccountEnrollmentState? state;
        try
        {
            var payload = Protector(httpContext, tenantId, userId).Unprotect(WebEncoders.Base64UrlDecode(value));
            state = JsonSerializer.Deserialize<AccountEnrollmentState>(payload);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException)
        {
            return null;
        }

        if (state is null || state.TenantId != tenantId || state.UserId != userId || state.Kind != kind) return null;
        return Now(httpContext) < state.ExpiresAt ? state : null;
    }

    private static IDataProtector Protector(HttpContext httpContext, Guid tenantId, Guid userId) =>
        httpContext.RequestServices.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(Purpose, tenantId.ToString("N"), userId.ToString("N"));

    private static DateTimeOffset Now(HttpContext httpContext) =>
        httpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
}
