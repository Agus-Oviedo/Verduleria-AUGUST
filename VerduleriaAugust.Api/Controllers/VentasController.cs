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
            .AsNoTracking()
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
            .AsNoTracking()
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
                    Producto = d.Producto != null
                        ? d.Producto.Nombre
                        : null,
                    d.TipoVenta,
                    d.Cantidad,
                    d.PrecioUnitario,
                    d.TotalLinea
                }),

                Pagos = v.Pagos.Select(p => new
                {
                    p.Id,
                    p.FormaPagoId,
                    FormaPago = p.FormaPago != null
                        ? p.FormaPago.Nombre
                        : null,
                    p.Importe
                })
            })
            .FirstOrDefaultAsync();

        if (venta == null)
        {
            return NotFound(new
            {
                mensaje = "Venta no encontrada."
            });
        }

        return Ok(venta);
    }

    [HttpPost]
    public async Task<IActionResult> CrearVenta([FromBody] CrearVentaDto dto)
    {
        var errorInicial = ValidarDatosIniciales(dto);

        if (errorInicial != null)
        {
            return BadRequest(new
            {
                mensaje = errorInicial
            });
        }

        var cajaExiste = await _context.Cajas
            .AsNoTracking()
            .AnyAsync(c => c.Id == dto.CajaId && c.Activa);

        if (!cajaExiste)
        {
            return BadRequest(new
            {
                mensaje = "La caja indicada no existe o no está activa."
            });
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync();

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

            /*
             * Guardamos primero para obtener Venta.Id.
             * Si después ocurre algún error, toda la operación se revierte
             * porque todavía estamos dentro de la transacción.
             */
            await _context.SaveChangesAsync();

            decimal subtotal = 0;

            foreach (var item in dto.Items)
            {
                var producto = await _context.Productos
                    .Include(p => p.Stock)
                    .FirstOrDefaultAsync(p =>
                        p.Id == item.ProductoId &&
                        p.Activo);

                if (producto == null)
                {
                    throw new VentaValidationException(
                        $"El producto {item.ProductoId} no existe o está inactivo.");
                }

                if (producto.Stock == null)
                {
                    throw new VentaValidationException(
                        $"El producto {producto.Nombre} no tiene stock configurado.");
                }

                if (producto.TipoVenta != item.TipoVenta &&
                    producto.TipoVenta != "Ambos")
                {
                    throw new VentaValidationException(
                        $"El producto {producto.Nombre} no permite venta por {item.TipoVenta}.");
                }

                var precioUnitario = ObtenerPrecioUnitario(
                    producto,
                    item.TipoVenta);

                if (producto.Stock.StockActual < item.Cantidad)
                {
                    throw new VentaValidationException(
                        $"Stock insuficiente para {producto.Nombre}. " +
                        $"Disponible: {producto.Stock.StockActual:0.###}. " +
                        $"Solicitado: {item.Cantidad:0.###}.");
                }

                var totalLinea = Math.Round(
                    item.Cantidad * precioUnitario,
                    2,
                    MidpointRounding.AwayFromZero);

                subtotal += totalLinea;

                var stockAnterior = producto.Stock.StockActual;
                var stockNuevo = stockAnterior - item.Cantidad;

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

                /*
                 * Stock tiene RowVersion.
                 * EF Core incluirá ese valor en el UPDATE.
                 * Si otra caja modificó el registro, SaveChangesAsync
                 * lanzará DbUpdateConcurrencyException.
                 */
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

            subtotal = Math.Round(
                subtotal,
                2,
                MidpointRounding.AwayFromZero);

            var total = subtotal - dto.Descuento;

            if (total < 0)
            {
                throw new VentaValidationException(
                    "El descuento no puede ser mayor al subtotal.");
            }

            total = Math.Round(
                total,
                2,
                MidpointRounding.AwayFromZero);

            var totalPagos = Math.Round(
                dto.Pagos.Sum(p => p.Importe),
                2,
                MidpointRounding.AwayFromZero);

            if (totalPagos != total)
            {
                throw new VentaValidationException(
                    $"El total de pagos ({totalPagos:0.00}) debe coincidir " +
                    $"con el total de la venta ({total:0.00}).");
            }

            foreach (var pagoDto in dto.Pagos)
            {
                var formaPagoExiste = await _context.FormasPago
                    .AsNoTracking()
                    .AnyAsync(f =>
                        f.Id == pagoDto.FormaPagoId &&
                        f.Activa);

                if (!formaPagoExiste)
                {
                    throw new VentaValidationException(
                        $"La forma de pago {pagoDto.FormaPagoId} " +
                        "no existe o no está activa.");
                }

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

            /*
             * En este SaveChanges se actualiza Stock.
             * Si RowVersion cambió, EF Core lanza
             * DbUpdateConcurrencyException.
             */
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return CreatedAtAction(
                nameof(GetVenta),
                new { id = venta.Id },
                new
                {
                    mensaje = "Venta registrada correctamente.",
                    venta.Id,
                    venta.NumeroVenta,
                    venta.Subtotal,
                    venta.Descuento,
                    venta.Total
                });
        }
        catch (VentaValidationException ex)
        {
            await transaction.RollbackAsync();

            return BadRequest(new
            {
                mensaje = ex.Message
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();

            /*
             * Quitamos las entidades cargadas del ChangeTracker.
             * Así evitamos reutilizar estados desactualizados en esta
             * instancia del DbContext.
             */
            _context.ChangeTracker.Clear();

            return Conflict(new
            {
                mensaje = "El stock fue modificado por otra caja mientras se procesaba la venta.",
                detalle = "Actualizá el stock y volvé a intentar la operación.",
                codigo = "STOCK_CONCURRENCIA"
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static string? ValidarDatosIniciales(CrearVentaDto dto)
    {
        if (dto.CajaId <= 0)
        {
            return "Debe indicar una caja válida.";
        }

        if (dto.Descuento < 0)
        {
            return "El descuento no puede ser negativo.";
        }

        if (dto.Items == null || dto.Items.Count == 0)
        {
            return "La venta debe tener al menos un ítem.";
        }

        if (dto.Pagos == null || dto.Pagos.Count == 0)
        {
            return "La venta debe tener al menos un pago.";
        }

        foreach (var item in dto.Items)
        {
            if (item.ProductoId <= 0)
            {
                return "Todos los ítems deben tener un producto válido.";
            }

            if (item.Cantidad <= 0)
            {
                return "La cantidad de cada ítem debe ser mayor a cero.";
            }

            if (item.TipoVenta != "Peso" &&
                item.TipoVenta != "Unidad")
            {
                return "TipoVenta debe ser Peso o Unidad.";
            }
        }

        foreach (var pago in dto.Pagos)
        {
            if (pago.FormaPagoId <= 0)
            {
                return "Todos los pagos deben tener una forma de pago válida.";
            }

            if (pago.Importe <= 0)
            {
                return "El importe de cada pago debe ser mayor a cero.";
            }
        }

        return null;
    }

    private static decimal ObtenerPrecioUnitario(
        Producto producto,
        string tipoVenta)
    {
        if (tipoVenta == "Peso")
        {
            if (!producto.PrecioPorKilo.HasValue ||
                producto.PrecioPorKilo.Value <= 0)
            {
                throw new VentaValidationException(
                    $"El producto {producto.Nombre} no tiene un precio por kilo válido.");
            }

            return producto.PrecioPorKilo.Value;
        }

        if (!producto.PrecioPorUnidad.HasValue ||
            producto.PrecioPorUnidad.Value <= 0)
        {
            throw new VentaValidationException(
                $"El producto {producto.Nombre} no tiene un precio por unidad válido.");
        }

        return producto.PrecioPorUnidad.Value;
    }

    private async Task<string> GenerarNumeroVentaAsync()
    {
        var ahora = DateTime.Now;
        var fecha = ahora.ToString("yyyyMMdd");

        var inicioDia = ahora.Date;
        var finDia = inicioDia.AddDays(1);

        var cantidadVentasHoy = await _context.Ventas
            .CountAsync(v =>
                v.FechaVenta >= inicioDia &&
                v.FechaVenta < finDia);

        var correlativo = cantidadVentasHoy + 1;

        return $"V-{fecha}-{correlativo:000000}";
    }

    private sealed class VentaValidationException : Exception
    {
        public VentaValidationException(string message)
            : base(message)
        {
        }
    }
}