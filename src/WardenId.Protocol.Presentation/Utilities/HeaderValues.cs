using Microsoft.Net.Http.Headers;

namespace WardenId.Protocol.Presentation.Utilities;

/// <summary>Response header values this module sets.</summary>
internal static class HeaderValues
{
    /// <summary>How long a relying party may cache discovery and JWKS.</summary>
    public static readonly TimeSpan PublicMetadataMaxAge = TimeSpan.FromMinutes(5);

    /// <summary><c>public, max-age=300</c>: discovery and JWKS.</summary>
    public static readonly string PublicMetadataCacheControl =
        $"{CacheControlHeaderValue.PublicString}, {CacheControlHeaderValue.MaxAgeString}={(int)PublicMetadataMaxAge.TotalSeconds}";

    /// <summary>X-Frame-Options: never framed.</summary>
    public const string FrameOptionsDeny = "DENY";

    /// <summary>Content-Security-Policy: never framed (the CSP successor of X-Frame-Options).</summary>
    public const string FrameAncestorsNone = "frame-ancestors 'none'";
}
