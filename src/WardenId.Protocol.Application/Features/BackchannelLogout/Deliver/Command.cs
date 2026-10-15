using FluentResults;
using MediatR;

namespace WardenId.Protocol.Application.Features.BackchannelLogout.Deliver;

public sealed record Command(BackchannelLogoutRequested Message) : IRequest<Result<DeliveryOutcome>>;
