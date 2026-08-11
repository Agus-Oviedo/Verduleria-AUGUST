using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace VerduleriaAugust.Web.Services;

public sealed class SessionAuthenticationStateProvider(IJSRuntime jsRuntime)
    : AuthenticationStateProvider
{
    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await jsRuntime.InvokeAsync<string?>(
            "sessionStorage.getItem", "august.auth.token");
        return BuildState(token);
    }

    public void NotifyAuthenticated(string token) =>
        NotifyAuthenticationStateChanged(Task.FromResult(BuildState(token)));

    public void NotifyLoggedOut() =>
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(Anonymous)));

    private static AuthenticationState BuildState(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new AuthenticationState(Anonymous);

        try
        {
            var payload = token.Split('.')[1]
                .Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var json = JsonDocument.Parse(Convert.FromBase64String(payload));

            if (json.RootElement.TryGetProperty("exp", out var exp) &&
                DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64()) <= DateTimeOffset.UtcNow)
                return new AuthenticationState(Anonymous);

            var claims = new List<Claim>();
            foreach (var property in json.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    claims.AddRange(property.Value.EnumerateArray()
                        .Select(value => new Claim(property.Name, value.ToString())));
                }
                else
                {
                    claims.Add(new Claim(property.Name, property.Value.ToString()));
                }
            }

            const string fullNameType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";
            const string fullRoleType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
            var nameType = claims.Any(claim => claim.Type == "unique_name")
                ? "unique_name"
                : fullNameType;
            var roleType = claims.Any(claim => claim.Type == "role")
                ? "role"
                : fullRoleType;
            return new AuthenticationState(new ClaimsPrincipal(
                new ClaimsIdentity(claims, "jwt", nameType, roleType)));
        }
        catch
        {
            return new AuthenticationState(Anonymous);
        }
    }
}
