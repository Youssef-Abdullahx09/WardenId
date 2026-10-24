namespace WardenId.Tenancy.Query.Contracts.Features.Tenants;

// Read-side mirror of WardenId.Tenancy.Domain.Status. Query.Contracts must not depend on Domain, so the read model
// declares its own copy; member names must match the domain enum's (they're what's persisted and parsed).

public enum Status
{
    Provisioning = 1,
    Active = 2,
    Suspended = 3,
    Archived = 4
}
