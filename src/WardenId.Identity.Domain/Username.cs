using System.Text.RegularExpressions;
using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class Username : ValueObject
{
    public const int MinLength = 3;
    public const int MaxLength = 64;

    private static readonly Regex FormatRegex = new(@"^[a-zA-Z0-9._-]+$", RegexOptions.Compiled);

    public string Value { get; }

    private Username(string value) => Value = value;

    public static Result<Username> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Result.Fail(UserErrors.UsernameRequired());

        var candidate = raw.Trim();

        if (candidate.Length is < MinLength or > MaxLength) return Result.Fail(UserErrors.UsernameInvalidLength());

        if (!FormatRegex.IsMatch(candidate)) return Result.Fail(UserErrors.UsernameInvalidFormat());

        return Result.Ok(new Username(candidate));
    }

    public static Username FromTrustedSource(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Username cannot be null or empty.", nameof(value))
            : new Username(value);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
