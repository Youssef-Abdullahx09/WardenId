using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WardenId.Gateway.Utilities;


/// <summary>
/// Makes enum values visible in Swagger UI for both shapes Swashbuckle emits:
/// <list type="bullet">
/// <item>GET query parameters — the schema itself is the enum type.</item>
/// <item>POST/PUT body models — enum properties inside a class are emitted as
/// references to a shared schema definition, which hides the acceptable values.
/// We walk the model's properties and inline the enum schema.</item>
/// </list>
/// In both cases the schema renders the string names and lists every possible
/// value (name = numeric value) in the description.
/// </summary>
public sealed class EnumSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        // GET case: the schema itself is an enum type.
        if (context.Type.IsEnum)
        {
            if (schema is OpenApiSchema concrete)
                InlineEnumSchema(concrete, context.Type);
            return;
        }

        // POST/PUT body case: walk the model's properties and inline any enum fields.
        if (schema.Properties is not { Count: > 0 }) return;

        var properties = context.Type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var propertyName in schema.Properties.Keys.ToList())
        {
            var property = properties.FirstOrDefault(p =>
                string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

            if (property is null) continue;

            var enumType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (!enumType.IsEnum) continue;

            // The existing entry is typically an OpenApiSchemaReference ($ref to a shared
            // component) rather than a plain OpenApiSchema, so we can't just null out a
            // "Reference" property like in the old model — we replace the dictionary entry
            // outright with a fresh inline schema. Grab any description already attached
            // to that entry (e.g. from XML doc comments on the property) so it isn't lost.
            var existing = schema.Properties[propertyName];
            var existingDescription = existing switch
            {
                OpenApiSchema concrete => concrete.Description,
                OpenApiSchemaReference reference => reference.Description,
                _ => null
            };

            var inlined = new OpenApiSchema { Description = existingDescription };
            InlineEnumSchema(inlined, enumType);
            schema.Properties[propertyName] = inlined;
        }
    }

    private static void InlineEnumSchema(OpenApiSchema schema, Type enumType)
    {
        schema.Type = JsonSchemaType.String;
        schema.Format = null;

        var builder = new StringBuilder();
        builder.AppendLine(schema.Description);
        builder.AppendLine();
        builder.AppendLine("Possible values:");

        var enumValues = new List<JsonNode>();
        foreach (var name in Enum.GetNames(enumType))
        {
            var value = Convert.ToInt64(Enum.Parse(enumType, name));
            enumValues.Add(JsonValue.Create(name));
            builder.AppendLine($"- `{name}` = {value}");
        }

        schema.Enum = enumValues;
        schema.Description = builder.ToString();
    }
}