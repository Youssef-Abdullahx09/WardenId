using FluentResults;
using MediatR;

namespace WardenId.ClientManagement.Integration.Command.Features.Clients.GetReference;

/// <summary>
/// Admin routes name a client by its Guid <paramref name="Id"/>; the protocol layer knows it by its public client_id.
/// Not found for another tenant's or a soft-deleted client; a disabled client is found.
/// </summary>
public sealed record Query(Guid TenantId, Guid Id) : IRequest<Result<ClientReference>>;
