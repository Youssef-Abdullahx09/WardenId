using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.ClientManagement.Domain;
using WardenId.SharedKernel.Presentation;

namespace WardenId.ClientManagement.Presentation.Features.Clients.List;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Clients")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet("/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/clients", async (
                [FromRoute] Guid tenantId,
                [FromQuery] int pageNumber,
                [FromQuery] int pageSize,
                [FromQuery] bool? enabled,
                [FromQuery] ClientType? clientType,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var request = new Request(pageNumber, pageSize, enabled, clientType);
                var result = await sender.Send(request.ToQuery(tenantId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .RequireAuthorization()
            .WithTags("ClientManagement - Clients")
            .WithSummary("List Clients")
            .WithDescription("Lists clients for the given tenant, paginated and optionally filtered by enabled/clientType.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
