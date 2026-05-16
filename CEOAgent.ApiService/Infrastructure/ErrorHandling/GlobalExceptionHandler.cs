using System.Diagnostics;
using CEOAgent.Application.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CEOAgent.ApiService;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    CorrelationIdAccessor correlationIdAccessor,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, type) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not found", "not_found"),
            BusinessRuleException => (StatusCodes.Status422UnprocessableEntity, "Business rule violation", "business_rule_violation"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Concurrency conflict", "concurrency_conflict"),
            IntegrationException => (StatusCodes.Status503ServiceUnavailable, "Downstream dependency unavailable", "downstream_dependency_unavailable"),
            OperationCanceledException => (499, "Client closed request", "client_closed_request"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error", "unexpected_error")
        };

        logger.LogError(exception, "Request failed with status {StatusCode}", status);

        httpContext.Response.StatusCode = status;

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = type,
            Detail = exception is BusinessRuleException or NotFoundException ? exception.Message : null
        };

        problemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        problemDetails.Extensions["correlationId"] = correlationIdAccessor.CorrelationId ?? httpContext.TraceIdentifier;

        if (exception is BusinessRuleException businessRuleException)
        {
            problemDetails.Extensions["code"] = businessRuleException.Code;
        }

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });

        return true;
    }
}
