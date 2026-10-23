namespace WardenId.Identity.Domain.Utilities;

/// <summary>Password hash algorithm names (<see cref="IPasswordHasher.Algorithm"/>), stored with every credential.</summary>
public static class PasswordHashAlgorithms
{
    public const string Argon2id = "Argon2id";
}
