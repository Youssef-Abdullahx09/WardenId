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
        Status = SigningKeyStatus.Active;
        ActivatedAt = DateTimeOffset.UtcNow;
    }
    
    public Guid SigningKeyId { get; private set; } 
    public string Algorithm { get; private set; }
    public string PublicKeyJwk { get; private set; } 
    public byte[] EncryptedPrivateKey { get; private set; } 
    public SigningKeyStatus Status { get; private set; }
    public DateTimeOffset ActivatedAt { get; private set; }
    public DateTimeOffset? RetiringSince { get; private set; }
    public DateTimeOffset? RetiredAt { get; private set; }

    /// <summary>Signs new tokens. Exactly one per tenant (partial unique index on Status).</summary>
    public bool IsActive => Status == SigningKeyStatus.Active;

    /// <summary>Served in JWKS so tokens it signed still verify: Active or Retiring.</summary>
    public bool IsPublished => Status is SigningKeyStatus.Active or SigningKeyStatus.Retiring;

    public static SigningKey CreateEs256(
        Guid signingKeyId,
        string publicKeyJwk,
        byte[] encryptedPrivateKey) =>
        new(signingKeyId, "ES256", publicKeyJwk, encryptedPrivateKey);

    /// <summary>Active -> Retiring. Only Tenant calls this, as part of a rotation.</summary>
    internal void BeginRetiring()
    {
        Status = SigningKeyStatus.Retiring;
        RetiringSince = DateTimeOffset.UtcNow;
    }

    /// <summary>Retiring -> Retired. Tenant checks the current status first.</summary>
    internal void Retire()
    {
        Status = SigningKeyStatus.Retired;
        RetiredAt = DateTimeOffset.UtcNow;
    }
}