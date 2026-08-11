using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace VerduleriaAugust.Web.Services;

public sealed class ProductsService(HttpClient httpClient, IJSRuntime jsRuntime)
{
    public async Task<IReadOnlyList<CategorySummary>> GetCategoriesAsync()
    {
        using var request = await AuthorizedRequestAsync(HttpMethod.Get, "api/Categorias?pagina=1&tamanoPagina=100&activa=true");
        using var response = await httpClient.SendAsync(request); EnsureAuthorized(response); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedCategories>())?.Items ?? [];
    }

    public async Task<PagedCategoriesResult> GetCategoriesPagedAsync(int page, string? search, bool? active)
    {
        var uri = $"api/Categorias?pagina={page}&tamanoPagina=20";
        if (!string.IsNullOrWhiteSpace(search)) uri += $"&buscar={Uri.EscapeDataString(search.Trim())}";
        if (active.HasValue) uri += $"&activa={active.Value.ToString().ToLowerInvariant()}";
        using var request = await AuthorizedRequestAsync(HttpMethod.Get, uri); using var response = await httpClient.SendAsync(request); EnsureAuthorized(response); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedCategoriesResult>()) ?? new([], 0, page, 20, 0);
    }

    public async Task SaveCategoryAsync(int? id, string name, bool active)
    {
        using var request = await AuthorizedRequestAsync(id.HasValue ? HttpMethod.Put : HttpMethod.Post, id.HasValue ? $"api/Categorias/{id}" : "api/Categorias");
        request.Content = JsonContent.Create(new { nombre = name, activa = active }); using var response = await httpClient.SendAsync(request); EnsureAuthorized(response);
        if (!response.IsSuccessStatusCode) { var error = await response.Content.ReadFromJsonAsync<ApiProductError>(); throw new InvalidOperationException(error?.Mensaje ?? "No se pudo guardar la categoría."); }
    }

    public async Task<IReadOnlyList<PriceHistoryEntry>> GetPriceHistoryAsync(int productId)
    {
        using var request = await AuthorizedRequestAsync(HttpMethod.Get, $"api/Productos/{productId}/historial-precios?pagina=1&tamanoPagina=100");
        using var response = await httpClient.SendAsync(request); EnsureAuthorized(response);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("No se pudo consultar el historial de precios.");
        return (await response.Content.ReadFromJsonAsync<PagedPriceHistory>())?.Items ?? [];
    }

    public async Task<PurchaseCostInfo> GetPurchaseCostsAsync(int productId)
    {
        using var request = await AuthorizedRequestAsync(HttpMethod.Get, $"api/Productos/{productId}/costos-compra");
        using var response = await httpClient.SendAsync(request); EnsureAuthorized(response);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("No se pudieron consultar los costos de compra.");
        return await response.Content.ReadFromJsonAsync<PurchaseCostInfo>() ?? new(null, null, null, null, null, 0);
    }

    public async Task SaveAsync(int? id, SaveProductRequest product)
    {
        using var request = await AuthorizedRequestAsync(id.HasValue ? HttpMethod.Put : HttpMethod.Post,
            id.HasValue ? $"api/Productos/{id}" : "api/Productos");
        request.Content = JsonContent.Create(product);
        using var response = await httpClient.SendAsync(request); EnsureAuthorized(response);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiProductError>();
            throw new InvalidOperationException(error?.Mensaje ?? "No se pudo guardar el producto.");
        }
    }

    public async Task<PagedProductsResult> GetPagedAsync(int page, string? search, bool? active)
    {
        var uri = $"api/Productos?pagina={page}&tamanoPagina=20";
        if (!string.IsNullOrWhiteSpace(search)) uri += $"&buscar={Uri.EscapeDataString(search.Trim())}";
        if (active.HasValue) uri += $"&activo={active.Value.ToString().ToLowerInvariant()}";
        using var request = await AuthorizedRequestAsync(HttpMethod.Get, uri);
        using var response = await httpClient.SendAsync(request);
        EnsureAuthorized(response);
        response.EnsureSuccessStatusCode();
        var pageResult = await response.Content.ReadFromJsonAsync<PagedProductsResult>();
        return pageResult ?? new([], 0, page, 20, 0);
    }

    public async Task<IReadOnlyList<ProductSummary>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        using var request = await AuthorizedRequestAsync(HttpMethod.Get, "api/Productos?pagina=1&tamanoPagina=100&activo=true");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        EnsureAuthorized(response);

        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedProducts>(cancellationToken);
        return page?.Items ?? [];
    }

    private async Task<HttpRequestMessage> AuthorizedRequestAsync(HttpMethod method, string uri)
    {
        var token = await jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", "august.auth.token");
        var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(token)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static void EnsureAuthorized(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized) throw new UnauthorizedAccessException("La sesión venció. Volvé a iniciar sesión.");
    }

    private sealed record PagedProducts(IReadOnlyList<ProductSummary> Items, int Total, int Pagina, int TamanoPagina);
    private sealed record PagedCategories(IReadOnlyList<CategorySummary> Items, int Total, int Pagina, int TamanoPagina);
    private sealed record PagedPriceHistory(IReadOnlyList<PriceHistoryEntry> Items, int Total, int Pagina, int TamanoPagina);
    private sealed record ApiProductError(string Mensaje);
}

public sealed record ProductSummary(
    int Id,
    string Codigo,
    string Nombre,
    int CategoriaId,
    string? Categoria,
    string TipoVenta,
    decimal? PrecioPorKilo,
    decimal? PrecioPorUnidad,
    bool Activo,
    ProductStock? Stock);

public sealed record ProductStock(decimal StockActual, string UnidadMedida, decimal? StockMinimo, DateTime FechaActualizacion);
public sealed record PagedProductsResult(IReadOnlyList<ProductSummary> Items, int Total, int Pagina, int TamanoPagina, int TotalPaginas);
public sealed record CategorySummary(int Id, string Nombre, bool Activa, DateTime FechaCreacion);
public sealed record SaveProductRequest(string Codigo, string Nombre, int CategoriaId, string TipoVenta, decimal? PrecioPorKilo, decimal? PrecioPorUnidad, decimal StockInicial, string UnidadMedida, decimal? StockMinimo, string? MotivoCambioPrecio, bool Activo);
public sealed record PriceHistoryEntry(int Id, decimal? PrecioPorKiloAnterior, decimal? PrecioPorKiloNuevo, decimal? PrecioPorUnidadAnterior, decimal? PrecioPorUnidadNuevo, string Motivo, DateTime FechaCambio, string? Usuario);
public sealed record PurchaseCostInfo(decimal? UltimoCosto, decimal? CostoPromedio, DateTime? FechaUltimaCompra, string? ProveedorUltimaCompra, string? NumeroRecepcion, decimal CantidadConsiderada);
public sealed record PagedCategoriesResult(IReadOnlyList<CategorySummary> Items, int Total, int Pagina, int TamanoPagina, int TotalPaginas);
