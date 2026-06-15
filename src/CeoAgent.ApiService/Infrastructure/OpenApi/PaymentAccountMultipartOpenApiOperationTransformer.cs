using System.Text.Json.Nodes;
using CeoAgent.Shared.Payment;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CeoAgent.ApiService.Infrastructure.OpenApi;

internal sealed class PaymentAccountMultipartOpenApiOperationTransformer : IOpenApiOperationTransformer
{
    private const string PaymentAccountsPath = "v1/admin/payment-accounts";
    private const string PaymentAccountByIdPath = "v1/admin/payment-accounts/{paymentAccountId}";

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var relativePath = context.Description.RelativePath?.Trim('/');
        if (!IsPaymentAccountWriteEndpoint(relativePath, context.Description.HttpMethod))
        {
            return Task.CompletedTask;
        }

        var isCreate = string.Equals(context.Description.HttpMethod, HttpMethods.Post, StringComparison.OrdinalIgnoreCase);
        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>(StringComparer.Ordinal)
            {
                ["multipart/form-data"] = new()
                {
                    Schema = CreatePaymentAccountMultipartSchema(isCreate),
                },
            },
        };

        return Task.CompletedTask;
    }

    private static bool IsPaymentAccountWriteEndpoint(string? relativePath, string? httpMethod)
    {
        if (!string.Equals(httpMethod, HttpMethods.Post, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(httpMethod, HttpMethods.Put, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(relativePath, PaymentAccountsPath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(relativePath, PaymentAccountByIdPath, StringComparison.OrdinalIgnoreCase);
    }

    private static OpenApiSchema CreatePaymentAccountMultipartSchema(bool isCreate)
    {
        var required = new HashSet<string>(StringComparer.Ordinal)
        {
            "bankId",
            "accountNumber",
            "accountType",
            "currency",
            "reservationPaymentAmount",
            "isDefault",
            "isActive",
        };
        if (isCreate)
        {
            required.Add("qrImage");
        }

        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Required = required,
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            {
                ["bankId"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
                ["accountNumber"] = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 80 },
                ["accountType"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = Enum.GetNames<PaymentAccountType>().Select(value => JsonValue.Create(value)!).ToList<JsonNode>(),
                },
                ["accountHolderName"] = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 200 },
                ["currency"] = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 3, MaxLength = 3 },
                ["reservationPaymentAmount"] = new OpenApiSchema { Type = JsonSchemaType.Number, Format = "decimal" },
                ["isDefault"] = new OpenApiSchema { Type = JsonSchemaType.Boolean },
                ["isActive"] = new OpenApiSchema { Type = JsonSchemaType.Boolean, Default = JsonValue.Create(true) },
                ["qrImage"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Format = "binary",
                    Description = "PNG or JPEG QR payment image.",
                },
            },
        };
    }
}
