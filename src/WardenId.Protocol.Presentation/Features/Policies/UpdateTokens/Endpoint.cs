using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Protocol.Presentation.Features.Policies.UpdateTokens;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Policies")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapPut("/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/policies/tokens", async (
                [FromRoute] Guid tenantId,
                [FromBody] Request request,
                [FromServices] ISender sender,
                [FromServices] ICurrentUser currentUser,
                CancellationToken cancellationToken) =>
            {
                var command = request.ToCommand(tenantId, currentUser.UserId);
                if (command.IsFailed) return command.HandleApiFailure();

                var result = await sender.Send(command.Value, cancellationToken);
                return result.IsSuccess ? Results.NoContent() : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Policies.UpdateTokens)
            .RequireAuthorization()
            .WithTags("Protocol - Policies")
            .WithSummary("Set the tenant's token and session lifetimes")
            .WithDescription(
                "Replaces the tenant's lifetime overrides. Every field is an ISO 8601 duration (days, hours, minutes, " +
                "seconds; e.g. \"PT15M\", \"P30D\"); null or omitted clears that override back to the global default. " +
                "Bounds: access and ID token 1 min-24 h, refresh token 1 h-365 d, refresh-token absolute 1 h-365 d and " +
                "at least the refresh lifetime (and no longer than the refresh-token retention, " +
                "ProtocolOpenIddict:PruneMinimumAge), authorization code 30 s-10 min, session cookie 5 min-30 d, " +
                "session absolute 5 min-365 d and at least the cookie lifetime. The pairs are checked against the " +
                "effective values, so a null field counts as its global default. A client's own lifetime overrides " +
                "still win over these. Changes apply to tokens, codes and sessions issued afterwards only; nothing " +
                "already issued is shortened or extended. Gateway nodes other than the one answering pick the change " +
                "up within 30 seconds. Audited as admin.config against the tenant. tenantId is not checked for existence.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
