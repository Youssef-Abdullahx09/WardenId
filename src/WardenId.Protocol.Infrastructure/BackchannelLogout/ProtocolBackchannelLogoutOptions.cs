namespace WardenId.Protocol.Infrastructure.BackchannelLogout;

public sealed class ProtocolBackchannelLogoutOptions
{
    /// <summary>Per-request timeout for the POST to an RP. A timeout is a transient failure, so CAP retries it.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
}
