using System.Text.RegularExpressions;
using FluentResults;
using WardenId.SharedKernel.Domain;
using WardenId.SharedKernel.Domain.Utilities;

namespace WardenId.Tenancy.Domain;

/// <summary>
/// Tenant slug used in the issuer path (/tenants/{slug}/...) and in per-tenant discovery/JWKS URLs.
/// Treat as immutable once a tenant has issued its first token — changing it invalidates
/// every relying party's cached discovery document for that tenant.
/// </summary>
public sealed class Slug : ValueObject
{
    public const int MinLength = TenantSlugFormat.MinLength;
    public const int MaxLength = TenantSlugFormat.MaxLength;
 
    private const char Hyphen = '-';
    private const string HyphenSeparator = "-";
    private const string ConsecutiveHyphens = "--";

    private const string NonSlugCharactersPattern = "[^a-z0-9]+";
    private const string HyphenRunPattern = "-{2,}";

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
 
        if (candidate.Contains(ConsecutiveHyphens, StringComparison.Ordinal))
            return Result.Fail(TenantErrors.TenantSlugConsecutiveHyphens());
 
        if (!TenantSlugFormat.IsValid(candidate)) return Result.Fail(TenantErrors.TenantSlugInvalidFormat());
        
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
        var replaced = Regex.Replace(lowered, NonSlugCharactersPattern, HyphenSeparator);
        var trimmedHyphens = replaced.Trim(Hyphen);
        var collapsed = Regex.Replace(trimmedHyphens, HyphenRunPattern, HyphenSeparator);
        return collapsed.Length > MaxLength ? collapsed[..MaxLength].Trim(Hyphen) : collapsed;
    }
 
    public override string ToString() => Value;
    
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}