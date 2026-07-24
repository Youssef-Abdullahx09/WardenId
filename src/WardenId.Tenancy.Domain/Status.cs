namespace WardenId.Tenancy.Domain;

/// <summary>
/// Provisioning -> Active -> Suspended <-> Active, or Active/Suspended -> Archived (terminal).
/// Enforced as transition rules on Tenant, not left to callers.
/// </summary>
public enum Status
{
    Provisioning = 1,
    Active = 2,
    Suspended = 3,
    Archived = 4
}