using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using VerduleriaAugust.Api.Middleware;

namespace VerduleriaAugust.Api.Tests;

public class GlobalExceptionMiddlewareTests
{
    [Fact]
    public async Task ExcepcionGeneral_EnProduccion_Devuelve500SinDetalleInterno()
    {
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(
            _ => throw new InvalidOperationException("información sensible"),
            Environments.Production);

        await middleware.InvokeAsync(context);

        var json = await ReadResponseAsync(context);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("ERROR_INTERNO", json.GetProperty("codigo").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("detalle").ValueKind);
        Assert.Equal("trace-prueba", json.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task ExcepcionGeneral_EnDesarrollo_IncluyeDetalleTecnico()
    {
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(
            _ => throw new InvalidOperationException("detalle para desarrollo"),
            Environments.Development);

        await middleware.InvokeAsync(context);

        var json = await ReadResponseAsync(context);
        Assert.Equal("detalle para desarrollo", json.GetProperty("detalle").GetString());
    }

    [Fact]
    public async Task ConflictoDeConcurrencia_Devuelve409YCodigoEspecifico()
    {
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(
            _ => throw new DbUpdateConcurrencyException("conflicto"),
            Environments.Production);

        await middleware.InvokeAsync(context);

        var json = await ReadResponseAsync(context);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal("CONFLICTO_CONCURRENCIA", json.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task SolicitudValida_ContinuaSinModificarLaRespuesta()
    {
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(async httpContext =>
        {
            httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
            await Task.CompletedTask;
        }, Environments.Production);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    private static GlobalExceptionMiddleware CreateMiddleware(
        RequestDelegate next,
        string environmentName)
    {
        return new GlobalExceptionMiddleware(
            next,
            NullLogger<GlobalExceptionMiddleware>.Instance,
            new TestHostEnvironment { EnvironmentName = environmentName });
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        return new DefaultHttpContext
        {
            TraceIdentifier = "trace-prueba",
            Response = { Body = new MemoryStream() }
        };
    }

    private static async Task<JsonElement> ReadResponseAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
