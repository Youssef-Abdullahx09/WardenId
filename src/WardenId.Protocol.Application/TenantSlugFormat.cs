using System.Text.RegularExpressions;

namespace WardenId.Protocol.Application;

/// <summary>
/// Mirrors WardenId.Tenancy.Domain.Slug's format (3–63 chars, lowercase alnum + hyphen, no edge
/// hyphen). Protocol can't reference Tenancy.Domain; keep in sync with it and with
/// Protocol.Presentation.ProtocolRoutes.TenantIssuer by hand. Case-sensitive on purpose.
/// </summary>
public static partial class TenantSlugFormat
{
    public static bool IsValid(string candidate) => Pattern().IsMatch(candidate);

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
