using System.Diagnostics;
using CeoAgent.ApiService.Infrastructure.Correlation;
using CeoAgent.Application.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.ApiService.Infrastructure.ErrorHandling;

public sealed partial class GlobalExceptionHandler(
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
            _ => (StatusCodes.Status500InternalServerError, "Unexpected server error", "unexpected_error"),
        };

        if (Activity.Current is { } activity)
        {
            activity.SetStatus(ActivityStatusCode.Error, title);
            activity.SetTag("http.response.status_code", status);
            activity.SetTag("error.type", type);
        }

        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            RequestCancelledByClient(logger, correlationIdAccessor.CorrelationId);
        }
        else
        {
            RequestFailed(logger, exception, status, correlationIdAccessor.CorrelationId);
        }

        httpContext.Response.StatusCode = status;

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Type = type,
            Detail = exception is BusinessRuleException ? exception.Message : null,
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
            Exception = exception,
        });

        return true;
    }

}
