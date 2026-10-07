using System.Text.RegularExpressions;
using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Tenancy.Domain;

/// <summary>
/// Tenant slug used in the issuer path (/tenants/{slug}/...) and in per-tenant discovery/JWKS URLs.
/// Treat as immutable once a tenant has issued its first token — changing it invalidates
/// every relying party's cached discovery document for that tenant.
/// </summary>
public sealed class Slug : ValueObject
{
    public const int MinLength = 3;
    public const int MaxLength = 63;
 
    // lowercase letters/digits, single internal hyphens, no leading/trailing hyphen
    private static readonly Regex FormatRegex =
        new(@"^[a-z0-9]([a-z0-9-]*[a-z0-9])?$", RegexOptions.Compiled);
 
    // Reserved so a tenant slug can never collide with a system route or a future one.
    private static readonly HashSet<string> ReservedSlugs = new(StringComparer.Ordinal)
    {
        "admin", "api", "app", "www",
        "t", "tenant", "tenants",
        "health", "healthz", "ready", "live", "metrics",
        "well-known", "auth", "login", "logout", "account",
        "console", "static", "assets", "system", "root", "internal"
    };
 
    public string Value { get; }
 
    private Slug(string value) => Value = value;
 
    /// <summary>
    /// Validates and creates a TenantSlug from external/user-supplied input.
    /// Use this from command handlers — never from EF Core materialization.
    /// </summary>
    public static Result<Slug> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Result.Fail(TenantErrors.TenantSlugEmpty());
 
        var candidate = raw.Trim();
 
        if (candidate.Length is < MinLength or > MaxLength)
            return Result.Fail(TenantErrors.TenantSlugInvalidLength(MinLength, MaxLength));
 
        if (!string.Equals(candidate, candidate.ToLowerInvariant(), StringComparison.Ordinal))
            return Result.Fail(TenantErrors.TenantSlugMustBeLowercase());
 
        if (candidate.Contains("--", StringComparison.Ordinal))
            return Result.Fail(TenantErrors.TenantSlugConsecutiveHyphens());
 
        if (!FormatRegex.IsMatch(candidate)) return Result.Fail(TenantErrors.TenantSlugInvalidFormat());
        
        if (ReservedSlugs.Contains(candidate)) return Result.Fail(TenantErrors.TenantSlugReserved(candidate));
 
        return Result.Ok(new Slug(candidate));
    }
 
    /// <summary>
    /// Rehydrates a slug already known to be valid — EF Core value conversion, seed replay,
    /// or other trusted internal sources only. Guards the null/empty invariant but skips
    /// format/reserved-word checks. Never call this with unvalidated external input.
    /// </summary>
    public static Slug FromTrustedSource(string value)
    {
        return string.IsNullOrWhiteSpace(value) ?
            throw new ArgumentException("Tenant slug cannot be null or empty.", nameof(value)) : new Slug(value);
    }
 
    /// <summary>
    /// Suggests a slug candidate from a display name (e.g. admin UI "create tenant" form).
    /// The result is NOT guaranteed valid or unique — always pass it through Create()
    /// and check uniqueness before persisting.
    /// </summary>
    public static string Slugify(string displayName)
    {
        var lowered = displayName.Trim().ToLowerInvariant();
        var replaced = Regex.Replace(lowered, @"[^a-z0-9]+", "-");
        var trimmedHyphens = replaced.Trim('-');
        var collapsed = Regex.Replace(trimmedHyphens, "-{2,}", "-");
        return collapsed.Length > MaxLength ? collapsed[..MaxLength].Trim('-') : collapsed;
    }
 
    public override string ToString() => Value;
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}