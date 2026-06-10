using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Infrastructure.Entities;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CeoAgent.Infrastructure.Implementation.AITools.Execution;

public abstract class AgentTool<TRequest> : IAgentTool<TRequest>
{
    protected AgentTool()
    {
        ParametersSchema = AgentToolJsonSchema.Create(typeof(TRequest));
    }

    public abstract string ToolKey { get; }

    public abstract bool IsMutating { get; }

    public abstract string Description { get; }

    public JsonElement ParametersSchema { get; }

    public Type RequestType => typeof(TRequest);

    public bool ValidateObject(object request)
    {
        return request is TRequest typedRequest && Validate(typedRequest);
    }

    public virtual bool Validate(TRequest request)
    {
        return true;
    }

    public async Task<IAgentToolExecution> ExecuteAsync(
        ToolExecutionContext context,
        object request,
        CancellationToken cancellationToken)
    {
        return await ExecuteToolAsync(context, (TRequest)request, cancellationToken);
    }

    public async Task<IAgentToolExecution> ExecuteAsync(
        ToolExecutionContext context,
        TRequest request,
        CancellationToken cancellationToken)
    {
        return await ExecuteToolAsync(context, request, cancellationToken);
    }

    protected abstract Task<ToolExecution> ExecuteToolAsync(
        ToolExecutionContext context,
        TRequest request,
        CancellationToken cancellationToken);
}

internal static class AgentToolJsonSchema
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static JsonElement Create(Type requestType)
    {
        var properties = new Dictionary<string, object?>(StringComparer.Ordinal);
        var required = new List<string>();
        var nullability = new NullabilityInfoContext();

        foreach (var property in requestType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var propertyName = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
                ?? JsonNamingPolicy.CamelCase.ConvertName(property.Name);
            properties[propertyName] = PropertySchema(property.PropertyType);

            var state = nullability.Create(property).WriteState;
            var isNullable = Nullable.GetUnderlyingType(property.PropertyType) is not null
                || state == NullabilityState.Nullable;
            if (!isNullable)
            {
                required.Add(propertyName);
            }
        }

        return JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false,
        }, SerializerOptions);
    }

    private static Dictionary<string, object?> PropertySchema(Type propertyType)
    {
        var type = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (type == typeof(int) || type == typeof(long))
        {
            return new Dictionary<string, object?> { ["type"] = "integer" };
        }

        if (type == typeof(bool))
        {
            return new Dictionary<string, object?> { ["type"] = "boolean" };
        }

        if (type == typeof(DateOnly))
        {
            return new Dictionary<string, object?> { ["type"] = "string", ["format"] = "date" };
        }

        if (type == typeof(TimeOnly))
        {
            return new Dictionary<string, object?> { ["type"] = "string", ["format"] = "time" };
        }

        if (type == typeof(DateTimeOffset) || type == typeof(DateTime))
        {
            return new Dictionary<string, object?> { ["type"] = "string", ["format"] = "date-time" };
        }

        return new Dictionary<string, object?> { ["type"] = "string" };
    }
}
