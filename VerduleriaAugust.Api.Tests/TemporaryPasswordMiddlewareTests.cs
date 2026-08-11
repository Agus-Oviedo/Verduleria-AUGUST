using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using VerduleriaAugust.Api.Middleware;
using VerduleriaAugust.Api.Services;

namespace VerduleriaAugust.Api.Tests;

public class TemporaryPasswordMiddlewareTests
{
    [Fact]
    public async Task PasswordTemporal_BloqueaOtrosEndpoints()
    {
        var nextCalled = false;
        var middleware = new TemporaryPasswordMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = AuthenticatedContext("true", "/api/ventas");

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task PasswordTemporal_PermiteCambiarPassword()
    {
        var nextCalled = false;
        var middleware = new TemporaryPasswordMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = AuthenticatedContext("true", "/api/Auth/cambiar-password");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    private static DefaultHttpContext AuthenticatedContext(string claimValue, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(JwtTokenService.PasswordChangeRequiredClaim, claimValue)
        ], "TestAuthentication"));
        return context;
    }
}
