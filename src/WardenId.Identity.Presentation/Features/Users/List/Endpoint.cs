using Asp.Versioning;
using Asp.Versioning.Builder;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Identity.Application.Features.Users.List;
using WardenId.Identity.Domain;
using WardenId.Identity.Presentation.Utilities;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;
using WardenId.SharedKernel.Query.Contracts;

namespace WardenId.Identity.Presentation.Features.Users.List;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Users)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet(IdentityRoutes.Users.Collection, async (
                [FromRoute] Guid tenantId,
                [FromQuery] int pageNumber,
                [FromQuery] int pageSize,
                [FromQuery] UserStatus? status,
                [FromQuery] string? search,
                [FromQuery] string? sort,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var request = new Request(pageNumber, pageSize, status, search, sort);
                var result = await sender.Send(request.ToQuery(tenantId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Users.List)
            .RequireAuthorization()
            .WithTags(EndpointTags.Users)
            .WithSummary("List users")
            .WithDescription(
                "Lists users for the given tenant, paginated and optionally filtered by status. `search` matches " +
                "the start of the email, username, first name, last name or full name (\"first last\"), ignoring case. " +
                "`sort` is `createdAt`, `email`, `username`, `firstName` or `lastName`, prefixed with `-` for " +
                "descending (users without the sorted name come last); the default is `-createdAt` (newest first).")
            .Produces<PaginationListResponse<UserListItem>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
