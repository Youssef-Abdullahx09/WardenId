namespace WardenId.Tenancy.Integration.Command.Features.SigningKeys.GetActiveForSigning;

public sealed record ActiveSigningKey(Guid KeyId, string Algorithm, byte[] EncryptedPrivateKey, string Slug);
