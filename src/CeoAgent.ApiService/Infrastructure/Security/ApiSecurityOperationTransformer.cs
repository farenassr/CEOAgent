using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CeoAgent.ApiService.Infrastructure.Security;

internal sealed class ApiSecurityOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var allowsAnonymous = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IAllowAnonymous>()
            .Any();

        if (allowsAnonymous)
        {
            return Task.CompletedTask;
        }

        if (context.Document is not { } document)
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(CreateRequirement(document, OpenApiSecuritySchemeNames.KeycloakOAuth, ["openid"]));

        return Task.CompletedTask;
    }

    private static OpenApiSecurityRequirement CreateRequirement(
        OpenApiDocument document,
        string schemeName,
        List<string> requiredScopes)
    {
        return new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(schemeName, document, externalResource: null)] = requiredScopes,
        };
    }
}
