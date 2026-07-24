using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Microsoft.OpenApi;
using WardenId.Gateway.Utilities;
using WardenId.SharedKernel.Presentation;

namespace WardenId.Gateway.DependencyInjection;

public static class GatewayDependencyInjection
{
    public static void AddGateway(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddEndPoints();
        services.AddApiVersioningConfiguration();
        services.AddExceptionHandling();
        services.AddJsonConfiguration();
        services.AddSwaggerDocumentation();
        services.AddCorsConfiguration();
    }

    private static void AddApiVersioningConfiguration(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        });
    }

    private static void AddJsonConfiguration(
        this IServiceCollection services)
    {
        // Accept enums as either their string name (case-insensitive, e.g. "Admin")
        // or their numeric value (e.g. 1) in request bodies and query strings.
        // The built-in converter reads both shapes and writes the string name back.
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
    }

    private static void AddExceptionHandling(
        this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
    }

    private static void AddCorsConfiguration(
        this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(corsPolicyBuilder =>
            {
                corsPolicyBuilder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });
    }
    
    private static void AddSwaggerDocumentation(
        this IServiceCollection services)
    {
        services.AddOpenApi();
        
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "WardenId",
                Version = "1.0"
            });

            options.OperationFilter<AsParametersOperationFilter>();
            options.SchemaFilter<EnumSchemaFilter>();
            options.CustomSchemaIds(type => type.FullName);

            options.MapType<DateOnly>(() => new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Format = "date"
            });

            options.MapType<TimeOnly>(() => new OpenApiSchema
            {
                Type = JsonSchemaType.String,
                Format = "time",
                Example = JsonValue.Create("14:00:00")
            });

            // Configure Bearer Token Authentication for Swagger UI
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description =
                    "Enter your JWT token only. The 'Bearer ' prefix will be added automatically.\n\nExample: eyJhbGciOiJSUzI1NiIsInR5cCI..."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecuritySchemeReference("Bearer", document), []
                }
            });
        });
    }
}