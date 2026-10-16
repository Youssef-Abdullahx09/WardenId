using FluentResults;
using WardenId.Identity.Domain;

namespace WardenId.Identity.Application.Features.Users.List;

public enum UserSortField
{
    CreatedAt = 1,
    Email = 2,
    Username = 3
}

/// <summary>
/// The user list's <c>sort</c> value: a field name (<c>createdAt</c>, <c>email</c>, <c>username</c>,
/// any case), ascending, or prefixed with <c>-</c> for descending. The default is <c>-createdAt</c>.
/// </summary>
public sealed record UserListSort(UserSortField Field, bool Descending)
{
    public static readonly UserListSort Default = new(UserSortField.CreatedAt, Descending: true);

    public static Result<UserListSort> Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Default;

        var value = raw.Trim();
        var descending = value.StartsWith('-');
        var fieldName = descending ? value[1..] : value;

        // Enum.TryParse would also accept numbers and comma-separated lists, so match names only.
        var field = Enum.GetValues<UserSortField>()
            .Where(f => string.Equals(f.ToString(), fieldName, StringComparison.OrdinalIgnoreCase))
            .Select(f => (UserSortField?)f)
            .FirstOrDefault();

        return field is null
            ? Result.Fail(UserErrors.Validation(
                $"Sort must be one of createdAt, email or username, optionally prefixed with '-' for descending; got '{value}'."))
            : new UserListSort(field.Value, descending);
    }
}
