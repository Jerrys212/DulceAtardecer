using DulceAtardecer.Models;

namespace DulceAtardecer.Middleware;

public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException ex)
        {
            await WriteAsync(context, ApiResponse.Fail(ex.StatusCode, ex.Message, ex.Errors));
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);

            IEnumerable<string>? errors = environment.IsDevelopment()
                ? [$"{ex.GetType().Name}: {ex.Message}"]
                : null;
            await WriteAsync(context, ApiResponse.Fail(StatusCodes.Status500InternalServerError, errors: errors));
        }
    }

    private static async Task WriteAsync(HttpContext context, ApiResponse<object> response)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = response.StatusCode;
        await context.Response.WriteAsJsonAsync(response);
    }
}
