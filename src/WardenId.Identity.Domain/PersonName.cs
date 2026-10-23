using FluentResults;
using WardenId.SharedKernel.Domain;

namespace WardenId.Identity.Domain;

/// <summary>
/// A user's first or last name, as they write it: trimmed, any characters, case kept.
/// Both names are optional on a user, so an absent name is null rather than a PersonName.
/// </summary>
public sealed class PersonName : ValueObject
{
    public const int MaxLength = 100;

    public string Value { get; }

    private PersonName(string value) => Value = value;

    /// <param name="fieldName">Names the field in the error message ("First name", "Last name").</param>
    public static Result<PersonName> Create(string? raw, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Result.Fail(UserErrors.PersonNameRequired(fieldName));

        var candidate = raw.Trim();

        if (candidate.Length > MaxLength) return Result.Fail(UserErrors.PersonNameTooLong(fieldName));

        return Result.Ok(new PersonName(candidate));
    }

    /// <summary>
    /// Null for null, empty or whitespace input (no name); otherwise <see cref="Create"/>. For the optional
    /// name fields of requests, where leaving a name blank means the user has none.
    /// </summary>
    public static Result<PersonName?> CreateOptional(string? raw, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Result.Ok<PersonName?>(null);

        var result = Create(raw, fieldName);
        return result.IsFailed ? Result.Fail<PersonName?>(result.Errors) : Result.Ok<PersonName?>(result.Value);
    }

    public static PersonName FromTrustedSource(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Person name cannot be null or empty.", nameof(value))
            : new PersonName(value);
    }

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
