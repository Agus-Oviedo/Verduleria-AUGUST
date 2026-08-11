using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace VerduleriaAugust.Web.Services;

public sealed class PaymentMethodsService(HttpClient httpClient, IJSRuntime jsRuntime)
{
    public async Task<IReadOnlyList<PaymentMethodAdmin>> GetAllAsync(bool includeInactive = true)
    {
        using var request = await AuthorizedRequestAsync(HttpMethod.Get,
            $"api/FormasPago?pagina=1&tamanoPagina=100&incluirInactivas={includeInactive.ToString().ToLowerInvariant()}");
        using var response = await httpClient.SendAsync(request);
        EnsureAuthorized(response);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PaymentMethodPage>())?.Items ?? [];
    }

    public Task CreateAsync(string name, bool isCash, bool active, int order) =>
        SaveAsync(HttpMethod.Post, "api/FormasPago", name, isCash, active, order);

    public Task UpdateAsync(int id, string name, bool isCash, bool active, int order) =>
        SaveAsync(HttpMethod.Put, $"api/FormasPago/{id}", name, isCash, active, order);

    private async Task SaveAsync(HttpMethod method, string uri, string name, bool isCash, bool active, int order)
    {
        using var request = await AuthorizedRequestAsync(method, uri);
        request.Content = JsonContent.Create(new { nombre = name, esEfectivo = isCash, activa = active, orden = order });
        using var response = await httpClient.SendAsync(request);
        EnsureAuthorized(response);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>();
            throw new InvalidOperationException(error?.Mensaje ?? "No se pudo guardar la forma de pago.");
        }
    }

    private async Task<HttpRequestMessage> AuthorizedRequestAsync(HttpMethod method, string uri)
    {
        var token = await jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", "august.auth.token");
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static void EnsureAuthorized(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException("La sesión venció. Volvé a iniciar sesión.");
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException("Tu rol no permite administrar formas de pago.");
    }

    private sealed record PaymentMethodPage(IReadOnlyList<PaymentMethodAdmin> Items, int Total, int Pagina, int TamanoPagina);
    private sealed record ApiError(string Mensaje);
}

public sealed record PaymentMethodAdmin(int Id, string Nombre, bool EsEfectivo, bool Activa, int Orden, int CantidadUsos);
