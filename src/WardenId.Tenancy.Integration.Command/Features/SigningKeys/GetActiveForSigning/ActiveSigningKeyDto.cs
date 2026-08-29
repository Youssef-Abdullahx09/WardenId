namespace WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning;

public sealed record ActiveSigningKeyDto(Guid KeyId, string Algorithm, byte[] EncryptedPrivateKey);
