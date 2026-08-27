using System.Net;
using FluentResults;

namespace WardenId.Audit.Domain;

public sealed record ClientContext
{
    public string? IpAddress { get; }
    public string? UserAgent { get; }
    public string? Asn { get; }
    public string? GeoLocation { get; }

    private ClientContext(string? ipAddress, string? userAgent, string? asn, string? geoLocation)
    {
        IpAddress = ipAddress;
        UserAgent = userAgent;
        Asn = asn;
        GeoLocation = geoLocation;
    }

    public static Result<ClientContext> Create(
        string? ipAddress, string? userAgent = null, string? asn = null, string? geoLocation = null)
    {
        if (ipAddress is not null && !IPAddress.TryParse(ipAddress, out _))
            return Result.Fail(AuditErrors.ClientContextInvalidIpAddress());

        return Result.Ok(new ClientContext(ipAddress, userAgent, asn, geoLocation));
    }

    public static ClientContext Unknown() => new(null, null, null, null);
}
