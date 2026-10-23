namespace WardenId.Protocol.Query.Contracts.Features.Sessions.GetLive;

/// <summary>
/// One session of a tenant by its sid, whatever its state: whether it is still live (not ended, not expired) is
/// decided by the caller.
/// </summary>
public sealed record Query(Guid TenantId, Guid Sid);
