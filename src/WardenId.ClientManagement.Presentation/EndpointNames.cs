namespace WardenId.ClientManagement.Presentation;

/// <summary>
/// Endpoint names (<c>.WithName</c>), which become OpenAPI <c>operationId</c>s and so the
/// function names in generated clients. Unique across the Gateway; renaming one is a breaking
/// change for those clients.
/// </summary>
public static class EndpointNames
{
    public static class Clients
    {
        public const string SetBackchannelLogoutUri = "SetClientBackchannelLogoutUri";
        public const string ReplaceCorsOrigins = "ReplaceClientCorsOrigins";
        public const string Create = "CreateClient";
        public const string Delete = "DeleteClient";
        public const string Get = "GetClient";
        public const string List = "ListClients";
        public const string AddPostLogoutRedirectUri = "AddClientPostLogoutRedirectUri";
        public const string RemovePostLogoutRedirectUri = "RemoveClientPostLogoutRedirectUri";
        public const string ReplacePostLogoutRedirectUris = "ReplaceClientPostLogoutRedirectUris";
        public const string AddRedirectUri = "AddClientRedirectUri";
        public const string RemoveRedirectUri = "RemoveClientRedirectUri";
        public const string ExpireSecret = "ExpireClientSecret";
        public const string IssueSecret = "IssueClientSecret";
        public const string Update = "UpdateClient";
    }
}
