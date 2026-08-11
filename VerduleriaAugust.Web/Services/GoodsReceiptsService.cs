using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;

namespace VerduleriaAugust.Web.Services;

public sealed class GoodsReceiptsService(HttpClient http, IJSRuntime js)
{
    public async Task<IReadOnlyList<SupplierItem>> GetSuppliersAsync() =>
        await SendAsync<IReadOnlyList<SupplierItem>>(HttpMethod.Get, "api/recepciones-mercaderia/proveedores") ?? [];

    public async Task<IReadOnlyList<GoodsReceiptItem>> GetHistoryAsync() =>
        await SendAsync<IReadOnlyList<GoodsReceiptItem>>(HttpMethod.Get, "api/recepciones-mercaderia") ?? [];

    public async Task<IReadOnlyList<GoodsReceiptAuditItem>> GetAuditAsync(int id) =>
        await SendAsync<IReadOnlyList<GoodsReceiptAuditItem>>(HttpMethod.Get, $"api/recepciones-mercaderia/{id}/auditoria") ?? [];

    public async Task<SupplierItem> CreateSupplierAsync(string name, string? cuit, string? contact) =>
        await SendAsync<SupplierItem>(HttpMethod.Post, "api/recepciones-mercaderia/proveedores", new { nombre = name, cuit, contacto = contact })
        ?? throw new InvalidOperationException("La API no devolvió el proveedor creado.");

    public async Task<ReceiptCreatedResult> CreateReceiptAsync(int supplierId, string? document, string? notes, IReadOnlyList<GoodsReceiptLineRequest> items) =>
        await SendAsync<ReceiptCreatedResult>(HttpMethod.Post, "api/recepciones-mercaderia", new { proveedorId = supplierId, comprobante = document, observaciones = notes, items })
        ?? throw new InvalidOperationException("La API no devolvió el comprobante de recepción.");

    public async Task<string> UpdateReceiptDataAsync(int id, int supplierId, string? document, string? notes, string reason) =>
        (await SendAsync<OperationResult>(HttpMethod.Put, $"api/recepciones-mercaderia/{id}/datos", new { proveedorId = supplierId, comprobante = document, observaciones = notes, motivo = reason }))?.Mensaje
        ?? "Datos actualizados.";

    public async Task<string> CancelReceiptAsync(int id, string reason) =>
        (await SendAsync<OperationResult>(HttpMethod.Post, $"api/recepciones-mercaderia/{id}/anular", new { motivo = reason }))?.Mensaje
        ?? "Recepción anulada.";

    public async Task<string> CorrectReceiptAsync(int id, string reason, IReadOnlyList<ReceiptCorrectionRequest> items) =>
        (await SendAsync<OperationResult>(HttpMethod.Post, $"api/recepciones-mercaderia/{id}/corregir", new { motivo = reason, items }))?.Mensaje
        ?? "Corrección registrada.";

    private async Task<T?> SendAsync<T>(HttpMethod method, string uri, object? body = null)
    {
        var token = await js.InvokeAsync<string?>("sessionStorage.getItem", "august.auth.token");
        using var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        using var response = await http.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var message = response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => "Tu sesión venció. Volvé a iniciar sesión.",
                System.Net.HttpStatusCode.Forbidden => "Tu usuario no tiene permiso para realizar esta operación.",
                _ => "No se pudo completar la operación."
            };

            if (!string.IsNullOrWhiteSpace(content))
            {
                try
                {
                    var error = JsonSerializer.Deserialize<ApiError>(content, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    if (!string.IsNullOrWhiteSpace(error?.Mensaje))
                        message = error.Mensaje;
                }
                catch (JsonException)
                {
                    // Algunas respuestas de infraestructura no incluyen JSON.
                }
            }

            throw new InvalidOperationException(message);
        }

        if (string.IsNullOrWhiteSpace(content))
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("La API devolvió una respuesta inválida. Actualizá la pantalla e intentá nuevamente.");
        }
    }

    private sealed record ApiError(string Mensaje);
}

public sealed record SupplierItem(int Id, string Nombre, string? Cuit, string? Contacto, bool Activo, DateTime FechaCreacion);
public sealed record GoodsReceiptLineRequest(int ProductoId, decimal Cantidad, decimal? CostoUnitario);
public sealed record ReceiptCorrectionRequest(int ProductoId, decimal Cantidad);
public sealed record ReceiptCreatedResult(string Mensaje, int Id, string NumeroRecepcion, decimal TotalCosto);
public sealed record OperationResult(string Mensaje);
public sealed record GoodsReceiptAuditItem(int Id, string Tipo, string Detalle, DateTime Fecha, string Usuario);
public sealed record GoodsReceiptDetail(int ProductoId, string Producto, decimal Cantidad, decimal CantidadCorregida, decimal? CostoUnitario, decimal? TotalCosto)
{
    public decimal CantidadPendiente => Cantidad - CantidadCorregida;
}
public sealed record GoodsReceiptItem(int Id, string NumeroRecepcion, DateTime FechaRecepcion, int ProveedorId, string Proveedor, string Usuario, string? Comprobante, string? Observaciones, decimal TotalCosto, string Estado, IReadOnlyList<GoodsReceiptDetail> Detalles);
