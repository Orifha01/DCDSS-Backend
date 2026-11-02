using System.Diagnostics;
using System.Text.Json;
using DCDSS.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DCDSS.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IWebHostEnvironment _env;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IWebHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex)
        {
            await HandleAsync(ctx, ex);
        }
    }

    private async Task HandleAsync(HttpContext ctx, Exception ex)
    {
        var traceId = Activity.Current?.Id ?? ctx.TraceIdentifier;
        bool isDev = _env.IsDevelopment();

        int status;
        string title;
        string? detail;

        switch (ex)
        {
            case NotFoundException nfe:
                status = StatusCodes.Status404NotFound;
                title = "Not Found";
                detail = isDev ? nfe.Message : null;
                _logger.LogWarning(nfe, "Not found: {Message}", nfe.Message);
                break;

            case ConflictException cfe:
                status = StatusCodes.Status409Conflict;
                title = "Conflict";
                detail = isDev ? cfe.Message : null;
                _logger.LogWarning(cfe, "Conflict: {Message}", cfe.Message);
                break;

            case BusinessRuleException bre:
                status = StatusCodes.Status400BadRequest;
                title = "Business Rule Violation";
                detail = isDev ? bre.Message : null;
                _logger.LogWarning(bre, "Business rule violation: {Message}", bre.Message);
                break;

            case UnauthorizedAccessException uae:
                status = StatusCodes.Status401Unauthorized;
                title = "Unauthorized";
                detail = isDev ? uae.Message : null;
                _logger.LogInformation(uae, "Unauthorized: {Message}", uae.Message);
                break;

            case DbUpdateException dbe:
                status = StatusCodes.Status409Conflict;
                title = "Data Conflict";
                detail = isDev ? dbe.Message : "A data conflict occurred. Please check your input and try again.";
                _logger.LogError(dbe, "DbUpdateException: {Message}", dbe.Message);
                break;

            default:
                status = StatusCodes.Status500InternalServerError;
                title = "Internal Server Error";
                detail = isDev ? ex.Message : "An unexpected error occurred. Please try again later.";
                _logger.LogError(ex, "Unhandled exception: {Message}\n{StackTrace}", ex.Message, ex.StackTrace);
                break;
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = ctx.Request.Path
        };
        problem.Extensions["traceId"] = traceId;

        ctx.Response.ContentType = "application/problem+json";
        ctx.Response.StatusCode = status;

        var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await ctx.Response.WriteAsync(json);
    }
}
