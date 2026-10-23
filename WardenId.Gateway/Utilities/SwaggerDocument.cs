namespace WardenId.Gateway.Utilities;

/// <summary>The gateway's single Swagger/OpenAPI document and its UI.</summary>
public static class SwaggerDocument
{
    public const string Name = "v1";
    public const string Title = "WardenId";
    public const string Version = "1.0";
    public const string DisplayName = Title + " v" + Version;

    /// <summary>Swagger UI is served at <c>/swagger</c>.</summary>
    public const string RoutePrefix = "swagger";
    public const string JsonEndpoint = "/" + RoutePrefix + "/" + Name + "/swagger.json";

    public static class BearerSecurityScheme
    {
        /// <summary>Security definition id the requirement references.</summary>
        public const string Id = "Bearer";
        /// <summary>RFC 7235 HTTP authentication scheme name, as OpenAPI expects it (lowercase).</summary>
        public const string HttpScheme = "bearer";
        public const string BearerFormat = "JWT";
    }

    /// <summary>OpenAPI string formats.</summary>
    public static class Formats
    {
        public const string Date = "date";
        public const string Time = "time";
    }
}
