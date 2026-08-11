using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace VerduleriaAugust.Web.Services;

public sealed class ScalesService(HttpClient httpClient, IJSRuntime jsRuntime)
{
    public async Task<CurrentScaleReading?> GetCurrentReadingAsync(int cashRegisterId, CancellationToken cancellationToken = default)
    {
        using var request = await AuthorizedAsync(HttpMethod.Get, $"api/Balanzas/caja/{cashRegisterId}/lectura-actual");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        EnsureAuthorized(response);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CurrentScaleReading>(cancellationToken);
    }

    public async Task<IReadOnlyList<ScaleAdmin>> GetAllAsync()
    {
        using var request = await AuthorizedAsync(HttpMethod.Get, "api/Balanzas?pagina=1&tamanoPagina=100");
        using var response = await httpClient.SendAsync(request);
        EnsureAuthorized(response); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ScalePage>())?.Items ?? [];
    }

    public async Task<CreatedScale> CreateAsync(ScaleInput input)
    {
        using var request = await AuthorizedAsync(HttpMethod.Post, "api/Balanzas");
        request.Content = JsonContent.Create(input);
        using var response = await httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<CreatedScale>())!;
    }

    public async Task UpdateAsync(int id, ScaleInput input)
    {
        using var request = await AuthorizedAsync(HttpMethod.Put, $"api/Balanzas/{id}");
        request.Content = JsonContent.Create(input);
        using var response = await httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
    }

    public async Task<RegeneratedKey> RegenerateKeyAsync(int id)
    {
        using var request = await AuthorizedAsync(HttpMethod.Post, $"api/Balanzas/{id}/regenerar-clave");
        using var response = await httpClient.SendAsync(request);
        await EnsureSuccessAsync(response);
        return (await response.Content.ReadFromJsonAsync<RegeneratedKey>())!;
    }

    private async Task<HttpRequestMessage> AuthorizedAsync(HttpMethod method, string uri)
    {
        var token = await jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", "august.auth.token");
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        EnsureAuthorized(response);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>();
            throw new InvalidOperationException(error?.Mensaje ?? "No se pudo guardar la balanza.");
        }
    }
    private static void EnsureAuthorized(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException("Tu sesión venció o no tiene permisos para administrar balanzas.");
    }
    private sealed record ScalePage(IReadOnlyList<ScaleAdmin> Items, int Total, int Pagina, int TamanoPagina);
    private sealed record ApiError(string Mensaje);
}

public sealed record ScaleAdmin(int Id, int CajaId, string? Caja, string Marca, string Modelo, string PuertoCom, int BaudRate, int DataBits, string Paridad, string StopBits, string? NumeroSerie, bool Activa, DateTime FechaCreacion, DateTime? UltimaConexion);
public sealed record ScaleInput(int CajaId, string Marca, string Modelo, string PuertoCom, int BaudRate, int DataBits, string Paridad, string StopBits, string? NumeroSerie, bool Activa);
public sealed record CreatedScale(string Mensaje, ScaleAdmin Balanza, string ApiKey);
public sealed record RegeneratedKey(string Mensaje, int BalanzaId, string ApiKey);
public sealed record CurrentScaleReading(Guid LecturaId, int BalanzaId, int CajaId, decimal PesoKg, bool Estable, DateTimeOffset FechaLectura, DateTimeOffset FechaRecepcion, bool Vigente);
