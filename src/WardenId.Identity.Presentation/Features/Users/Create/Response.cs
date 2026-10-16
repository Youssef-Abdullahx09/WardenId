namespace WardenId.Identity.Presentation.Features.Users.Create;

/// <summary>Body of 201 Created: the new user's id.</summary>
public sealed record Response(Guid UserId);
