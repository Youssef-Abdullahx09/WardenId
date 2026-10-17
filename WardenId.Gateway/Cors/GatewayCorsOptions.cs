namespace WardenId.Gateway.Cors;

/// <summary>
/// <c>Cors:AllowedAdminOrigins</c>: browser origins allowed to call <c>/api/**</c> cross-origin. Empty by default:
/// the admin console (Gatehouse) calls the API server-side from its BFF, so no browser origin needs it. Set it only for
/// a browser app that calls the admin API directly with a bearer token (no cookies; credentials are never allowed).
/// </summary>
public sealed class GatewayCorsOptions
{
    public const string Section = "Cors";

    public string[] AllowedAdminOrigins { get; set; } = [];
}
