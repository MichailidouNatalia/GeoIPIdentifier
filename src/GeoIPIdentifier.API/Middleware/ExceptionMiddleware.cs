using System.Net;
using System.Text.Json;
using GeoIPIdentifier.Domain.Exceptions;

namespace GeoIPIdentifier.API.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception has occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var response = _env.IsDevelopment()
            ? new ApiExceptionResponse("Internal Server Error", (int)HttpStatusCode.InternalServerError, exception.Message, exception.StackTrace, GetReferenceId())
            : new ApiExceptionResponse("Internal Server Error", (int)HttpStatusCode.InternalServerError, "An internal server error occurred", null, GetReferenceId());

        switch (exception)
        {
            case DomainException:
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                response.Title = "Bad Request";
                response.Status = context.Response.StatusCode;
                break;

            case KeyNotFoundException:
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                response.Title = "Not Found";
                response.Status = context.Response.StatusCode;
                break;

            case UnauthorizedAccessException:
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                response.Title = "Unauthorized";
                response.Status = context.Response.StatusCode;
                break;

            default:
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                response.Title = "Internal Server Error";
                response.Status = context.Response.StatusCode;
                break;
        }

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(response, options);

        await context.Response.WriteAsync(json);
    }

    private string GetReferenceId() => Guid.NewGuid().ToString()[..8];
}

// Response models
public class ApiExceptionResponse(string title,int status, string detail, string? stackTrace, string referenceId)
{
    public string Title { get; set; } = title;
    public int Status { get; set; } = status;
  public string Detail { get; set; } = detail;
  public string? StackTrace { get; set; } = stackTrace;
  public string ReferenceId { get; set; } = referenceId;
  public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}