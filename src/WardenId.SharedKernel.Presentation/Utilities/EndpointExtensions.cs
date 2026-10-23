using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace WardenId.SharedKernel.Presentation.Utilities;

public static class EndpointExtensions
{
    public static void AddEndPoints(
        this IServiceCollection services)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name?.EndsWith(nameof(Presentation), StringComparison.OrdinalIgnoreCase) ?? false);

        var serviceDescriptors = assemblies
            .SelectMany(assembly => assembly.DefinedTypes)
            .Where(type => type is { IsAbstract: false, IsInterface: false }
                           && type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type))
            .ToArray();

        services.TryAddEnumerable(serviceDescriptors);
    }

    public static void MapEndpoints(this WebApplication app)
    {
        var endpoints = app.Services.GetRequiredService<IEnumerable<IEndpoint>>();

        foreach (var endpoint in endpoints)
        {
            var endpointBuilder = endpoint.MapEndpoint(app);

            // Automatically apply .WithOpenApi() to each mapped endpoint
            endpointBuilder.WithMetadata();
        }
    }
}