using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain.Events;

public sealed record ClientSecretRotatedDomainEvent(Guid SecretId) : DomainEvent;
