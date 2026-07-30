namespace WardenId.Identity.Application.Features.Users.Register;

public sealed record UserRegistered(Guid UserId, string RawVerificationToken);
