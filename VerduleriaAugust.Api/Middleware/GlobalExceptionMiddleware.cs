using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VerduleriaAugust.Api.DTOs;

namespace VerduleriaAugust.Api.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            await WriteErrorAsync(context, exception);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, Exception exception)
    {
        var (statusCode, code, message) = exception switch
        {
            BadHttpRequestException => (
                StatusCodes.Status400BadRequest,
                "SOLICITUD_INVALIDA",
                "La solicitud enviada no es válida."),
            JsonException => (
                StatusCodes.Status400BadRequest,
                "JSON_INVALIDO",
                "El contenido JSON enviado no es válido."),
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "CONFLICTO_CONCURRENCIA",
                "Los datos fueron modificados por otra operación."),
            DbUpdateException => (
                StatusCodes.Status409Conflict,
                "CONFLICTO_DATOS",
                "No se pudieron guardar los datos por un conflicto de integridad."),
            _ => (
                StatusCodes.Status500InternalServerError,
                "ERROR_INTERNO",
                "Ocurrió un error interno al procesar la solicitud.")
        };

        _logger.LogError(
            exception,
            "Error no controlado {ErrorCode} en {Method} {Path}. TraceId: {TraceId}",
            code,
            context.Request.Method,
            context.Request.Path,
            context.TraceIdentifier);

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var response = new ApiErrorResponse(
            message,
            code,
            context.TraceIdentifier,
            Detalle: _environment.IsDevelopment() ? exception.Message : null);

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            response,
            JsonOptions,
            context.RequestAborted);
    }

}
