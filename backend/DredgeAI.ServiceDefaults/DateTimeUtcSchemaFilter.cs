using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DredgeAI;

/// <summary>
/// Swagger schema filter that marks all DateTime properties with UTC format
/// requirements in the description, so API consumers know to send ISO 8601
/// with Z suffix (e.g., 2026-07-12T02:00:00Z).
/// </summary>
public class DateTimeUtcSchemaFilter : ISchemaFilter
{
    private const string UtcDescription =
        "UTC time in ISO 8601 format with Z suffix. Example: 2026-07-12T02:00:00Z";

    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(DateTime) || context.Type == typeof(DateTime?))
        {
            schema.Description = string.IsNullOrEmpty(schema.Description)
                ? UtcDescription
                : schema.Description + " (" + UtcDescription + ")";
        }
    }
}
