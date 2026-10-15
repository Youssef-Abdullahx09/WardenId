namespace WardenId.Protocol.Application.Features.BackchannelLogout;

public enum DeliveryOutcome
{
    /// <summary>The RP answered 2xx.</summary>
    Delivered = 1,

    /// <summary>Nothing to send: the client is deleted, disabled or has no back-channel logout URI.</summary>
    Skipped = 2,

    /// <summary>The RP refused (3xx/4xx) or the URI is not allowed. A retry cannot help.</summary>
    Rejected = 3,

    /// <summary>Timeout, network error, 5xx or no signing key. The CAP subscriber throws so CAP retries.</summary>
    TransientFailure = 4
}
