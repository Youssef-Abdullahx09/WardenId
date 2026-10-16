namespace WardenId.Tenancy.Presentation.Features.SigningKeys.Rotate;

/// <summary>The new signing key's id; its JWK "kid" is this id in "N" format.</summary>
public sealed record Response(Guid KeyId);
