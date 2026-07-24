using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain.Events;

public sealed record ClientSecretExpiredDomainEvent(Guid SecretId) : DomainEvent;
