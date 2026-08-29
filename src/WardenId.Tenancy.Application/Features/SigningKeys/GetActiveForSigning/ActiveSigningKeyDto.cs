namespace WardenId.Tenancy.Application.Features.SigningKeys.GetActiveForSigning;

public sealed record ActiveSigningKeyDto(Guid KeyId, string Algorithm, byte[] EncryptedPrivateKey);
