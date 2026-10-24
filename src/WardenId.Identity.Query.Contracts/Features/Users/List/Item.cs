namespace WardenId.Identity.Query.Contracts.Features.Users.List;

/// <summary>One row of Features/Users/List/Query.sql. <see cref="CreatedAt"/> is a UTC <see cref="DateTime"/>.</summary>
public sealed class Item
{
    public Guid Id { get; init; }
    public string Email { get; init; } = "";
    public string Username { get; init; } = "";
    public string? FirstName { get; init; }
    public string? LastName { get; init; }

    /// <summary>Persisted as the member name; Dapper parses it.</summary>
    public UserStatus Status { get; init; }

    public DateTime CreatedAt { get; init; }
}
