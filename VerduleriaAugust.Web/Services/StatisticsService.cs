using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace VerduleriaAugust.Web.Services;

public sealed class StatisticsService(HttpClient httpClient, IJSRuntime jsRuntime)
{
    public async Task<StatisticsDashboard> GetDashboardAsync(
        DateTime from,
        DateTime to,
        int? cashRegisterId = null,
        CancellationToken cancellationToken = default)
    {
        var uri = $"api/Estadisticas/dashboard?desde={Uri.EscapeDataString(from.Date.ToString("O"))}&hasta={Uri.EscapeDataString(to.Date.ToString("O"))}";
        if (cashRegisterId.HasValue) uri += $"&cajaId={cashRegisterId.Value}";
        var token = await jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", "august.auth.token");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException("La sesión venció. Volvé a iniciar sesión.");
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException("Tu rol no tiene permiso para ver estadísticas.");
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadFromJsonAsync<ApiError>(cancellationToken);
            throw new InvalidOperationException(error?.Mensaje ?? "No se pudieron calcular las estadísticas.");
        }
        return (await response.Content.ReadFromJsonAsync<StatisticsDashboard>(cancellationToken))
            ?? throw new InvalidOperationException("La API devolvió estadísticas incompletas.");
    }

    private sealed record ApiError(string Mensaje);
}

public sealed record PeriodSummary(DateTime Desde, DateTime Hasta, int CantidadVentas, decimal FacturacionTotal, decimal TicketPromedio, decimal KilosVendidos, decimal UnidadesVendidas, int VentasAnuladas, decimal PorcentajeAnulacion, decimal DiferenciaArqueos);
public sealed record CashRegisterStatistic(int CajaId, string Caja, string Codigo, int CantidadVentas, decimal FacturacionTotal, decimal TicketPromedio);
public sealed record DailySaleStatistic(DateTime Fecha, int CantidadVentas, decimal FacturacionTotal);
public sealed record PaymentStatistic(int FormaPagoId, string FormaPago, int CantidadPagos, decimal TotalCobrado);
public sealed record ProductStatistic(int ProductoId, string Producto, string Codigo, decimal CantidadVendida, decimal FacturacionTotal);
public sealed record PeriodComparison(DateTime DesdeAnterior, DateTime HastaAnterior, decimal FacturacionAnterior, int VentasAnteriores, decimal TicketPromedioAnterior, decimal VariacionFacturacion, decimal VariacionVentas, decimal VariacionTicketPromedio);
public sealed record CategoryStatistic(int CategoriaId, string Categoria, decimal CantidadVendida, decimal FacturacionTotal);
public sealed record HourStatistic(int Hora, int CantidadVentas, decimal FacturacionTotal, decimal TicketPromedio);
public sealed record StatisticsAlerts(int ProductosSinVentas, int ProductosBajoStock, int VentasAnuladas, decimal ImporteAnulado, int ArqueosConDiferencia);
public sealed record StatisticsDashboard(PeriodSummary Resumen, PeriodComparison Comparacion, IReadOnlyList<CashRegisterStatistic> VentasPorCaja, IReadOnlyList<DailySaleStatistic> VentasPorDia, IReadOnlyList<PaymentStatistic> FormasPago, IReadOnlyList<ProductStatistic> ProductosMasVendidos, IReadOnlyList<CategoryStatistic> VentasPorCategoria, IReadOnlyList<HourStatistic> VentasPorHora, StatisticsAlerts Alertas);
