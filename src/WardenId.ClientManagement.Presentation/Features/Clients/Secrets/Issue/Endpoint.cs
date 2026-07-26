using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.SharedKernel.Presentation;

namespace WardenId.ClientManagement.Presentation.Features.Clients.Secrets.Issue;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Clients")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapPost("/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/clients/{clientId:guid}/secrets", async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid clientId,
                [FromBody] Request request,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(request.ToCommand(tenantId, clientId), cancellationToken);

                return result.IsSuccess ? Results.Created(
                    $"/api/v1/admin/tenants/{tenantId}/clients/{clientId}", result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .RequireAuthorization()
            .WithTags("ClientManagement - Clients")
            .WithSummary("Issue a Client secret")
            .WithDescription("Issues a new client secret, rotating out the prior active secret with an overlap window if one exists. The plaintext secret is only ever returned here, once.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }
}
