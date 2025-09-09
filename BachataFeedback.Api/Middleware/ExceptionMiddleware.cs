using System.Net;
using System.Text.Json;

namespace BachataFeedback.Api.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await HandleExceptionAsync(httpContext, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";
        var response = context.Response;

        string message;
        int statusCode;

        switch (exception)
        {
            case ApplicationException ex:
                message = ex.Message;
                statusCode = (int)HttpStatusCode.BadRequest;
                break;
            case KeyNotFoundException ex:
                message = ex.Message;
                statusCode = (int)HttpStatusCode.NotFound;
                break;
            case UnauthorizedAccessException ex:
                message = ex.Message;
                statusCode = (int)HttpStatusCode.Unauthorized;
                break;
            default:
                message = "Internal Server Error";
                statusCode = (int)HttpStatusCode.InternalServerError;
                break;
        }

        response.StatusCode = statusCode;

        var payload = new { success = false, message };
        var json = JsonSerializer.Serialize(payload);
        await context.Response.WriteAsync(json);
    }
}