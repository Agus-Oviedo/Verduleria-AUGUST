namespace VerduleriaAugust.Api.Middleware;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers.Append("Referrer-Policy", "no-referrer");
        headers.Append("Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");

        await _next(context);
    }
}
