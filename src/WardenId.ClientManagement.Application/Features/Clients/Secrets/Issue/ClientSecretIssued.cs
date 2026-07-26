namespace WardenId.ClientManagement.Application.Features.Clients.Secrets.Issue;

public sealed record ClientSecretIssued(
    Guid Id,
    string Label,
    string Secret,
    DateTimeOffset? ExpiresAt);
