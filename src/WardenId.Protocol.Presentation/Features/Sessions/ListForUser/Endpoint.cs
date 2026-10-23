using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Protocol.Application.Features.Sessions.ListForUser;
using WardenId.Protocol.Presentation.Utilities;
using WardenId.SharedKernel.Presentation;
using WardenId.SharedKernel.Presentation.Utilities;
using ListForUserQuery = WardenId.Protocol.Application.Features.Sessions.ListForUser.Query;

namespace WardenId.Protocol.Presentation.Features.Sessions.ListForUser;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet(ApiVersionSets.Sessions)
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet(ProtocolRoutes.Admin.UserSessions, async (
                [FromRoute] Guid tenantId,
                [FromRoute] Guid userId,
                [FromQuery] bool? includeEnded,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new ListForUserQuery(tenantId, userId, includeEnded ?? false), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Sessions.ListForUser)
            .RequireAuthorization()
            .WithTags(EndpointTags.Sessions)
            .WithSummary("List a User's Sessions")
            .WithDescription(
                "Lists the user's live login sessions in the tenant, newest first, with the clients that took part in each. " +
                "includeEnded=true adds ended and expired sessions; endedAt and endReason are null until a session is ended.")
            .Produces<IReadOnlyList<UserSession>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
