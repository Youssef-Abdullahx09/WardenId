using FluentResults;
using MediatR;
using WardenId.ClientManagement.Application.Abstractions;
using WardenId.ClientManagement.Domain;

namespace WardenId.ClientManagement.Application.Features.Clients.Update;

public sealed class Handler(
    IClientRepository repository) : IRequestHandler<Command, Result>
{
    public async Task<Result> Handle(
        Command request,
        CancellationToken cancellationToken)
    {
        var clientResult = await repository.GetByIdAsync(request.TenantId, request.ClientId, cancellationToken);
        if (clientResult.IsFailed) return Result.Fail(clientResult.Errors);

        var client = clientResult.Value;

        if (request.DisplayName is not null)
        {
            var displayNameResult = client.UpdateDisplayName(request.DisplayName);
            if (displayNameResult.IsFailed) return Result.Fail(displayNameResult.Errors);
        }

        if (request.Description is not null)
        {
            var descriptionResult = client.UpdateDescription(request.Description);
            if (descriptionResult.IsFailed) return Result.Fail(descriptionResult.Errors);
        }

        if (request.GrantType is not null)
        {
            var grantsResult = client.UpdateGrantTypes(request.GrantType.Value);
            if (grantsResult.IsFailed) return Result.Fail(grantsResult.Errors);
        }

        if (request.IsFirstParty is not null) client.UpdateIsFirstParty(request.IsFirstParty.Value);

        if (request.Enabled is true) client.Enable();
        if (request.Enabled is false) client.Disable();

        if (request.ConsentRequired is not null) client.UpdateConsentRequired(request.ConsentRequired.Value);

        if (request.ClearTokenLifetimeOverrides)
        {
            client.UpdateTokenLifetimeOverrides(null);
        }
        else if (request.AccessTokenSeconds is not null
            || request.IdTokenSeconds is not null
            || request.RefreshTokenSeconds is not null)
        {
            var overridesResult = TokenLifetimeOverrides.Create(
                request.AccessTokenSeconds, request.IdTokenSeconds, request.RefreshTokenSeconds);
            if (overridesResult.IsFailed) return Result.Fail(overridesResult.Errors);

            client.UpdateTokenLifetimeOverrides(overridesResult.Value);
        }

        await repository.UpdateAsync(client, request.ActorId, cancellationToken);

        return Result.Ok();
    }
}
