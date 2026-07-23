using System.ComponentModel;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WardenId.Gateway.Utilities;

/// <summary>
/// Swashbuckle operation filter that fixes OpenAPI parameter generation for Minimal API
/// endpoints using the <see cref="AsParametersAttribute"/> binding pattern.
///
/// Problem: When a Minimal API handler receives a complex object via [AsParameters], ASP.NET Core
/// flattens its public properties into individual query/header parameters at runtime, but
/// Swashbuckle does not reflect this — it either omits those parameters or generates them
/// incorrectly in the Swagger UI.
///
/// Solution: This filter detects method parameters decorated with [AsParameters], reflects over
/// the parameter type's public properties, and manually registers each property as an
/// <see cref="OpenApiParameter"/> on the operation — inferring name (camelCase), location (Query),
/// required-ness (non-nullable + no [DefaultValue]), and schema from the property type.
///
/// Usage: Register via <c>services.AddSwaggerGen(o => o.OperationFilter&lt;AsParametersOperationFilter&gt;())</c>.
/// </summary>
public sealed class AsParametersOperationFilter : IOperationFilter
{
    /// <inheritdoc/>
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var asParametersParameters = context.ApiDescription.ParameterDescriptions
            .Where(p => p.Source == BindingSource.Query || p.Source == BindingSource.Header)
            .ToList();

        if (!asParametersParameters.Any())
            return;

        var methodParameters = context.MethodInfo.GetParameters();

        foreach (var methodParam in methodParameters)
        {
            var asParamAttr = methodParam.GetCustomAttribute<AsParametersAttribute>();
            if (asParamAttr == null)
                continue;

            var paramType = methodParam.ParameterType;
            var properties = paramType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                // ASP.NET Core binds properties using camelCase names by convention
                var propertyName = char.ToLowerInvariant(property.Name[0]) + property.Name[1..];

                // Skip if Swashbuckle already registered this parameter from another source
                if (operation.Parameters!.Any(p =>
                    p.Name!.Equals(propertyName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var parameter = new OpenApiParameter
                {
                    Name = propertyName,
                    In = ParameterLocation.Query,
                    // A parameter is optional when its type is nullable or it carries [DefaultValue]
                    Required = !IsNullable(property.PropertyType) &&
                               !property.GetCustomAttributes(typeof(DefaultValueAttribute), false).Any(),
                    Schema = context.SchemaGenerator.GenerateSchema(
                        property.PropertyType,
                        context.SchemaRepository)
                };

                operation.Parameters!.Add(parameter);
            }
        }
    }

    /// <summary>
    /// Returns <c>true</c> for reference types and <c>Nullable&lt;T&gt;</c> value types —
    /// both map to optional parameters in OpenAPI.
    /// </summary>
    private static bool IsNullable(Type type)
    {
        return Nullable.GetUnderlyingType(type) != null ||
               !type.IsValueType;
    }
}