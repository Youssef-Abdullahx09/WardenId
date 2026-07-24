using WardenId.SharedKernel.Domain;

namespace WardenId.Tenancy.Domain;

public sealed class SigningKey : Entity
{
    // EF Core materialization only
    private SigningKey() { }
    
    private SigningKey(
        Guid signingKeyId,
        string algorithm, 
        string publicKeyJwk,
        byte[] encryptedPrivateKey)
    {
        SigningKeyId =  signingKeyId;
        Algorithm = algorithm;
        PublicKeyJwk = publicKeyJwk;
        EncryptedPrivateKey = encryptedPrivateKey;
    }
    
    public Guid SigningKeyId { get; private set; } 
    public string Algorithm { get; private set; }
    public string PublicKeyJwk { get; private set; } 
    public byte[] EncryptedPrivateKey { get; private set; } 
    public bool IsActive { get; private set; }
 
    public static SigningKey CreateEs256(
        Guid signingKeyId,
        string publicKeyJwk,
        byte[] encryptedPrivateKey) =>
        new(signingKeyId, "ES256", publicKeyJwk, encryptedPrivateKey);
}