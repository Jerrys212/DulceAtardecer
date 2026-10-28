using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace DulceAtardecer.Services;

public class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    private const string SchemeName = "Bearer";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Pega el accessToken que devuelve POST /api/v1/auth/login.",
        };

        return Task.CompletedTask;
    }

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;
        bool requiresAuthorization = metadata.OfType<IAuthorizeData>().Any()
            && !metadata.OfType<IAllowAnonymous>().Any();

        if (requiresAuthorization)
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = [],
            });
        }

        return Task.CompletedTask;
    }
}
