namespace WardenId.Tenancy.Query.Contracts.Features.SigningKeys.GetActiveForSigning;

/// <summary>A tenant by id, with its key in <paramref name="ActiveStatus"/> (the <c>SigningKeyStatus</c> member name), if any.</summary>
public sealed record Query(Guid TenantId, string ActiveStatus);
