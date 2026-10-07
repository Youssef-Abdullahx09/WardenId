namespace WardenId.Protocol.Application.Abstractions;

public sealed record ActiveSigningKey(Guid KeyId, string Algorithm, byte[] EncryptedPrivateKey, string Slug);
