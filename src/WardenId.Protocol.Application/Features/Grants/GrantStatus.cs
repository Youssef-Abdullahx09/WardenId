namespace WardenId.Protocol.Application.Features.Grants;

/// <summary>OpenIddict's authorization status. Only <see cref="Valid"/> grants can still mint tokens.</summary>
public enum GrantStatus
{
    Valid = 1,
    Revoked = 2,
    Inactive = 3,
    Rejected = 4
}
