using System.Diagnostics;
using System.Text.Json;
using AcxiomCRM.DTOs.Common;

namespace AcxiomCRM.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
            _logger.LogError(ex, "Unhandled exception occurred. TraceId: {TraceId}, Path: {Path}", traceId, context.Request.Path);

            if (context.Response.HasStarted)
            {
                _logger.LogWarning("The response has already started, the exception middleware will not write response body.");
                throw;
            }

            if (ex is UnauthorizedAccessException)
            {
                if (IsApiRequest(context))
                {
                    context.Response.Clear();
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";

                    var response = ApiResponse.Fail("You do not have permission to access this resource.", null, traceId);
                    var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                    await context.Response.WriteAsync(json);
                    return;
                }
                else
                {
                    context.Response.Redirect("/Account/AccessDenied");
                    return;
                }
            }

            if (IsApiRequest(context))
            {
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";

                var response = ApiResponse.Fail("An unexpected error occurred while processing your request. Please try again later.", null, traceId);
                var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                await context.Response.WriteAsync(json);
            }
            else
            {
                context.Response.Redirect($"/Home/Error?traceId={Uri.EscapeDataString(traceId)}");
            }
        }
    }

    private static bool IsApiRequest(HttpContext context)
    {
        return context.Request.Path.StartsWithSegments("/api") ||
               (context.Request.Headers.Accept.ToString().Contains("application/json") && !context.Request.Headers.Accept.ToString().Contains("text/html"));
    }
}
