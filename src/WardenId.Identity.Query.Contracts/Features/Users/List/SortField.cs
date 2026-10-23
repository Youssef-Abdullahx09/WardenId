namespace WardenId.Identity.Query.Contracts.Features.Users.List;

/// <summary>The column a user list page is ordered by; the user id breaks ties in the same direction.</summary>
public enum SortField
{
    CreatedAt = 1,
    Email = 2,
    Username = 3,
    FirstName = 4,
    LastName = 5
}
