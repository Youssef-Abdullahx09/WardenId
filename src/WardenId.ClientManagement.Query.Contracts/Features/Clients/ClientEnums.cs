namespace WardenId.ClientManagement.Query.Contracts.Features.Clients;

// Read-side mirror of WardenId.ClientManagement.Domain.ClientType. Query.Contracts must not depend on Domain, so the
// read model declares its own copy; member names must match the domain enum's (they're what's persisted and parsed).

public enum ClientType
{
    Confidential = 1,
    Public = 2
}
