namespace WardenId.Protocol.Domain.Sessions;

/// <summary>Serializable copy of a <see cref="SessionAuthorization"/> carried by <c>SessionEndedDomainEvent</c>.</summary>
public sealed record SessionAuthorizationSnapshot(string AuthorizationId, string ClientId, bool OfflineAccess);
