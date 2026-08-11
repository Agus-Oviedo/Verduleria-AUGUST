using Microsoft.AspNetCore.Http;
using VerduleriaAugust.Api.Middleware;

namespace VerduleriaAugust.Api.Tests;

public class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task Respuesta_IncluyeCabecerasDeSeguridad()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new SecurityHeadersMiddleware(async httpContext =>
        {
            await httpContext.Response.WriteAsync("ok");
        });

        await middleware.InvokeAsync(context);
        await context.Response.CompleteAsync();

        Assert.Equal("nosniff", context.Response.Headers.XContentTypeOptions);
        Assert.Equal("DENY", context.Response.Headers.XFrameOptions);
        Assert.Equal("no-referrer", context.Response.Headers["Referrer-Policy"]);
        Assert.Equal(
            "default-src 'none'; frame-ancestors 'none'",
            context.Response.Headers["Content-Security-Policy"]);
    }
}
