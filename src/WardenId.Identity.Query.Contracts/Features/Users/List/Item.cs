namespace WardenId.Identity.Query.Contracts.Features.Users.List;

/// <summary>One row of Features/Users/List/Query.sql. <see cref="CreatedAt"/> is a UTC <see cref="DateTime"/>.</summary>
public sealed class Item
{
    public Guid Id { get; init; }
    public string Email { get; init; } = "";
    public string Username { get; init; } = "";

    /// <summary>The <c>UserStatus</c> member name, as persisted.</summary>
    public string Status { get; init; } = "";

    public DateTime CreatedAt { get; init; }
}
