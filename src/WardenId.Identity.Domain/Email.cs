using System.Text.RegularExpressions;
using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

public sealed class Email : ValueObject
{
    public const int MaxLength = 254;

    private const string FormatPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";

    private static readonly Regex FormatRegex = new(FormatPattern, RegexOptions.Compiled);

    public string Value { get; }

    private Email(string value) => Value = value;

    public static Result<Email> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Result.Fail(UserErrors.EmailRequired());

        var candidate = raw.Trim().ToLowerInvariant();

        if (candidate.Length > MaxLength) return Result.Fail(UserErrors.EmailTooLong());

        if (!FormatRegex.IsMatch(candidate)) return Result.Fail(UserErrors.EmailInvalidFormat());

        return Result.Ok(new Email(candidate));
    }

    public static Email FromTrustedSource(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Email cannot be null or empty.", nameof(value))
            : new Email(value);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
