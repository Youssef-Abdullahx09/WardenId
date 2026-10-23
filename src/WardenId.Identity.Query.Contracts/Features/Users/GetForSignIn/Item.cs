namespace WardenId.Identity.Query.Contracts.Features.Users.GetForSignIn;

/// <summary>One row of Features/Users/GetForSignIn/Query.sql.</summary>
public sealed class Item
{
    public Guid Id { get; init; }
    public string Email { get; init; } = "";
    public string Username { get; init; } = "";

    /// <summary>The <c>UserStatus</c> member name, as persisted.</summary>
    public string Status { get; init; } = "";

    /// <summary>The <c>RequiredAction</c> member names, as persisted, in stored order.</summary>
    public string[] PendingActions { get; init; } = [];
}
