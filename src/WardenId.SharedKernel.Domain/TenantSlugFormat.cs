using System.Text.RegularExpressions;

namespace WardenId.SharedKernel.Domain;

/// <summary>
/// The tenant slug format: 3–63 chars, lowercase letters/digits and hyphens, no leading/trailing hyphen.
/// Single source for Tenancy's <c>Slug</c> value object and Protocol's issuer-path checks and route constraint.
/// Case-sensitive on purpose.
/// </summary>
public static partial class TenantSlugFormat
{
    public const int MinLength = 3;
    public const int MaxLength = 63;

    /// <summary>The quantifier is <see cref="MinLength"/> − 2 to <see cref="MaxLength"/> − 2 (the edge chars are matched separately).</summary>
    public const string RegexPattern = "^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$";

    public static bool IsValid(string candidate) => Pattern().IsMatch(candidate);

    [GeneratedRegex(RegexPattern, RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
