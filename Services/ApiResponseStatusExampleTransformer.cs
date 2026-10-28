using System.Text.Json.Nodes;
using DulceAtardecer.Models;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DulceAtardecer.Services;

public class ApiResponseStatusExampleTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (operation.Responses is null)
        {
            return Task.CompletedTask;
        }

        foreach ((string code, IOpenApiResponse response) in operation.Responses)
        {
            if (!int.TryParse(code, out int statusCode) || response.Content is null)
            {
                continue;
            }

            foreach (OpenApiMediaType mediaType in response.Content.Values)
            {
                if (mediaType.Schema is null)
                {
                    continue;
                }

                mediaType.Schema = new OpenApiSchema
                {
                    AllOf = [mediaType.Schema, CreateStatusOverlay(statusCode)],
                };
            }
        }

        return Task.CompletedTask;
    }

    private static OpenApiSchema CreateStatusOverlay(int statusCode)
    {
        bool success = statusCode < StatusCodes.Status400BadRequest;
        Dictionary<string, IOpenApiSchema> properties = new()
        {
            ["success"] = new OpenApiSchema { Type = JsonSchemaType.Boolean, Example = JsonValue.Create(success) },
            ["statusCode"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Integer,
                Description = "Igual al código HTTP de la respuesta",
                Example = JsonValue.Create(statusCode),
            },
        };

        if (!success)
        {
            string message = ApiResponse.DefaultMessage(statusCode);
            properties["message"] = new OpenApiSchema { Type = JsonSchemaType.String, Example = JsonValue.Create(message) };
            properties["errors"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = new OpenApiSchema { Type = JsonSchemaType.String },
                Example = new JsonArray(message),
            };
        }

        return new OpenApiSchema { Type = JsonSchemaType.Object, Properties = properties };
    }
}
