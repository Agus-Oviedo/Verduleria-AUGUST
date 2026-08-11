using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VerduleriaAugust.Api.Models;
using VerduleriaAugust.Api.Security;
using VerduleriaAugust.Api.DTOs;

namespace VerduleriaAugust.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = Roles.Administracion)]
public class EstadisticasController : ControllerBase
{
    private readonly AugustDbContext _context;

    public EstadisticasController(AugustDbContext context)
    {
        _context = context;
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(EstadisticasDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard([FromQuery] EstadisticasDashboardConsultaDto consulta)
    {
        var hasta = (consulta.Hasta ?? DateTime.Today).Date;
        var desde = (consulta.Desde ?? hasta.AddDays(-29)).Date;
        if (desde > hasta)
            return BadRequest(new { mensaje = "La fecha desde no puede ser posterior a la fecha hasta." });
        if ((hasta - desde).TotalDays > 366)
            return BadRequest(new { mensaje = "El período no puede superar 366 días." });
        var hastaExclusivo = hasta.AddDays(1);

        var ventasPeriodo = _context.Ventas.AsNoTracking()
            .Where(venta => venta.FechaVenta >= desde && venta.FechaVenta < hastaExclusivo);
        if (consulta.CajaId.HasValue)
            ventasPeriodo = ventasPeriodo.Where(venta => venta.CajaId == consulta.CajaId.Value);
        var ventasFinalizadas = ventasPeriodo.Where(venta => venta.Estado == "Finalizada");

        var cantidadVentas = await ventasFinalizadas.CountAsync();
        var facturacion = await ventasFinalizadas.SumAsync(venta => (decimal?)venta.Total) ?? 0m;
        var anuladas = await ventasPeriodo.CountAsync(venta => venta.Estado == "Anulada");
        var totalOperaciones = cantidadVentas + anuladas;
        var ticketPromedio = cantidadVentas == 0 ? 0m : Math.Round(facturacion / cantidadVentas, 2);
        var porcentajeAnulacion = totalOperaciones == 0
            ? 0m
            : Math.Round(anuladas * 100m / totalOperaciones, 2);

        var cantidadDias = (hasta - desde).Days + 1;
        var hastaAnterior = desde.AddDays(-1);
        var desdeAnterior = hastaAnterior.AddDays(-cantidadDias + 1);
        var hastaAnteriorExclusivo = hastaAnterior.AddDays(1);
        var ventasAnteriores = _context.Ventas.AsNoTracking()
            .Where(venta => venta.Estado == "Finalizada" &&
                            venta.FechaVenta >= desdeAnterior &&
                            venta.FechaVenta < hastaAnteriorExclusivo);
        if (consulta.CajaId.HasValue)
            ventasAnteriores = ventasAnteriores.Where(venta => venta.CajaId == consulta.CajaId.Value);
        var cantidadVentasAnterior = await ventasAnteriores.CountAsync();
        var facturacionAnterior = await ventasAnteriores.SumAsync(venta => (decimal?)venta.Total) ?? 0m;
        var ticketAnterior = cantidadVentasAnterior == 0
            ? 0m
            : Math.Round(facturacionAnterior / cantidadVentasAnterior, 2);

        var detallesPeriodo = _context.VentaDetalles.AsNoTracking()
            .Where(detalle => detalle.Venta != null &&
                              detalle.Venta.Estado == "Finalizada" &&
                              detalle.Venta.FechaVenta >= desde &&
                              detalle.Venta.FechaVenta < hastaExclusivo);
        if (consulta.CajaId.HasValue)
            detallesPeriodo = detallesPeriodo.Where(detalle => detalle.Venta!.CajaId == consulta.CajaId.Value);
        var kilosVendidos = await detallesPeriodo
            .Where(detalle => detalle.TipoVenta == "Peso")
            .SumAsync(detalle => (decimal?)detalle.Cantidad) ?? 0m;
        var unidadesVendidas = await detallesPeriodo
            .Where(detalle => detalle.TipoVenta == "Unidad")
            .SumAsync(detalle => (decimal?)detalle.Cantidad) ?? 0m;

        var arqueos = _context.SesionesCaja.AsNoTracking()
            .Where(session => session.Estado == "Cerrada" &&
                              session.FechaCierre >= desde && session.FechaCierre < hastaExclusivo);
        if (consulta.CajaId.HasValue)
            arqueos = arqueos.Where(session => session.CajaId == consulta.CajaId.Value);
        var diferenciaArqueos = await arqueos.SumAsync(session => session.Diferencia) ?? 0m;

        var ventasPorCajaData = await ventasFinalizadas
            .GroupBy(venta => new { venta.CajaId, venta.Caja!.Nombre, venta.Caja.Codigo })
            .Select(group => new
            {
                group.Key.CajaId, group.Key.Nombre, group.Key.Codigo,
                Cantidad = group.Count(),
                Total = group.Sum(venta => venta.Total)
            })
            .OrderByDescending(item => item.Total)
            .ToListAsync();
        var ventasPorCaja = ventasPorCajaData.Select(item => new VentaPorCajaDto(
            item.CajaId, item.Nombre, item.Codigo, item.Cantidad, item.Total,
            item.Cantidad == 0 ? 0 : Math.Round(item.Total / item.Cantidad, 2))).ToList();

        var ventasPorDiaData = await ventasFinalizadas
            .GroupBy(venta => venta.FechaVenta.Date)
            .Select(group => new
            {
                Fecha = group.Key,
                Cantidad = group.Count(),
                Total = group.Sum(venta => venta.Total)
            })
            .OrderBy(item => item.Fecha)
            .ToListAsync();
        var ventasPorDia = ventasPorDiaData.Select(item => new VentaPorDiaDto(
            item.Fecha, item.Cantidad, item.Total)).ToList();

        var pagosPeriodo = _context.PagosVenta.AsNoTracking()
            .Where(pago => pago.Venta != null && pago.Venta.Estado == "Finalizada" &&
                           pago.Venta.FechaVenta >= desde && pago.Venta.FechaVenta < hastaExclusivo);
        if (consulta.CajaId.HasValue)
            pagosPeriodo = pagosPeriodo.Where(pago => pago.Venta!.CajaId == consulta.CajaId.Value);
        var formasPagoData = await pagosPeriodo
            .GroupBy(pago => new { pago.FormaPagoId, pago.FormaPago!.Nombre })
            .Select(group => new
            {
                group.Key.FormaPagoId, group.Key.Nombre,
                Cantidad = group.Count(),
                Total = group.Sum(pago => pago.Importe)
            })
            .OrderByDescending(item => item.Total)
            .ToListAsync();
        var formasPago = formasPagoData.Select(item => new EstadisticaFormaPagoDto(
            item.FormaPagoId, item.Nombre, item.Cantidad, item.Total)).ToList();

        var productosData = await detallesPeriodo
            .GroupBy(detalle => new { detalle.ProductoId, detalle.Producto!.Nombre, detalle.Producto.Codigo })
            .Select(group => new
            {
                group.Key.ProductoId, group.Key.Nombre, group.Key.Codigo,
                Cantidad = group.Sum(detalle => detalle.Cantidad),
                Total = group.Sum(detalle => detalle.TotalLinea)
            })
            .OrderByDescending(item => item.Total)
            .Take(10)
            .ToListAsync();
        var productos = productosData.Select(item => new ProductoEstadisticaDto(
            item.ProductoId, item.Nombre, item.Codigo, item.Cantidad, item.Total)).ToList();

        var categoriasData = await detallesPeriodo
            .GroupBy(detalle => new
            {
                CategoriaId = detalle.Producto!.CategoriaId,
                Categoria = detalle.Producto.Categoria!.Nombre
            })
            .Select(group => new
            {
                group.Key.CategoriaId,
                group.Key.Categoria,
                Cantidad = group.Sum(detalle => detalle.Cantidad),
                Total = group.Sum(detalle => detalle.TotalLinea)
            })
            .OrderByDescending(item => item.Total)
            .ToListAsync();
        var categorias = categoriasData.Select(item => new VentaPorCategoriaDto(
            item.CategoriaId, item.Categoria, item.Cantidad, item.Total)).ToList();

        var horasData = await ventasFinalizadas
            .GroupBy(venta => venta.FechaVenta.Hour)
            .Select(group => new
            {
                Hora = group.Key,
                Cantidad = group.Count(),
                Total = group.Sum(venta => venta.Total)
            })
            .OrderBy(item => item.Hora)
            .ToListAsync();
        var horas = horasData.Select(item => new VentaPorHoraDto(
            item.Hora, item.Cantidad, item.Total,
            item.Cantidad == 0 ? 0m : Math.Round(item.Total / item.Cantidad, 2))).ToList();

        var productosSinVentas = await _context.Productos.AsNoTracking()
            .CountAsync(producto => producto.Activo &&
                !_context.VentaDetalles.Any(detalle => detalle.ProductoId == producto.Id &&
                    detalle.Venta != null &&
                    detalle.Venta.Estado == "Finalizada" &&
                    detalle.Venta.FechaVenta >= desde &&
                    detalle.Venta.FechaVenta < hastaExclusivo &&
                    (!consulta.CajaId.HasValue || detalle.Venta.CajaId == consulta.CajaId.Value)));
        var productosBajoStock = await _context.Stock.AsNoTracking()
            .CountAsync(stock => stock.StockMinimo.HasValue &&
                                 stock.StockActual <= stock.StockMinimo.Value);
        var importeAnulado = await ventasPeriodo
            .Where(venta => venta.Estado == "Anulada")
            .SumAsync(venta => (decimal?)venta.Total) ?? 0m;
        var arqueosConDiferencia = await arqueos.CountAsync(session => session.Diferencia != 0);

        return Ok(new EstadisticasDashboardDto(
            new ResumenPeriodoDto(
                desde, hasta, cantidadVentas, Math.Round(facturacion, 2), ticketPromedio,
                kilosVendidos, unidadesVendidas, anuladas, porcentajeAnulacion,
                Math.Round(diferenciaArqueos, 2)),
            new ComparacionPeriodoDto(
                desdeAnterior, hastaAnterior, Math.Round(facturacionAnterior, 2),
                cantidadVentasAnterior, ticketAnterior,
                CalcularVariacion(facturacion, facturacionAnterior),
                CalcularVariacion(cantidadVentas, cantidadVentasAnterior),
                CalcularVariacion(ticketPromedio, ticketAnterior)),
            ventasPorCaja, ventasPorDia, formasPago, productos,
            categorias, horas,
            new AlertasEstadisticasDto(
                productosSinVentas, productosBajoStock, anuladas,
                Math.Round(importeAnulado, 2), arqueosConDiferencia)));
    }

    private static decimal CalcularVariacion(decimal actual, decimal anterior)
    {
        if (anterior == 0) return actual == 0 ? 0 : 100;
        return Math.Round((actual - anterior) / anterior * 100, 2);
    }

    [HttpGet("resumen-hoy")]
    public async Task<IActionResult> GetResumenHoy()
    {
        var hoy = DateTime.Today;
        var mañana = hoy.AddDays(1);

        var ventasHoy = await _context.Ventas
            .Where(v => v.Estado == "Finalizada"
                        && v.FechaVenta >= hoy
                        && v.FechaVenta < mañana)
            .ToListAsync();

        var cantidadVentas = ventasHoy.Count;
        var facturacionTotal = ventasHoy.Sum(v => v.Total);
        var ticketPromedio = cantidadVentas > 0
            ? Math.Round(facturacionTotal / cantidadVentas, 2)
            : 0;

        var cantidadItemsVendidos = await _context.VentaDetalles
            .Where(d => d.Venta != null
                        && d.Venta.Estado == "Finalizada"
                        && d.Venta.FechaVenta >= hoy
                        && d.Venta.FechaVenta < mañana)
            .SumAsync(d => d.Cantidad);

        var kilosVendidos = await _context.VentaDetalles
            .Where(d => d.Venta != null
                        && d.Venta.Estado == "Finalizada"
                        && d.Venta.FechaVenta >= hoy
                        && d.Venta.FechaVenta < mañana
                        && d.TipoVenta == "Peso")
            .SumAsync(d => d.Cantidad);

        return Ok(new
        {
            Fecha = hoy,
            CantidadVentas = cantidadVentas,
            FacturacionTotal = facturacionTotal,
            TicketPromedio = ticketPromedio,
            CantidadItemsVendidos = cantidadItemsVendidos,
            KilosVendidos = kilosVendidos
        });
    }

    [HttpGet("resumen-mes")]
    public async Task<IActionResult> GetResumenMes()
    {
        var hoy = DateTime.Today;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);
        var inicioMesSiguiente = inicioMes.AddMonths(1);

        var ventasMes = await _context.Ventas
            .Where(v => v.Estado == "Finalizada"
                        && v.FechaVenta >= inicioMes
                        && v.FechaVenta < inicioMesSiguiente)
            .ToListAsync();

        var cantidadVentas = ventasMes.Count;
        var facturacionTotal = ventasMes.Sum(v => v.Total);
        var ticketPromedio = cantidadVentas > 0
            ? Math.Round(facturacionTotal / cantidadVentas, 2)
            : 0;

        return Ok(new
        {
            Mes = inicioMes.Month,
            Anio = inicioMes.Year,
            CantidadVentas = cantidadVentas,
            FacturacionTotal = facturacionTotal,
            TicketPromedio = ticketPromedio
        });
    }

    [HttpGet("ventas-por-dia")]
    public async Task<IActionResult> GetVentasPorDia([FromQuery] int dias = 30)
    {
        if (dias <= 0)
            return BadRequest(new { mensaje = "La cantidad de días debe ser mayor a cero." });

        var desde = DateTime.Today.AddDays(-dias + 1);
        var hasta = DateTime.Today.AddDays(1);

        var ventasPorDia = await _context.Ventas
            .Where(v => v.Estado == "Finalizada"
                        && v.FechaVenta >= desde
                        && v.FechaVenta < hasta)
            .GroupBy(v => v.FechaVenta.Date)
            .Select(g => new
            {
                Fecha = g.Key,
                CantidadVentas = g.Count(),
                FacturacionTotal = g.Sum(v => v.Total),
                TicketPromedio = g.Count() > 0
                    ? Math.Round(g.Sum(v => v.Total) / g.Count(), 2)
                    : 0
            })
            .OrderBy(x => x.Fecha)
            .ToListAsync();

        return Ok(ventasPorDia);
    }

    [HttpGet("productos-mas-vendidos")]
    public async Task<IActionResult> GetProductosMasVendidos([FromQuery] int top = 10)
    {
        if (top <= 0)
            return BadRequest(new { mensaje = "El valor de top debe ser mayor a cero." });

        var productos = await _context.VentaDetalles
            .Where(d => d.Venta != null && d.Venta.Estado == "Finalizada")
            .GroupBy(d => new
            {
                d.ProductoId,
                Producto = d.Producto != null ? d.Producto.Nombre : "Sin nombre",
                Codigo = d.Producto != null ? d.Producto.Codigo : "",
                d.TipoVenta
            })
            .Select(g => new
            {
                g.Key.ProductoId,
                g.Key.Producto,
                g.Key.Codigo,
                g.Key.TipoVenta,
                CantidadVendida = g.Sum(x => x.Cantidad),
                FacturacionTotal = g.Sum(x => x.TotalLinea)
            })
            .OrderByDescending(x => x.FacturacionTotal)
            .Take(top)
            .ToListAsync();

        return Ok(productos);
    }

    [HttpGet("productos-menos-vendidos")]
    public async Task<IActionResult> GetProductosMenosVendidos([FromQuery] int top = 10)
    {
        if (top <= 0)
            return BadRequest(new { mensaje = "El valor de top debe ser mayor a cero." });

        var productos = await _context.VentaDetalles
            .Where(d => d.Venta != null && d.Venta.Estado == "Finalizada")
            .GroupBy(d => new
            {
                d.ProductoId,
                Producto = d.Producto != null ? d.Producto.Nombre : "Sin nombre",
                Codigo = d.Producto != null ? d.Producto.Codigo : "",
                d.TipoVenta
            })
            .Select(g => new
            {
                g.Key.ProductoId,
                g.Key.Producto,
                g.Key.Codigo,
                g.Key.TipoVenta,
                CantidadVendida = g.Sum(x => x.Cantidad),
                FacturacionTotal = g.Sum(x => x.TotalLinea)
            })
            .OrderBy(x => x.CantidadVendida)
            .Take(top)
            .ToListAsync();

        return Ok(productos);
    }

    [HttpGet("facturacion-por-caja")]
    public async Task<IActionResult> GetFacturacionPorCaja()
    {
        var resultado = await _context.Ventas
            .Where(v => v.Estado == "Finalizada")
            .GroupBy(v => new
            {
                v.CajaId,
                Caja = v.Caja != null ? v.Caja.Nombre : "Sin caja",
                CodigoCaja = v.Caja != null ? v.Caja.Codigo : ""
            })
            .Select(g => new
            {
                g.Key.CajaId,
                g.Key.Caja,
                g.Key.CodigoCaja,
                CantidadVentas = g.Count(),
                FacturacionTotal = g.Sum(v => v.Total),
                TicketPromedio = g.Count() > 0
                    ? Math.Round(g.Sum(v => v.Total) / g.Count(), 2)
                    : 0
            })
            .OrderByDescending(x => x.FacturacionTotal)
            .ToListAsync();

        return Ok(resultado);
    }

    [HttpGet("kilos-vendidos-por-producto")]
    public async Task<IActionResult> GetKilosVendidosPorProducto()
    {
        var resultado = await _context.VentaDetalles
            .Where(d => d.Venta != null
                        && d.Venta.Estado == "Finalizada"
                        && d.TipoVenta == "Peso")
            .GroupBy(d => new
            {
                d.ProductoId,
                Producto = d.Producto != null ? d.Producto.Nombre : "Sin nombre",
                Codigo = d.Producto != null ? d.Producto.Codigo : ""
            })
            .Select(g => new
            {
                g.Key.ProductoId,
                g.Key.Producto,
                g.Key.Codigo,
                KilosVendidos = g.Sum(x => x.Cantidad),
                FacturacionTotal = g.Sum(x => x.TotalLinea)
            })
            .OrderByDescending(x => x.KilosVendidos)
            .ToListAsync();

        return Ok(resultado);
    }

    [HttpGet("facturacion-por-producto")]
    public async Task<IActionResult> GetFacturacionPorProducto()
    {
        var resultado = await _context.VentaDetalles
            .Where(d => d.Venta != null && d.Venta.Estado == "Finalizada")
            .GroupBy(d => new
            {
                d.ProductoId,
                Producto = d.Producto != null ? d.Producto.Nombre : "Sin nombre",
                Codigo = d.Producto != null ? d.Producto.Codigo : ""
            })
            .Select(g => new
            {
                g.Key.ProductoId,
                g.Key.Producto,
                g.Key.Codigo,
                CantidadVendida = g.Sum(x => x.Cantidad),
                FacturacionTotal = g.Sum(x => x.TotalLinea)
            })
            .OrderByDescending(x => x.FacturacionTotal)
            .ToListAsync();

        return Ok(resultado);
    }

    [HttpGet("bajo-stock")]
    public async Task<IActionResult> GetProductosBajoStock()
    {
        var resultado = await _context.Stock
            .Include(s => s.Producto)
            .ThenInclude(p => p!.Categoria)
            .Where(s => s.StockMinimo.HasValue && s.StockActual <= s.StockMinimo.Value)
            .OrderBy(s => s.StockActual)
            .Select(s => new
            {
                s.ProductoId,
                Producto = s.Producto != null ? s.Producto.Nombre : "Sin nombre",
                Codigo = s.Producto != null ? s.Producto.Codigo : "",
                Categoria = s.Producto != null && s.Producto.Categoria != null
                    ? s.Producto.Categoria.Nombre
                    : "",
                s.StockActual,
                s.StockMinimo,
                s.UnidadMedida
            })
            .ToListAsync();

        return Ok(resultado);
    }

    [HttpGet("ventas-por-forma-pago")]
    public async Task<IActionResult> GetVentasPorFormaPago()
    {
        var resultado = await _context.PagosVenta
            .Where(p => p.Venta != null && p.Venta.Estado == "Finalizada")
            .GroupBy(p => new
            {
                p.FormaPagoId,
                FormaPago = p.FormaPago != null ? p.FormaPago.Nombre : "Sin forma de pago"
            })
            .Select(g => new
            {
                g.Key.FormaPagoId,
                g.Key.FormaPago,
                CantidadPagos = g.Count(),
                TotalCobrado = g.Sum(p => p.Importe)
            })
            .OrderByDescending(x => x.TotalCobrado)
            .ToListAsync();

        return Ok(resultado);
    }
}
