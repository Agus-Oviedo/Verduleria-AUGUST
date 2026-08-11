using System.ComponentModel.DataAnnotations;

namespace VerduleriaAugust.Api.DTOs;

public sealed class EstadisticasDashboardConsultaDto
{
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }

    [Range(1, int.MaxValue)]
    public int? CajaId { get; set; }
}

public sealed record ResumenPeriodoDto(
    DateTime Desde,
    DateTime Hasta,
    int CantidadVentas,
    decimal FacturacionTotal,
    decimal TicketPromedio,
    decimal KilosVendidos,
    decimal UnidadesVendidas,
    int VentasAnuladas,
    decimal PorcentajeAnulacion,
    decimal DiferenciaArqueos);

public sealed record VentaPorCajaDto(
    int CajaId,
    string Caja,
    string Codigo,
    int CantidadVentas,
    decimal FacturacionTotal,
    decimal TicketPromedio);

public sealed record VentaPorDiaDto(
    DateTime Fecha,
    int CantidadVentas,
    decimal FacturacionTotal);

public sealed record EstadisticaFormaPagoDto(
    int FormaPagoId,
    string FormaPago,
    int CantidadPagos,
    decimal TotalCobrado);

public sealed record ProductoEstadisticaDto(
    int ProductoId,
    string Producto,
    string Codigo,
    decimal CantidadVendida,
    decimal FacturacionTotal);

public sealed record ComparacionPeriodoDto(
    DateTime DesdeAnterior,
    DateTime HastaAnterior,
    decimal FacturacionAnterior,
    int VentasAnteriores,
    decimal TicketPromedioAnterior,
    decimal VariacionFacturacion,
    decimal VariacionVentas,
    decimal VariacionTicketPromedio);

public sealed record VentaPorCategoriaDto(
    int CategoriaId,
    string Categoria,
    decimal CantidadVendida,
    decimal FacturacionTotal);

public sealed record VentaPorHoraDto(
    int Hora,
    int CantidadVentas,
    decimal FacturacionTotal,
    decimal TicketPromedio);

public sealed record AlertasEstadisticasDto(
    int ProductosSinVentas,
    int ProductosBajoStock,
    int VentasAnuladas,
    decimal ImporteAnulado,
    int ArqueosConDiferencia);

public sealed record EstadisticasDashboardDto(
    ResumenPeriodoDto Resumen,
    ComparacionPeriodoDto Comparacion,
    IReadOnlyList<VentaPorCajaDto> VentasPorCaja,
    IReadOnlyList<VentaPorDiaDto> VentasPorDia,
    IReadOnlyList<EstadisticaFormaPagoDto> FormasPago,
    IReadOnlyList<ProductoEstadisticaDto> ProductosMasVendidos,
    IReadOnlyList<VentaPorCategoriaDto> VentasPorCategoria,
    IReadOnlyList<VentaPorHoraDto> VentasPorHora,
    AlertasEstadisticasDto Alertas);
