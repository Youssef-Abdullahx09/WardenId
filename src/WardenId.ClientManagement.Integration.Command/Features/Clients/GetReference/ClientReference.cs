namespace WardenId.ClientManagement.Integration.Command.Features.Clients.GetReference;

/// <param name="ClientId">The public client_id (unique per tenant only).</param>
public sealed record ClientReference(Guid Id, string ClientId);
