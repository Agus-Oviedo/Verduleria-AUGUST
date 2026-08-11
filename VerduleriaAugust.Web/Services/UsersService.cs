using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace VerduleriaAugust.Web.Services;

public sealed class UsersService(HttpClient httpClient, IJSRuntime jsRuntime)
{
    public async Task<IReadOnlyList<UserSummary>> GetAsync()
    {
        using var response = await SendAsync(HttpMethod.Get, "api/Auth/usuarios");
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<UserSummary>>() ?? [];
    }

    public async Task<TemporaryAccess> InviteAsync(string name, string role)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/Auth/usuarios/invitacion",
            new { nombreUsuario = name, rol = role });
        return await response.Content.ReadFromJsonAsync<TemporaryAccess>()
            ?? throw new InvalidOperationException("No se recibió la contraseña temporal.");
    }

    public async Task UpdateAsync(int id, string role, bool active)
    {
        using var response = await SendAsync(HttpMethod.Put, $"api/Auth/usuarios/{id}",
            new { rol = role, activo = active });
    }

    public async Task<TemporaryAccess> ResetPasswordAsync(int id)
    {
        using var response = await SendAsync(HttpMethod.Post, $"api/Auth/usuarios/{id}/restablecer-password");
        return await response.Content.ReadFromJsonAsync<TemporaryAccess>()
            ?? throw new InvalidOperationException("No se recibió la contraseña temporal.");
    }

    public async Task<UserAuditPage> GetAuditAsync(int page = 1, int? userId = null, string? action = null)
    {
        var uri = $"api/Auth/usuarios/auditoria?pagina={page}&tamanoPagina=50";
        if (userId.HasValue) uri += $"&usuarioId={userId.Value}";
        if (!string.IsNullOrWhiteSpace(action)) uri += $"&accion={Uri.EscapeDataString(action)}";
        using var response = await SendAsync(HttpMethod.Get, uri);
        return await response.Content.ReadFromJsonAsync<UserAuditPage>()
            ?? new UserAuditPage([], 0, page, 50, 0);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, object? body = null)
    {
        var token = await jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", "august.auth.token");
        using var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);

        var response = await httpClient.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            throw new UnauthorizedAccessException("La sesión venció.");
        }
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            response.Dispose();
            throw new UnauthorizedAccessException("Solo un administrador puede gestionar usuarios.");
        }
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<UserError>();
            response.Dispose();
            throw new InvalidOperationException(error?.Mensaje ?? "No se pudo completar la operación.");
        }
        return response;
    }

    private sealed record UserError(string Mensaje);
}

public sealed record UserSummary(int Id, string NombreUsuario, string Rol, bool Activo, DateTime FechaCreacion);
public sealed record TemporaryAccess(string Mensaje, string PasswordTemporal, int? Id = null, string? NombreUsuario = null, string? Rol = null);
public sealed record UserAuditItem(int Id, int UsuarioObjetivoId, string UsuarioObjetivo, int? UsuarioActorId, string UsuarioActor, string Accion, string Detalle, DateTime Fecha);
public sealed record UserAuditPage(IReadOnlyList<UserAuditItem> Items, int Total, int Pagina, int TamanoPagina, int TotalPaginas);
