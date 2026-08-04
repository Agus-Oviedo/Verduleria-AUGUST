using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VerduleriaAugust.Api.DTOs;
using VerduleriaAugust.Api.Models;

namespace VerduleriaAugust.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class StockController : ControllerBase
{
    private readonly AugustDbContext _context;

    public StockController(AugustDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetStock()
    {
        var stock = await _context.Stock
            .Include(s => s.Producto)
            .ThenInclude(p => p!.Categoria)
            .OrderBy(s => s.Producto!.Nombre)
            .Select(s => new
            {
                s.Id,
                s.ProductoId,
                Producto = s.Producto!.Nombre,
                Codigo = s.Producto.Codigo,
                Categoria = s.Producto.Categoria != null ? s.Producto.Categoria.Nombre : null,
                s.StockActual,
                s.UnidadMedida,
                s.StockMinimo,
                s.FechaActualizacion,
                BajoStock = s.StockMinimo.HasValue && s.StockActual <= s.StockMinimo.Value
            })
            .ToListAsync();

        return Ok(stock);
    }

    [HttpGet("producto/{productoId:int}")]
    public async Task<IActionResult> GetStockPorProducto(int productoId)
    {
        var stock = await _context.Stock
            .Include(s => s.Producto)
            .Where(s => s.ProductoId == productoId)
            .Select(s => new
            {
                s.Id,
                s.ProductoId,
                Producto = s.Producto!.Nombre,
                Codigo = s.Producto.Codigo,
                s.StockActual,
                s.UnidadMedida,
                s.StockMinimo,
                s.FechaActualizacion
            })
            .FirstOrDefaultAsync();

        if (stock == null)
            return NotFound(new { mensaje = "No se encontró stock para ese producto." });

        return Ok(stock);
    }

    [HttpGet("movimientos")]
    public async Task<IActionResult> GetMovimientos()
    {
        var movimientos = await _context.MovimientosStock
            .Include(m => m.Producto)
            .OrderByDescending(m => m.FechaMovimiento)
            .Take(200)
            .Select(m => new
            {
                m.Id,
                m.ProductoId,
                Producto = m.Producto!.Nombre,
                Codigo = m.Producto.Codigo,
                m.TipoMovimiento,
                m.Cantidad,
                m.StockAnterior,
                m.StockNuevo,
                m.Motivo,
                m.FechaMovimiento
            })
            .ToListAsync();

        return Ok(movimientos);
    }

    [HttpPost("entrada")]
    public async Task<IActionResult> EntradaStock([FromBody] MovimientoStockDto dto)
    {
        if (dto.Cantidad <= 0)
            return BadRequest(new { mensaje = "La cantidad debe ser mayor a cero." });

        var stock = await _context.Stock
            .FirstOrDefaultAsync(s => s.ProductoId == dto.ProductoId);

        if (stock == null)
            return NotFound(new { mensaje = "No se encontró stock para ese producto." });

        var stockAnterior = stock.StockActual;
        var stockNuevo = stock.StockActual + dto.Cantidad;

        stock.StockActual = stockNuevo;
        stock.FechaActualizacion = DateTime.Now;

        var movimiento = new MovimientoStock
        {
            ProductoId = dto.ProductoId,
            TipoMovimiento = "Entrada",
            Cantidad = dto.Cantidad,
            StockAnterior = stockAnterior,
            StockNuevo = stockNuevo,
            Motivo = dto.Motivo,
            FechaMovimiento = DateTime.Now
        };

        _context.MovimientosStock.Add(movimiento);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensaje = "Entrada de stock registrada correctamente.",
            stockAnterior,
            stockNuevo
        });
    }

    [HttpPost("salida")]
    public async Task<IActionResult> SalidaStock([FromBody] MovimientoStockDto dto)
    {
        if (dto.Cantidad <= 0)
            return BadRequest(new { mensaje = "La cantidad debe ser mayor a cero." });

        var stock = await _context.Stock
            .FirstOrDefaultAsync(s => s.ProductoId == dto.ProductoId);

        if (stock == null)
            return NotFound(new { mensaje = "No se encontró stock para ese producto." });

        if (stock.StockActual < dto.Cantidad)
            return BadRequest(new { mensaje = "Stock insuficiente para realizar la salida." });

        var stockAnterior = stock.StockActual;
        var stockNuevo = stock.StockActual - dto.Cantidad;

        stock.StockActual = stockNuevo;
        stock.FechaActualizacion = DateTime.Now;

        var movimiento = new MovimientoStock
        {
            ProductoId = dto.ProductoId,
            TipoMovimiento = "Salida",
            Cantidad = dto.Cantidad,
            StockAnterior = stockAnterior,
            StockNuevo = stockNuevo,
            Motivo = dto.Motivo,
            FechaMovimiento = DateTime.Now
        };

        _context.MovimientosStock.Add(movimiento);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensaje = "Salida de stock registrada correctamente.",
            stockAnterior,
            stockNuevo
        });
    }

    [HttpPost("ajuste")]
    public async Task<IActionResult> AjusteStock([FromBody] AjusteStockDto dto)
    {
        if (dto.NuevoStock < 0)
            return BadRequest(new { mensaje = "El nuevo stock no puede ser negativo." });

        var stock = await _context.Stock
            .FirstOrDefaultAsync(s => s.ProductoId == dto.ProductoId);

        if (stock == null)
            return NotFound(new { mensaje = "No se encontró stock para ese producto." });

        var stockAnterior = stock.StockActual;
        var diferencia = dto.NuevoStock - stockAnterior;

        stock.StockActual = dto.NuevoStock;
        stock.FechaActualizacion = DateTime.Now;

        var movimiento = new MovimientoStock
        {
            ProductoId = dto.ProductoId,
            TipoMovimiento = "Ajuste",
            Cantidad = diferencia,
            StockAnterior = stockAnterior,
            StockNuevo = dto.NuevoStock,
            Motivo = dto.Motivo,
            FechaMovimiento = DateTime.Now
        };

        _context.MovimientosStock.Add(movimiento);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            mensaje = "Ajuste de stock registrado correctamente.",
            stockAnterior,
            stockNuevo = dto.NuevoStock,
            diferencia
        });
    }
}