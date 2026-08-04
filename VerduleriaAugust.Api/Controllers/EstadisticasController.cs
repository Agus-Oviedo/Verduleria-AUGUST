using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VerduleriaAugust.Api.Models;

namespace VerduleriaAugust.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class EstadisticasController : ControllerBase
{
    private readonly AugustDbContext _context;

    public EstadisticasController(AugustDbContext context)
    {
        _context = context;
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