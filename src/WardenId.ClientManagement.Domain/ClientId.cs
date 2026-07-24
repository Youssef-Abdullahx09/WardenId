using System.Security.Cryptography;
using WardenId.SharedKernel.Domain;

namespace WardenId.ClientManagement.Domain;

public sealed class ClientId : ValueObject
{
    public const int Length = 32;

    private const string Alphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    public string Value { get; }

    private ClientId(string value) => Value = value;

    public static ClientId Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(Length);
        var chars = new char[Length];

        for (var i = 0; i < Length; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return new ClientId(new string(chars));
    }

    public static ClientId FromTrustedSource(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Client id cannot be null or empty.", nameof(value))
            : new ClientId(value);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
