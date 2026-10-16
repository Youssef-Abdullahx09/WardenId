namespace WardenId.Tenancy.Presentation;

/// <summary>
/// Endpoint names (<c>.WithName</c>), which become OpenAPI <c>operationId</c>s and so the
/// function names in generated clients. Unique across the Gateway; renaming one is a breaking
/// change for those clients.
/// </summary>
public static class EndpointNames
{
    public static class Tenants
    {
        public const string Create = "CreateTenant";
        public const string List = "ListTenants";
        public const string Get = "GetTenant";
        public const string GetBySlug = "GetTenantBySlug";
        public const string Rename = "RenameTenant";
        public const string Suspend = "SuspendTenant";
        public const string Reactivate = "ReactivateTenant";
        public const string Archive = "ArchiveTenant";
    }
}
