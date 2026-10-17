namespace WardenId.Protocol.Presentation;

/// <summary>
/// Endpoint names (<c>.WithName</c>), which become OpenAPI <c>operationId</c>s and so the
/// function names in generated clients. Unique across the Gateway; renaming one is a breaking
/// change for those clients.
/// </summary>
public static class EndpointNames
{
    public static class Sessions
    {
        public const string End = "EndSession";
        public const string EndForUser = "EndUserSessions";
        public const string ListForUser = "ListUserSessions";
        public const string List = "ListSessions";
    }

    public static class Grants
    {
        public const string ListForUser = "ListUserGrants";
        public const string Revoke = "RevokeUserGrant";
        public const string RevokeForUser = "RevokeUserGrants";
        public const string RevokeForClient = "RevokeClientGrants";
    }

    public static class Policies
    {
        public const string GetTokens = "GetTokenPolicy";
        public const string UpdateTokens = "UpdateTokenPolicy";
    }
}
