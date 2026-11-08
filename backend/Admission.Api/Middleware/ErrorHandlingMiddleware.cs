using Admission.Api.Contracts;
using Admission.Api.Services;

namespace Admission.Api.Middleware;

public sealed class ErrorHandlingMiddleware(
    RequestDelegate next,
    ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client went away; there is nobody to write a response to.
        }
        catch (ApiException ex)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteAsync(context, ex.StatusCode, ex.Code, ex.Message);
        }
        catch (BadHttpRequestException)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteAsync(
                context,
                StatusCodes.Status400BadRequest,
                "INVALID_REQUEST",
                "Please check the request body and try again.");
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Unhandled exception while processing {Method} {Path}.",
                context.Request.Method,
                context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "INTERNAL_ERROR",
                "Something went wrong. Please try again.");
        }
    }

    private static async Task WriteAsync(HttpContext context, int statusCode, string code, string message)
    {
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        await context.Response.WriteAsJsonAsync(new ApiErrorResponse(code, message));
    }
}
