namespace WardenId.Protocol.Presentation;

internal static class ProtocolRoutes
{
    // Issuer path root — unversioned on purpose: an issuer change breaks every relying party.
    // Slug pattern mirrors WardenId.Tenancy.Domain.Slug (3–63 chars, lowercase alnum + hyphen,
    // no edge hyphen); Protocol can't reference Tenancy.Domain, so keep the two in sync by hand.
    // "{{1,61}}" is the route-template escape for the regex quantifier {1,61}. Route regex
    // constraints are case-insensitive — mixed-case slugs pass routing and 404 at lookup.
    public const string TenantIssuer = "/tenants/{slug:regex(^[a-z0-9][a-z0-9-]{{1,61}}[a-z0-9]$)}";
}
