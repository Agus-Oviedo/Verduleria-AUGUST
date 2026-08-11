using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;

namespace VerduleriaAugust.Web.Services;

public sealed class SalesService(HttpClient httpClient, IJSRuntime jsRuntime)
{
    public async Task<PagedSales> GetSalesAsync(int page, string? search = null, string? status = null)
    {
        var uri = $"api/Ventas?pagina={page}&tamanoPagina=20";
        if (!string.IsNullOrWhiteSpace(search)) uri += $"&buscar={Uri.EscapeDataString(search.Trim())}";
        if (!string.IsNullOrWhiteSpace(status)) uri += $"&estado={Uri.EscapeDataString(status)}";
        using var request = await AuthorizedRequestAsync(HttpMethod.Get, uri);
        using var response = await httpClient.SendAsync(request);
        EnsureAuthorized(response);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedSales>()) ?? new([], 0, page, 20, 0);
    }

    public async Task<SaleDetail> GetSaleAsync(int id)
    {
        using var request = await AuthorizedRequestAsync(HttpMethod.Get, $"api/Ventas/{id}"); using var response = await httpClient.SendAsync(request); EnsureAuthorized(response); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SaleDetail>()) ?? throw new InvalidOperationException("No se pudo leer la venta.");
    }

    public async Task CancelSaleAsync(int id, string reason)
    {
        using var request = await AuthorizedRequestAsync(HttpMethod.Post, $"api/Ventas/{id}/anular"); request.Content = JsonContent.Create(new { motivo = reason }); using var response = await httpClient.SendAsync(request); EnsureAuthorized(response);
        if (!response.IsSuccessStatusCode) { var error = await response.Content.ReadFromJsonAsync<ApiError>(); throw new InvalidOperationException(error?.Mensaje ?? "No se pudo anular la venta."); }
    }

    public async Task<IReadOnlyList<PaymentMethod>> GetPaymentMethodsAsync()
    {
        using var request = await AuthorizedRequestAsync(HttpMethod.Get, "api/FormasPago?pagina=1&tamanoPagina=100");
        using var response = await httpClient.SendAsync(request);
        EnsureAuthorized(response);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedPaymentMethods>())?.Items ?? [];
    }

    public async Task<SaleCreated> CreateAsync(CreateSaleRequest sale)
    {
        using var request = await AuthorizedRequestAsync(HttpMethod.Post, "api/Ventas");
        request.Content = JsonContent.Create(sale);
        using var response = await httpClient.SendAsync(request);
        EnsureAuthorized(response);
        if (!response.IsSuccessStatusCode)
        {
            var message = "No se pudo registrar la venta.";
            try { message = (await response.Content.ReadFromJsonAsync<ApiError>())?.Mensaje ?? message; }
            catch (JsonException) { }
            throw new InvalidOperationException(message);
        }
        return (await response.Content.ReadFromJsonAsync<SaleCreated>())
            ?? throw new InvalidOperationException("La API no devolvió la venta creada.");
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

    private sealed record PagedPaymentMethods(IReadOnlyList<PaymentMethod> Items, int Total, int Pagina, int TamanoPagina);
    private sealed record ApiError(string Mensaje);
}

public sealed record PaymentMethod(int Id, string Nombre, bool EsEfectivo, bool Activa);
public sealed record CreateSaleItem(int ProductoId, string TipoVenta, decimal Cantidad, Guid? LecturaBalanzaId = null);
public sealed record CreateSalePayment(int FormaPagoId, decimal Importe);
public sealed record CreateSaleRequest(string IdempotencyKey, int CajaId, decimal Descuento, string? MotivoDescuento, IReadOnlyList<CreateSaleItem> Items, IReadOnlyList<CreateSalePayment> Pagos);
public sealed record SaleCreated(string Mensaje, int Id, string NumeroVenta, decimal Subtotal, decimal Descuento, decimal Total, string? IdempotencyKey);
public sealed record SaleSummary(int Id, string NumeroVenta, string? IdempotencyKey, int CajaId, string? Caja, DateTime FechaVenta, decimal Subtotal, decimal Descuento, decimal Total, string Estado, string? FormasPago);
public sealed record PagedSales(IReadOnlyList<SaleSummary> Items, int Total, int Pagina, int TamanoPagina, int TotalPaginas);
public sealed record SaleLineDetail(int Id, int ProductoId, string? Producto, string TipoVenta, decimal Cantidad, decimal PrecioUnitario, decimal TotalLinea);
public sealed record SalePaymentDetail(int Id, int FormaPagoId, string? FormaPago, decimal Importe);
public sealed record SaleCancellationDetail(string Motivo, DateTime FechaAnulacion, string? Usuario);
public sealed record SaleDiscountAudit(string Motivo, int UsuarioId, string? Usuario);
public sealed record SaleDetail(int Id, string NumeroVenta, string? IdempotencyKey, int CajaId, string? Caja, DateTime FechaVenta, decimal Subtotal, decimal Descuento, decimal Total, string Estado, IReadOnlyList<SaleLineDetail> Detalles, IReadOnlyList<SalePaymentDetail> Pagos, SaleDiscountAudit? AuditoriaDescuento, SaleCancellationDetail? Anulacion);
