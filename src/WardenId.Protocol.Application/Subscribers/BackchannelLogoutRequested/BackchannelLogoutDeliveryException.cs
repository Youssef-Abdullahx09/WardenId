namespace WardenId.Protocol.Application.Subscribers.BackchannelLogoutRequested;

/// <summary>Thrown only to make CAP retry a transiently failed delivery (CapPublisher:RetryCount / RetryInterval).</summary>
public sealed class BackchannelLogoutDeliveryException(Guid tenantId, string clientId, Guid sid)
    : Exception($"Back-channel logout to client '{clientId}' (tenant {tenantId}, session {sid}) failed transiently.");
