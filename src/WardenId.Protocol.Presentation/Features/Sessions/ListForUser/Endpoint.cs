using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Protocol.Application.Features.Sessions.ListForUser;
using ListForUserQuery = WardenId.Protocol.Application.Features.Sessions.ListForUser.Query;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Protocol.Presentation.Features.Sessions.ListForUser;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Sessions")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet("/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/users/{userId:guid}/sessions", async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid userId,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new ListForUserQuery(tenantId, userId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Sessions.ListForUser)
            .RequireAuthorization()
            .WithTags("Protocol - Sessions")
            .WithSummary("List a User's Sessions")
            .WithDescription("Lists the user's live login sessions in the tenant, with the clients that took part in each.")
            .Produces<IReadOnlyList<UserSession>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
