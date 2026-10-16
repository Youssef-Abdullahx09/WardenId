using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.ClientManagement.Application.Features.Clients.Secrets.Expire;
using WardenId.SharedKernel.Presentation;

namespace WardenId.ClientManagement.Presentation.Features.Clients.Secrets.Expire;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Clients")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapDelete(
                "/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/clients/{clientId:guid}/secrets/{secretId:guid}", async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid clientId,
                [FromRoute] Guid secretId,
                [FromServices] ISender sender,
                [FromServices] ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new Command(tenantId, clientId, secretId, currentUser.UserId), cancellationToken);

                return result.IsSuccess ? Results.NoContent() : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Clients.ExpireSecret)
            .RequireAuthorization()
            .WithTags("ClientManagement - Clients")
            .WithSummary("Expire a Client secret")
            .WithDescription("Expires a specific client secret immediately (e.g. a leaked secret), independent of rotation.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
