using Microsoft.IdentityModel.Tokens;
using WardenId.Protocol.Application.Abstractions;

namespace WardenId.Protocol.Infrastructure;

public interface ITenantSigningCredentialsFactory
{
    /// <summary>
    /// Decrypts the tenant's ES256 private key and wraps it as signing credentials. The caller owns
    /// the returned key's ECDsa (dispose it once signing is done, when signing happens synchronously).
    /// </summary>
    SigningCredentials Create(ActiveSigningKey key);
}
