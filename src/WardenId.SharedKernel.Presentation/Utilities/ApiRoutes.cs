namespace WardenId.SharedKernel.Presentation.Utilities;

/// <summary>Route prefixes shared by every module's REST endpoints.</summary>
public static class ApiRoutes
{
    /// <summary>Root of the REST/admin API; OIDC protocol endpoints live outside it.</summary>
    public const string Prefix = "/api";

    /// <summary>URL-segment API version prefix (<c>/api/v{version:apiVersion}</c>).</summary>
    public const string VersionedPrefix = Prefix + "/v{version:apiVersion}";

    /// <summary>Concrete version 1 prefix, for <c>Location</c> headers.</summary>
    public const string V1Prefix = Prefix + "/v1";
}
