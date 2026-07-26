using FluentResults;
using MediatR;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Application.Features.Clients.Create;

public sealed record Command(
    Guid TenantId,
    ClientType ClientType,
    string DisplayName,
    GrantType GrantType,
    ClientAuthenticationMethod ClientAuthenticationMethod,
    bool IsFirstParty,
    bool ConsentRequired,
    string? Description) : IRequest<Result<Guid>>;
