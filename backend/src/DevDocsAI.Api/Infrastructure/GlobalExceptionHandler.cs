using DevDocsAI.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DevDocsAI.Api.Infrastructure;

/// <summary>
/// Translates exceptions into RFC 7807 ProblemDetails. Expected application
/// exceptions map to their HTTP status; anything else is a logged 500 with no
/// internal detail leaked. Errors are never swallowed silently.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;

        var (status, title) = Map(exception);
        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception (traceId: {TraceId})", traceId);
        }
        else
        {
            logger.LogInformation("Request failed: {Status} {Title} (traceId: {TraceId})",
                status, title, traceId);
        }

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status >= StatusCodes.Status500InternalServerError ? null : exception.Message,
        };
        problemDetails.Extensions["traceId"] = traceId;

        if (exception is ValidationException validation)
        {
            problemDetails.Extensions["errors"] = validation.Errors;
        }

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });
    }

    private static (int Status, string Title) Map(Exception exception) => exception switch
    {
        ValidationException => (StatusCodes.Status400BadRequest, "Validation failed."),
        AuthenticationException => (StatusCodes.Status401Unauthorized, "Authentication failed."),
        ForbiddenAccessException => (StatusCodes.Status403Forbidden, "Access denied."),
        NotFoundException => (StatusCodes.Status404NotFound, "Resource not found."),
        ConflictException => (StatusCodes.Status409Conflict, "Conflict."),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred."),
    };
}
