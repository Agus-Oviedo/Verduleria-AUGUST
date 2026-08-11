using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace VerduleriaAugust.Web.Services;

public sealed class StockService(HttpClient httpClient, IJSRuntime jsRuntime)
{
    public async Task<PagedStockMovements> GetMovementsAsync(int page, string? search, string? type)
    {
        var uri = $"api/Stock/movimientos?pagina={page}&tamanoPagina=20";
        if (!string.IsNullOrWhiteSpace(search)) uri += $"&buscar={Uri.EscapeDataString(search.Trim())}";
        if (!string.IsNullOrWhiteSpace(type)) uri += $"&tipoMovimiento={Uri.EscapeDataString(type)}";
        var token = await jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", "august.auth.token");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await httpClient.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new UnauthorizedAccessException("La sesión venció. Volvé a iniciar sesión.");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedStockMovements>()) ?? new([], 0, page, 20, 0);
    }

    public async Task<PagedStock> GetAsync(int page, string? search, bool? lowStock)
    {
        var uri = $"api/Stock?pagina={page}&tamanoPagina=20";
        if (!string.IsNullOrWhiteSpace(search)) uri += $"&buscar={Uri.EscapeDataString(search.Trim())}";
        if (lowStock.HasValue) uri += $"&bajoStock={lowStock.Value.ToString().ToLowerInvariant()}";
        var token = await jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", "august.auth.token");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await httpClient.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new UnauthorizedAccessException("La sesión venció. Volvé a iniciar sesión.");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedStock>()) ?? new([], 0, page, 20, 0);
    }
}

public sealed record StockSummary(int Id, int ProductoId, string Producto, string Codigo, string? Categoria, decimal StockActual, string UnidadMedida, decimal? StockMinimo, DateTime FechaActualizacion, bool BajoStock);
public sealed record PagedStock(IReadOnlyList<StockSummary> Items, int Total, int Pagina, int TamanoPagina, int TotalPaginas);
public sealed record StockMovement(int Id, int ProductoId, string Producto, string Codigo, string TipoMovimiento, decimal Cantidad, decimal StockAnterior, decimal StockNuevo, string? Motivo, string? Usuario, DateTime FechaMovimiento);
public sealed record PagedStockMovements(IReadOnlyList<StockMovement> Items, int Total, int Pagina, int TamanoPagina, int TotalPaginas);
