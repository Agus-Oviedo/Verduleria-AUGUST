using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace VerduleriaAugust.Web.Services;

public sealed class CashRegistersService(HttpClient httpClient, IJSRuntime jsRuntime)
{
    public async Task<IReadOnlyList<CashRegisterSummary>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        using var request = await AuthorizedRequestAsync(HttpMethod.Get, "api/Cajas?pagina=1&tamanoPagina=100&activa=true");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        EnsureAuthorized(response);
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<PagedCashRegisters>(cancellationToken);
        return page?.Items ?? [];
    }

    public async Task<OpenRegisterResult> OpenAsync(int cashRegisterId, decimal openingBalance, CancellationToken cancellationToken = default)
    {
        using var request = await AuthorizedRequestAsync(HttpMethod.Post, $"api/Cajas/{cashRegisterId}/abrir");
        request.Content = JsonContent.Create(new { saldoInicial = openingBalance });
        using var response = await httpClient.SendAsync(request, cancellationToken);
        EnsureAuthorized(response);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken);
            throw new InvalidOperationException(error?.Mensaje ?? "No se pudo abrir la caja.");
        }

        return (await response.Content.ReadFromJsonAsync<OpenRegisterResult>(cancellationToken))
            ?? throw new InvalidOperationException("La API no devolvió la sesión abierta.");
    }

    public async Task<CloseRegisterResult> CloseAsync(int sessionId, decimal declaredCash, string? notes, CancellationToken cancellationToken = default)
    {
        using var request = await AuthorizedRequestAsync(HttpMethod.Post, $"api/Cajas/sesiones/{sessionId}/cerrar");
        request.Content = JsonContent.Create(new { efectivoDeclarado = declaredCash, observaciones = notes });
        using var response = await httpClient.SendAsync(request, cancellationToken); EnsureAuthorized(response);
        if (!response.IsSuccessStatusCode) { var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken); throw new InvalidOperationException(error?.Mensaje ?? "No se pudo cerrar la caja."); }
        return (await response.Content.ReadFromJsonAsync<CloseRegisterResult>(cancellationToken)) ?? throw new InvalidOperationException("La API no devolvió el arqueo.");
    }

    public async Task<CashReconciliation> GetReconciliationAsync(int sessionId, CancellationToken cancellationToken = default)
    {
        using var request = await AuthorizedRequestAsync(HttpMethod.Get, $"api/Cajas/sesiones/{sessionId}/arqueo");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        EnsureAuthorized(response);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken);
            throw new InvalidOperationException(error?.Mensaje ?? "No se pudo calcular el arqueo.");
        }
        return (await response.Content.ReadFromJsonAsync<CashReconciliation>(cancellationToken))
            ?? throw new InvalidOperationException("La API no devolvió el detalle del arqueo.");
    }

    public async Task AddCashMovementAsync(int sessionId, string type, decimal amount, string reason, CancellationToken cancellationToken = default)
    {
        using var request = await AuthorizedRequestAsync(HttpMethod.Post, $"api/Cajas/sesiones/{sessionId}/movimientos");
        request.Content = JsonContent.Create(new { tipo = type, importe = amount, motivo = reason });
        using var response = await httpClient.SendAsync(request, cancellationToken); EnsureAuthorized(response);
        if (!response.IsSuccessStatusCode) { var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken); throw new InvalidOperationException(error?.Mensaje ?? "No se pudo registrar el movimiento de caja."); }
    }

    public async Task CancelCashMovementAsync(int movementId, string reason, CancellationToken cancellationToken = default)
    {
        using var request = await AuthorizedRequestAsync(HttpMethod.Post, $"api/Cajas/movimientos/{movementId}/anular");
        request.Content = JsonContent.Create(new { motivo = reason });
        using var response = await httpClient.SendAsync(request, cancellationToken); EnsureAuthorized(response);
        if (!response.IsSuccessStatusCode) { var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken); throw new InvalidOperationException(error?.Mensaje ?? "No se pudo anular el movimiento."); }
    }

    public async Task<CashShiftPage> GetShiftsAsync(CashShiftFilters filters, CancellationToken cancellationToken = default)
    {
        var query = new List<string>
        {
            $"pagina={filters.Page}",
            $"tamanoPagina={filters.PageSize}"
        };
        if (filters.CashRegisterId.HasValue) query.Add($"cajaId={filters.CashRegisterId.Value}");
        if (!string.IsNullOrWhiteSpace(filters.User)) query.Add($"usuario={Uri.EscapeDataString(filters.User.Trim())}");
        if (!string.IsNullOrWhiteSpace(filters.Status)) query.Add($"estado={Uri.EscapeDataString(filters.Status)}");
        if (filters.From.HasValue) query.Add($"desde={Uri.EscapeDataString(filters.From.Value.ToString("O"))}");
        if (filters.To.HasValue) query.Add($"hasta={Uri.EscapeDataString(filters.To.Value.ToString("O"))}");
        using var request = await AuthorizedRequestAsync(HttpMethod.Get, $"api/Cajas/sesiones?{string.Join("&", query)}");
        using var response = await httpClient.SendAsync(request, cancellationToken);
        EnsureAuthorized(response);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CashShiftPage>(cancellationToken))
            ?? new CashShiftPage([], 0, filters.Page, filters.PageSize, 0);
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
    }

    private sealed record PagedCashRegisters(IReadOnlyList<CashRegisterSummary> Items, int Total, int Pagina, int TamanoPagina);
    private sealed record ApiError(string Mensaje);
}

public sealed record CashRegisterSummary(int Id, string Nombre, string Codigo, string? PcIdentificador, bool Activa, OpenRegisterSession? SesionAbierta);
public sealed record OpenRegisterSession(int Id, DateTime FechaApertura, decimal SaldoInicial, int UsuarioAperturaId);
public sealed record OpenRegisterResult(string Mensaje, int Id, int CajaId, DateTime FechaApertura, decimal SaldoInicial);
public sealed record CloseRegisterResult(string Mensaje, int Id, decimal? EfectivoEsperado, decimal? EfectivoDeclarado, decimal? Diferencia);
public sealed record ReconciliationPayment(int FormaPagoId, string FormaPago, bool EsEfectivo, decimal Total);
public sealed record CashMovement(int Id, string Tipo, decimal Importe, string Motivo, DateTime Fecha, int UsuarioId, string? Usuario, bool Anulado, DateTime? FechaAnulacion, string? MotivoAnulacion, string? UsuarioAnulacion);
public sealed record CashReconciliation(int SesionId, DateTime FechaApertura, decimal SaldoInicial, int CantidadVentas, decimal TotalVendido, decimal VentasEfectivo, decimal IngresosEfectivo, decimal RetirosEfectivo, decimal EfectivoEsperado, IReadOnlyList<ReconciliationPayment> FormasPago, IReadOnlyList<CashMovement> Movimientos);
public sealed record CashShiftFilters(int Page = 1, int PageSize = 10, int? CashRegisterId = null, string? User = null, string? Status = null, DateTime? From = null, DateTime? To = null);
public sealed record CashShiftPage(IReadOnlyList<CashShift> Items, int Total, int Pagina, int TamanoPagina, int TotalPaginas);
public sealed record CashShift(int Id, int CajaId, string Caja, string CodigoCaja, string Estado, int UsuarioAperturaId, string UsuarioApertura, int? UsuarioCierreId, string? UsuarioCierre, DateTime FechaApertura, DateTime? FechaCierre, int? DuracionMinutos, decimal SaldoInicial, int CantidadVentas, decimal TotalVendido, decimal? EfectivoEsperado, decimal? EfectivoDeclarado, decimal? Diferencia, string? Observaciones, decimal IngresosEfectivo, decimal RetirosEfectivo, IReadOnlyList<ReconciliationPayment> FormasPago, IReadOnlyList<CashMovement> Movimientos);
