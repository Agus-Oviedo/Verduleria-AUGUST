namespace VerduleriaAugust.Api.DTOs;

public sealed record StockResumenDto(
    decimal StockActual,
    string UnidadMedida,
    decimal? StockMinimo,
    DateTime FechaActualizacion);

public sealed record ProductoListadoDto(
    int Id,
    string Codigo,
    string Nombre,
    int CategoriaId,
    string? Categoria,
    string TipoVenta,
    decimal? PrecioPorKilo,
    decimal? PrecioPorUnidad,
    bool Activo,
    StockResumenDto? Stock);

public sealed record VentaListadoDto(
    int Id,
    string NumeroVenta,
    string? IdempotencyKey,
    int CajaId,
    string? Caja,
    DateTime FechaVenta,
    decimal Subtotal,
    decimal Descuento,
    decimal Total,
    string Estado,
    string? FormasPago);

public sealed record StockListadoDto(
    int Id,
    int ProductoId,
    string Producto,
    string Codigo,
    string? Categoria,
    decimal StockActual,
    string UnidadMedida,
    decimal? StockMinimo,
    DateTime FechaActualizacion,
    bool BajoStock);

public sealed record MovimientoStockListadoDto(
    int Id,
    int ProductoId,
    string Producto,
    string Codigo,
    string TipoMovimiento,
    decimal Cantidad,
    decimal StockAnterior,
    decimal StockNuevo,
    string? Motivo,
    string? Usuario,
    DateTime FechaMovimiento);

public sealed record SesionCajaResumenDto(
    int Id,
    DateTime FechaApertura,
    decimal SaldoInicial,
    int UsuarioAperturaId);

public sealed record CajaListadoDto(
    int Id,
    string Nombre,
    string Codigo,
    string? PcIdentificador,
    bool Activa,
    SesionCajaResumenDto? SesionAbierta);

public sealed record HistorialPrecioListadoDto(
    int Id,
    decimal? PrecioPorKiloAnterior,
    decimal? PrecioPorKiloNuevo,
    decimal? PrecioPorUnidadAnterior,
    decimal? PrecioPorUnidadNuevo,
    string Motivo,
    DateTime FechaCambio,
    string? Usuario);

public sealed record CategoriaListadoDto(
    int Id,
    string Nombre,
    bool Activa,
    DateTime FechaCreacion);

public sealed record FormaPagoListadoDto(
    int Id,
    string Nombre,
    bool EsEfectivo,
    bool Activa,
    int Orden,
    int CantidadUsos);

public sealed record VentaDetalleRespuestaDto(
    int Id,
    int ProductoId,
    string? Producto,
    string TipoVenta,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal TotalLinea);

public sealed record PagoVentaRespuestaDto(
    int Id,
    int FormaPagoId,
    string? FormaPago,
    decimal Importe);

public sealed record AnulacionVentaRespuestaDto(
    string Motivo,
    DateTime FechaAnulacion,
    string? Usuario);

public sealed record DescuentoVentaRespuestaDto(
    string Motivo,
    int UsuarioId,
    string? Usuario);

public sealed record VentaRespuestaDto(
    int Id,
    string NumeroVenta,
    string? IdempotencyKey,
    int CajaId,
    string? Caja,
    DateTime FechaVenta,
    decimal Subtotal,
    decimal Descuento,
    decimal Total,
    string Estado,
    IEnumerable<VentaDetalleRespuestaDto> Detalles,
    IEnumerable<PagoVentaRespuestaDto> Pagos,
    DescuentoVentaRespuestaDto? AuditoriaDescuento,
    AnulacionVentaRespuestaDto? Anulacion);

public sealed record StockProductoRespuestaDto(
    int Id,
    int ProductoId,
    string Producto,
    string Codigo,
    decimal StockActual,
    string UnidadMedida,
    decimal? StockMinimo,
    DateTime FechaActualizacion);

public sealed record CajaRespuestaDto(
    int Id,
    string Nombre,
    string Codigo,
    string? PcIdentificador,
    bool Activa);

public sealed record VentaCreadaRespuestaDto(
    string Mensaje,
    int Id,
    string NumeroVenta,
    decimal Subtotal,
    decimal Descuento,
    decimal Total,
    string? IdempotencyKey);

public sealed record VentaExistenteRespuestaDto(
    string Mensaje,
    bool Idempotente,
    int Id,
    string NumeroVenta,
    decimal Subtotal,
    decimal Descuento,
    decimal Total,
    string? IdempotencyKey);

public sealed record VentaAnuladaRespuestaDto(
    string Mensaje,
    int Id,
    string NumeroVenta,
    string Estado);

public sealed record CajaAbiertaRespuestaDto(
    string Mensaje,
    int Id,
    int CajaId,
    DateTime FechaApertura,
    decimal SaldoInicial);

public sealed record CajaCerradaRespuestaDto(
    string Mensaje,
    int Id,
    decimal? EfectivoEsperado,
    decimal? EfectivoDeclarado,
    decimal? Diferencia);

public sealed record ArqueoFormaPagoDto(
    int FormaPagoId,
    string FormaPago,
    bool EsEfectivo,
    decimal Total);

public sealed record MovimientoCajaRespuestaDto(int Id, string Tipo, decimal Importe, string Motivo, DateTime Fecha, int UsuarioId, string? Usuario, bool Anulado, DateTime? FechaAnulacion, string? MotivoAnulacion, string? UsuarioAnulacion);

public sealed record ArqueoCajaDto(
    int SesionId,
    DateTime FechaApertura,
    decimal SaldoInicial,
    int CantidadVentas,
    decimal TotalVendido,
    decimal VentasEfectivo,
    decimal IngresosEfectivo,
    decimal RetirosEfectivo,
    decimal EfectivoEsperado,
    IReadOnlyList<ArqueoFormaPagoDto> FormasPago,
    IReadOnlyList<MovimientoCajaRespuestaDto> Movimientos);

public sealed record TurnoCajaListadoDto(
    int Id,
    int CajaId,
    string Caja,
    string CodigoCaja,
    string Estado,
    int UsuarioAperturaId,
    string UsuarioApertura,
    int? UsuarioCierreId,
    string? UsuarioCierre,
    DateTime FechaApertura,
    DateTime? FechaCierre,
    int? DuracionMinutos,
    decimal SaldoInicial,
    int CantidadVentas,
    decimal TotalVendido,
    decimal? EfectivoEsperado,
    decimal? EfectivoDeclarado,
    decimal? Diferencia,
    string? Observaciones,
    decimal IngresosEfectivo,
    decimal RetirosEfectivo,
    IReadOnlyList<ArqueoFormaPagoDto> FormasPago,
    IReadOnlyList<MovimientoCajaRespuestaDto> Movimientos);

public sealed record AuditoriaUsuarioDto(
    int Id,
    int UsuarioObjetivoId,
    string UsuarioObjetivo,
    int? UsuarioActorId,
    string UsuarioActor,
    string Accion,
    string Detalle,
    DateTime Fecha);
