using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using WardenId.Protocol.Application.Features.Policies.GetTokens;
using WardenId.SharedKernel.Presentation;
using GetTokensQuery = WardenId.Protocol.Application.Features.Policies.GetTokens.Query;

namespace WardenId.Protocol.Presentation.Features.Policies.GetTokens;

public sealed class Endpoint : IEndpoint
{
    public IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app)
    {
        var apiVersionSet = app.NewApiVersionSet("Policies")
            .HasApiVersion(new ApiVersion(1))
            .ReportApiVersions()
            .Build();

        return app.MapGet("/api/v{version:apiVersion}/admin/tenants/{tenantId:guid}/policies/tokens", async (
                [FromRoute] Guid tenantId,
                [FromServices] ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetTokensQuery(tenantId), cancellationToken);

                return result.IsSuccess ? Results.Ok(result.Value) : result.HandleApiFailure();
            })
            .WithApiVersionSet(apiVersionSet)
            .MapToApiVersion(new ApiVersion(1))
            .WithName(EndpointNames.Policies.GetTokens)
            .RequireAuthorization()
            .WithTags("Protocol - Policies")
            .WithSummary("Get the token and session lifetimes")
            .WithDescription(
                "Returns the token and login-session lifetimes in effect for the tenant. Every lifetime is an ISO 8601 " +
                "duration (e.g. \"PT15M\", \"P30D\"). Each value is the tenant's override where it has one, else the " +
                "global default (the ProtocolToken and ProtocolSession config sections, with code defaults). " +
                "`overrides` holds the tenant's own values (null = global default); scope is \"tenant\" when at least " +
                "one is set, else \"global\". A client's own lifetime overrides still win at issuance and are not " +
                "reflected here. tenantId is not checked for existence.")
            .Produces<TokenPolicy>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
