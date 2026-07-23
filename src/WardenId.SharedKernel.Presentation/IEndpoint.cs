using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace WardenId.SharedKernel.Presentation;

public interface IEndpoint
{
    IEndpointConventionBuilder MapEndpoint(IEndpointRouteBuilder app);
}