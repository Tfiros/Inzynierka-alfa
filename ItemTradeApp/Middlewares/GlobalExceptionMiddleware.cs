using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace ItemTradeApp.Middlewares;

public sealed class GlobalExceptionMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionMiddleware> logger,
    IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            logger.LogInformation(
                "Request was cancelled by client. TraceId: {TraceId}",
                context.TraceIdentifier);

            context.Response.StatusCode = 499;
        }
        catch (JsonException ex)
        {
            await WriteProblemAsync(
                context,
                ex,
                HttpStatusCode.BadRequest,
                "invalid_json",
                "Invalid request JSON.");
        }
        catch (InvalidOperationException ex)
        {
            await WriteProblemAsync(
                context,
                ex,
                HttpStatusCode.BadRequest,
                "invalid_operation",
                "Invalid operation.");
        }
        catch (ArgumentException ex)
        {
            await WriteProblemAsync(
                context,
                ex,
                HttpStatusCode.BadRequest,
                "invalid_argument",
                "Invalid request.");
        }
        catch (KeyNotFoundException ex)
        {
            await WriteProblemAsync(
                context,
                ex,
                HttpStatusCode.NotFound,
                "resource_not_found",
                "Resource was not found.");
        }
        catch (UnauthorizedAccessException ex)
        {
            await WriteProblemAsync(
                context,
                ex,
                HttpStatusCode.Forbidden,
                "access_denied",
                "Access denied.");
        }
        catch (Exception ex)
        {
            await WriteProblemAsync(
                context,
                ex,
                HttpStatusCode.InternalServerError,
                "internal_server_error",
                "Unexpected server error.");
        }
    }

    private async Task WriteProblemAsync(
        HttpContext context,
        Exception exception,
        HttpStatusCode statusCode,
        string errorCode,
        string safeMessage)
    {
        var traceId =
            Activity.Current?.Id ??
            context.TraceIdentifier;

        logger.LogError(
            exception,
            "Unhandled exception. ErrorCode: {ErrorCode}, TraceId: {TraceId}, Path: {Path}, Method: {Method}",
            errorCode,
            traceId,
            context.Request.Path,
            context.Request.Method);

        if (context.Response.HasStarted)
        {
            logger.LogWarning("Response has already started. Cannot write ProblemDetails. TraceId: {TraceId}", traceId);
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = safeMessage,
            Detail = env.IsDevelopment() ? exception.Message : null,
            Type = $"https://itemtradeapp/errors/{errorCode}",
            Instance = context.Request.Path
        };

        problem.Extensions["errorCode"] = errorCode;
        problem.Extensions["traceId"] = traceId;

        await context.Response.WriteAsJsonAsync(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}