using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VerduleriaAugust.Api.DTOs;
using VerduleriaAugust.Api.Models;

namespace VerduleriaAugust.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class VentasController : ControllerBase
{
    private readonly AugustDbContext _context;

    public VentasController(AugustDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetVentas()
    {
        var ventas = await _context.Ventas
            .Include(v => v.Caja)
            .OrderByDescending(v => v.FechaVenta)
            .Take(200)
            .Select(v => new
            {
                v.Id,
                v.NumeroVenta,
                v.CajaId,
                Caja = v.Caja != null ? v.Caja.Nombre : null,
                v.FechaVenta,
                v.Subtotal,
                v.Descuento,
                v.Total,
                v.Estado
            })
            .ToListAsync();

        return Ok(ventas);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetVenta(int id)
    {
        var venta = await _context.Ventas
            .Include(v => v.Caja)
            .Include(v => v.Detalles)
                .ThenInclude(d => d.Producto)
            .Include(v => v.Pagos)
                .ThenInclude(p => p.FormaPago)
            .Where(v => v.Id == id)
            .Select(v => new
            {
                v.Id,
                v.NumeroVenta,
                v.CajaId,
                Caja = v.Caja != null ? v.Caja.Nombre : null,
                v.FechaVenta,
                v.Subtotal,
                v.Descuento,
                v.Total,
                v.Estado,
                Detalles = v.Detalles.Select(d => new
                {
                    d.Id,
                    d.ProductoId,
                    Producto = d.Producto != null ? d.Producto.Nombre : null,
                    d.TipoVenta,
                    d.Cantidad,
                    d.PrecioUnitario,
                    d.TotalLinea
                }),
                Pagos = v.Pagos.Select(p => new
                {
                    p.Id,
                    p.FormaPagoId,
                    FormaPago = p.FormaPago != null ? p.FormaPago.Nombre : null,
                    p.Importe
                })
            })
            .FirstOrDefaultAsync();

        if (venta == null)
            return NotFound(new { mensaje = "Venta no encontrada." });

        return Ok(venta);
    }

    [HttpPost]
    public async Task<IActionResult> CrearVenta([FromBody] CrearVentaDto dto)
    {
        if (dto.Items == null || dto.Items.Count == 0)
            return BadRequest(new { mensaje = "La venta debe tener al menos un ítem." });

        if (dto.Pagos == null || dto.Pagos.Count == 0)
            return BadRequest(new { mensaje = "La venta debe tener al menos un pago." });

        var cajaExiste = await _context.Cajas.AnyAsync(c => c.Id == dto.CajaId && c.Activa);

        if (!cajaExiste)
            return BadRequest(new { mensaje = "La caja indicada no existe o no está activa." });

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var numeroVenta = await GenerarNumeroVentaAsync();

            var venta = new Venta
            {
                NumeroVenta = numeroVenta,
                CajaId = dto.CajaId,
                FechaVenta = DateTime.Now,
                Subtotal = 0,
                Descuento = dto.Descuento,
                Total = 0,
                Estado = "Finalizada"
            };

            _context.Ventas.Add(venta);
            await _context.SaveChangesAsync();

            decimal subtotal = 0;

            foreach (var item in dto.Items)
            {
                if (item.Cantidad <= 0)
                    return BadRequest(new { mensaje = "La cantidad de cada ítem debe ser mayor a cero." });

                if (item.TipoVenta != "Peso" && item.TipoVenta != "Unidad")
                    return BadRequest(new { mensaje = "TipoVenta debe ser Peso o Unidad." });

                var producto = await _context.Productos
                    .Include(p => p.Stock)
                    .FirstOrDefaultAsync(p => p.Id == item.ProductoId && p.Activo);

                if (producto == null)
                    return BadRequest(new { mensaje = $"El producto {item.ProductoId} no existe o está inactivo." });

                if (producto.Stock == null)
                    return BadRequest(new { mensaje = $"El producto {producto.Nombre} no tiene stock configurado." });

                if (producto.TipoVenta != item.TipoVenta && producto.TipoVenta != "Ambos")
                    return BadRequest(new { mensaje = $"El producto {producto.Nombre} no permite venta por {item.TipoVenta}." });

                decimal precioUnitario;

                if (item.TipoVenta == "Peso")
                {
                    if (!producto.PrecioPorKilo.HasValue || producto.PrecioPorKilo <= 0)
                        return BadRequest(new { mensaje = $"El producto {producto.Nombre} no tiene precio por kilo válido." });

                    precioUnitario = producto.PrecioPorKilo.Value;
                }
                else
                {
                    if (!producto.PrecioPorUnidad.HasValue || producto.PrecioPorUnidad <= 0)
                        return BadRequest(new { mensaje = $"El producto {producto.Nombre} no tiene precio por unidad válido." });

                    precioUnitario = producto.PrecioPorUnidad.Value;
                }

                if (producto.Stock.StockActual < item.Cantidad)
                    return BadRequest(new { mensaje = $"Stock insuficiente para {producto.Nombre}." });

                var totalLinea = Math.Round(item.Cantidad * precioUnitario, 2);
                subtotal += totalLinea;

                var detalle = new VentaDetalle
                {
                    VentaId = venta.Id,
                    ProductoId = producto.Id,
                    TipoVenta = item.TipoVenta,
                    Cantidad = item.Cantidad,
                    PrecioUnitario = precioUnitario,
                    TotalLinea = totalLinea
                };

                _context.VentaDetalles.Add(detalle);

                var stockAnterior = producto.Stock.StockActual;
                var stockNuevo = stockAnterior - item.Cantidad;

                producto.Stock.StockActual = stockNuevo;
                producto.Stock.FechaActualizacion = DateTime.Now;

                var movimiento = new MovimientoStock
                {
                    ProductoId = producto.Id,
                    TipoMovimiento = "Salida",
                    Cantidad = item.Cantidad,
                    StockAnterior = stockAnterior,
                    StockNuevo = stockNuevo,
                    Motivo = $"Venta {numeroVenta}",
                    FechaMovimiento = DateTime.Now
                };

                _context.MovimientosStock.Add(movimiento);
            }

            var total = subtotal - dto.Descuento;

            if (total < 0)
                return BadRequest(new { mensaje = "El descuento no puede ser mayor al subtotal." });

            var totalPagos = dto.Pagos.Sum(p => p.Importe);

            if (totalPagos != total)
                return BadRequest(new { mensaje = $"El total de pagos ({totalPagos}) debe coincidir con el total de la venta ({total})." });

            foreach (var pagoDto in dto.Pagos)
            {
                if (pagoDto.Importe <= 0)
                    return BadRequest(new { mensaje = "El importe de cada pago debe ser mayor a cero." });

                var formaPagoExiste = await _context.FormasPago
                    .AnyAsync(f => f.Id == pagoDto.FormaPagoId && f.Activa);

                if (!formaPagoExiste)
                    return BadRequest(new { mensaje = $"La forma de pago {pagoDto.FormaPagoId} no existe o no está activa." });

                var pago = new PagoVenta
                {
                    VentaId = venta.Id,
                    FormaPagoId = pagoDto.FormaPagoId,
                    Importe = pagoDto.Importe
                };

                _context.PagosVenta.Add(pago);
            }

            venta.Subtotal = subtotal;
            venta.Total = total;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return CreatedAtAction(nameof(GetVenta), new { id = venta.Id }, new
            {
                mensaje = "Venta registrada correctamente.",
                venta.Id,
                venta.NumeroVenta,
                venta.Subtotal,
                venta.Descuento,
                venta.Total
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task<string> GenerarNumeroVentaAsync()
    {
        var fecha = DateTime.Now.ToString("yyyyMMdd");

        var cantidadVentasHoy = await _context.Ventas
            .CountAsync(v => v.FechaVenta.Date == DateTime.Today);

        var correlativo = cantidadVentasHoy + 1;

        return $"V-{fecha}-{correlativo:000000}";
    }
}