namespace WardenId.Identity.Presentation.Features.Users.Register;

/// <summary>Body of 201 Created: the new user's id. The verification token is emailed, never returned.</summary>
public sealed record Response(Guid UserId);
