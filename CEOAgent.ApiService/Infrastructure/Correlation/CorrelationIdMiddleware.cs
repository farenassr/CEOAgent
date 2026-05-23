using System.Diagnostics;

namespace CEOAgent.ApiService.Infrastructure.Correlation;

public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger,
    CorrelationIdAccessor correlationIdAccessor)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        correlationIdAccessor.CorrelationId = correlationId;
        context.Response.Headers[HeaderName] = correlationId;
        Activity.Current?.SetTag("correlation_id", correlationId);

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["correlation_id"] = correlationId
        }))
        {
            try
            {
                await next(context);
            }
            finally
            {
                correlationIdAccessor.CorrelationId = null;
            }
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values)
            && values is [{ Length: > 0 } id])
        {
            return id;
        }

        Span<char> buffer = stackalloc char[36];
        Guid.CreateVersion7().TryFormat(buffer, out _, format: "D");
        return new string(buffer);
    }
}
