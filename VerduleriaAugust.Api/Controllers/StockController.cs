using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VerduleriaAugust.Api.DTOs;
using VerduleriaAugust.Api.Models;
using VerduleriaAugust.Api.Security;

namespace VerduleriaAugust.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class StockController : ControllerBase
{
    private readonly AugustDbContext _context;

    public StockController(AugustDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(RespuestaPaginada<StockListadoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStock([FromQuery] StockConsultaDto consulta)
    {
        var query = _context.Stock.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(consulta.Buscar))
        {
            var buscar = consulta.Buscar.Trim();
            query = query.Where(s => s.Producto != null &&
                (s.Producto.Nombre.Contains(buscar) || s.Producto.Codigo.Contains(buscar)));
        }

        if (consulta.CategoriaId.HasValue)
            query = query.Where(s => s.Producto!.CategoriaId == consulta.CategoriaId.Value);

        if (consulta.BajoStock == true)
            query = query.Where(s => s.StockMinimo.HasValue && s.StockActual <= s.StockMinimo.Value);
        else if (consulta.BajoStock == false)
            query = query.Where(s => !s.StockMinimo.HasValue || s.StockActual > s.StockMinimo.Value);

        var total = await query.CountAsync();
        var stock = await query
            .OrderBy(s => s.Producto!.Nombre)
            .ThenBy(s => s.Id)
            .Skip((consulta.Pagina - 1) * consulta.TamanoPagina)
            .Take(consulta.TamanoPagina)
            .Select(s => new StockListadoDto(
                s.Id, s.ProductoId, s.Producto!.Nombre, s.Producto.Codigo,
                s.Producto.Categoria != null ? s.Producto.Categoria.Nombre : null,
                s.StockActual, s.UnidadMedida, s.StockMinimo, s.FechaActualizacion,
                s.StockMinimo.HasValue && s.StockActual <= s.StockMinimo.Value))
            .ToListAsync();

        return Ok(new RespuestaPaginada<StockListadoDto>(stock,
            total, consulta.Pagina, consulta.TamanoPagina));
    }

    [HttpGet("producto/{productoId:int}")]
    [ProducesResponseType(typeof(StockProductoRespuestaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStockPorProducto(int productoId)
    {
        var stock = await _context.Stock
            .Include(s => s.Producto)
            .Where(s => s.ProductoId == productoId)
            .Select(s => new StockProductoRespuestaDto(
                s.Id, s.ProductoId, s.Producto!.Nombre, s.Producto.Codigo,
                s.StockActual, s.UnidadMedida, s.StockMinimo, s.FechaActualizacion))
            .FirstOrDefaultAsync();

        if (stock == null)
            return NotFound(new { mensaje = "No se encontró stock para ese producto." });

        return Ok(stock);
    }

    [HttpGet("movimientos")]
    [ProducesResponseType(typeof(RespuestaPaginada<MovimientoStockListadoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMovimientos([FromQuery] MovimientosStockConsultaDto consulta)
    {
        if (consulta.Desde.HasValue && consulta.Hasta.HasValue && consulta.Desde > consulta.Hasta)
            return BadRequest(new { mensaje = "La fecha Desde no puede ser posterior a Hasta." });

        var query = _context.MovimientosStock.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(consulta.Buscar))
        {
            var buscar = consulta.Buscar.Trim();
            query = query.Where(m => m.Producto != null &&
                (m.Producto.Nombre.Contains(buscar) || m.Producto.Codigo.Contains(buscar)));
        }

        if (consulta.ProductoId.HasValue)
            query = query.Where(m => m.ProductoId == consulta.ProductoId.Value);

        if (!string.IsNullOrWhiteSpace(consulta.TipoMovimiento))
            query = query.Where(m => m.TipoMovimiento == consulta.TipoMovimiento);

        if (consulta.Desde.HasValue)
            query = query.Where(m => m.FechaMovimiento >= consulta.Desde.Value);

        if (consulta.Hasta.HasValue)
            query = query.Where(m => m.FechaMovimiento <= consulta.Hasta.Value);

        var total = await query.CountAsync();
        var movimientos = await query
            .OrderByDescending(m => m.FechaMovimiento)
            .ThenByDescending(m => m.Id)
            .Skip((consulta.Pagina - 1) * consulta.TamanoPagina)
            .Take(consulta.TamanoPagina)
            .Select(m => new MovimientoStockListadoDto(
                m.Id, m.ProductoId, m.Producto!.Nombre, m.Producto.Codigo,
                m.TipoMovimiento, m.Cantidad, m.StockAnterior, m.StockNuevo,
                m.Motivo, m.Usuario != null ? m.Usuario.NombreUsuario : null,
                m.FechaMovimiento))
            .ToListAsync();

        return Ok(new RespuestaPaginada<MovimientoStockListadoDto>(movimientos,
            total, consulta.Pagina, consulta.TamanoPagina));
    }

    [HttpPost("entrada")]
    [Authorize(Roles = Roles.Administracion)]
    public async Task<IActionResult> EntradaStock([FromBody] MovimientoStockDto dto)
    {
        if (dto.Cantidad <= 0)
            return BadRequest(new { mensaje = "La cantidad debe ser mayor a cero." });
        var motivoError = ValidarMotivo(dto.Motivo);
        if (motivoError != null)
            return BadRequest(new { mensaje = motivoError });

        var stock = await _context.Stock
            .FirstOrDefaultAsync(s => s.ProductoId == dto.ProductoId);

        if (stock == null)
            return NotFound(new { mensaje = "No se encontró stock para ese producto." });
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { mensaje = "El token no identifica un usuario válido." });

        var stockAnterior = stock.StockActual;
        var stockNuevo = stock.StockActual + dto.Cantidad;

        stock.StockActual = stockNuevo;
        stock.FechaActualizacion = DateTime.Now;

        var movimiento = new MovimientoStock
        {
            ProductoId = dto.ProductoId,
            UsuarioId = userId,
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
    [Authorize(Roles = Roles.Administracion)]
    public async Task<IActionResult> SalidaStock([FromBody] MovimientoStockDto dto)
    {
        if (dto.Cantidad <= 0)
            return BadRequest(new { mensaje = "La cantidad debe ser mayor a cero." });
        var motivoError = ValidarMotivo(dto.Motivo);
        if (motivoError != null)
            return BadRequest(new { mensaje = motivoError });

        var stock = await _context.Stock
            .FirstOrDefaultAsync(s => s.ProductoId == dto.ProductoId);

        if (stock == null)
            return NotFound(new { mensaje = "No se encontró stock para ese producto." });

        if (stock.StockActual < dto.Cantidad)
            return BadRequest(new { mensaje = "Stock insuficiente para realizar la salida." });
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { mensaje = "El token no identifica un usuario válido." });

        var stockAnterior = stock.StockActual;
        var stockNuevo = stock.StockActual - dto.Cantidad;

        stock.StockActual = stockNuevo;
        stock.FechaActualizacion = DateTime.Now;

        var movimiento = new MovimientoStock
        {
            ProductoId = dto.ProductoId,
            UsuarioId = userId,
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
    [Authorize(Roles = Roles.Administracion)]
    public async Task<IActionResult> AjusteStock([FromBody] AjusteStockDto dto)
    {
        if (dto.NuevoStock < 0)
            return BadRequest(new { mensaje = "El nuevo stock no puede ser negativo." });
        var motivoError = ValidarMotivo(dto.Motivo);
        if (motivoError != null)
            return BadRequest(new { mensaje = motivoError });

        var stock = await _context.Stock
            .FirstOrDefaultAsync(s => s.ProductoId == dto.ProductoId);

        if (stock == null)
            return NotFound(new { mensaje = "No se encontró stock para ese producto." });
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { mensaje = "El token no identifica un usuario válido." });

        var stockAnterior = stock.StockActual;
        var diferencia = dto.NuevoStock - stockAnterior;

        stock.StockActual = dto.NuevoStock;
        stock.FechaActualizacion = DateTime.Now;

        var movimiento = new MovimientoStock
        {
            ProductoId = dto.ProductoId,
            UsuarioId = userId,
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

    private static string? ValidarMotivo(string? motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo) || motivo.Trim().Length < 5)
            return "El motivo debe tener al menos 5 caracteres.";
        if (motivo.Trim().Length > 200)
            return "El motivo no puede superar 200 caracteres.";
        return null;
    }

    private bool TryGetUserId(out int userId)
    {
        var user = ControllerContext.HttpContext?.User;
        var value = user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? user?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(value, out userId);
    }
}
