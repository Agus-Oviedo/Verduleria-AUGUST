using VerduleriaAugust.Api.Services;

namespace VerduleriaAugust.Api.Middleware;

public sealed class TemporaryPasswordMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var mustChangePassword = context.User.FindFirst(JwtTokenService.PasswordChangeRequiredClaim)?.Value;
        var isPasswordChangeEndpoint = context.Request.Path.Equals(
            "/api/Auth/cambiar-password",
            StringComparison.OrdinalIgnoreCase);

        if (context.User.Identity?.IsAuthenticated == true &&
            string.Equals(mustChangePassword, "true", StringComparison.OrdinalIgnoreCase) &&
            !isPasswordChangeEndpoint)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new
            {
                mensaje = "Debés cambiar la contraseña temporal antes de continuar.",
                codigo = "CAMBIO_PASSWORD_REQUERIDO",
                traceId = context.TraceIdentifier
            });
            return;
        }

        await next(context);
    }
}
