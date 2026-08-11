using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;

namespace VerduleriaAugust.Web.Services;

public sealed class AuthService(
    HttpClient httpClient,
    IJSRuntime jsRuntime,
    SessionAuthenticationStateProvider authenticationStateProvider)
{
    public async Task<LoginOutcome> LoginAsync(string userName, string password)
    {
        using var response = await httpClient.PostAsJsonAsync("api/Auth/login", new
        {
            nombreUsuario = userName,
            password
        });

        if (!response.IsSuccessStatusCode)
        {
            var message = "No se pudo iniciar sesión.";
            try
            {
                var error = await response.Content.ReadFromJsonAsync<ApiError>();
                if (!string.IsNullOrWhiteSpace(error?.Mensaje)) message = error.Mensaje;
            }
            catch (JsonException) { }
            return new LoginOutcome(false, message);
        }

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        if (string.IsNullOrWhiteSpace(login?.Token))
            return new LoginOutcome(false, "La API no devolvió un token válido.");

        await jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "august.auth.token", login.Token);
        authenticationStateProvider.NotifyAuthenticated(login.Token);
        return new LoginOutcome(true, null, login.Usuario.DebeCambiarPassword);
    }

    public async Task LogoutAsync()
    {
        await jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", "august.auth.token");
        authenticationStateProvider.NotifyLoggedOut();
    }

    public async Task<LoginOutcome> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var token = await jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", "august.auth.token");
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/Auth/cambiar-password");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { passwordActual = currentPassword, passwordNueva = newPassword });
        using var response = await httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) { var error = await response.Content.ReadFromJsonAsync<ApiError>(); return new(false, error?.Mensaje ?? "No se pudo cambiar la contraseña.", false); }
        var change = await response.Content.ReadFromJsonAsync<ChangePasswordResponse>();
        if (string.IsNullOrWhiteSpace(change?.Token))
            return new(false, "La API no devolvió una sesión válida después del cambio.", false);
        await jsRuntime.InvokeVoidAsync("sessionStorage.setItem", "august.auth.token", change.Token);
        authenticationStateProvider.NotifyAuthenticated(change.Token);
        return new(true, null, false);
    }

    private sealed record LoginResponse(string Token, DateTime Expira, LoginUser Usuario);
    private sealed record LoginUser(int Id, string NombreUsuario, string Rol, bool DebeCambiarPassword);
    private sealed record ChangePasswordResponse(string Mensaje, string Token, DateTime Expira);
    private sealed record ApiError(string Mensaje);
}

public sealed record LoginOutcome(bool Success, string? ErrorMessage, bool MustChangePassword = false);
