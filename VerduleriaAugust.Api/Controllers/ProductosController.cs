using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VerduleriaAugust.Api.DTOs;
using VerduleriaAugust.Api.Models;

namespace VerduleriaAugust.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProductosController : ControllerBase
{
    private readonly AugustDbContext _context;

    public ProductosController(AugustDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetProductos()
    {
        var productos = await _context.Productos
            .Include(p => p.Categoria)
            .Include(p => p.Stock)
            .OrderBy(p => p.Nombre)
            .Select(p => new
            {
                p.Id,
                p.Codigo,
                p.Nombre,
                p.CategoriaId,
                Categoria = p.Categoria != null ? p.Categoria.Nombre : null,
                p.TipoVenta,
                p.PrecioPorKilo,
                p.PrecioPorUnidad,
                p.Activo,
                Stock = p.Stock == null ? null : new
                {
                    p.Stock.StockActual,
                    p.Stock.UnidadMedida,
                    p.Stock.StockMinimo,
                    p.Stock.FechaActualizacion
                }
            })
            .ToListAsync();

        return Ok(productos);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetProducto(int id)
    {
        var producto = await _context.Productos
            .Include(p => p.Categoria)
            .Include(p => p.Stock)
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id,
                p.Codigo,
                p.Nombre,
                p.CategoriaId,
                Categoria = p.Categoria != null ? p.Categoria.Nombre : null,
                p.TipoVenta,
                p.PrecioPorKilo,
                p.PrecioPorUnidad,
                p.Activo,
                Stock = p.Stock == null ? null : new
                {
                    p.Stock.StockActual,
                    p.Stock.UnidadMedida,
                    p.Stock.StockMinimo,
                    p.Stock.FechaActualizacion
                }
            })
            .FirstOrDefaultAsync();

        if (producto == null)
            return NotFound(new { mensaje = "Producto no encontrado." });

        return Ok(producto);
    }

    [HttpPost]
    public async Task<IActionResult> CrearProducto([FromBody] CrearProductoDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Codigo))
            return BadRequest(new { mensaje = "El código es obligatorio." });

        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest(new { mensaje = "El nombre es obligatorio." });

        if (!await _context.Categorias.AnyAsync(c => c.Id == dto.CategoriaId))
            return BadRequest(new { mensaje = "La categoría indicada no existe." });

        if (!TipoVentaValido(dto.TipoVenta))
            return BadRequest(new { mensaje = "TipoVenta debe ser Peso, Unidad o Ambos." });

        if (!UnidadMedidaValida(dto.UnidadMedida))
            return BadRequest(new { mensaje = "UnidadMedida debe ser Kg o Unidad." });

        if (!PreciosValidos(dto.TipoVenta, dto.PrecioPorKilo, dto.PrecioPorUnidad))
            return BadRequest(new { mensaje = "Los precios no coinciden con el tipo de venta." });

        var codigoExiste = await _context.Productos
            .AnyAsync(p => p.Codigo == dto.Codigo);

        if (codigoExiste)
            return BadRequest(new { mensaje = "Ya existe un producto con ese código." });

        var producto = new Producto
        {
            Codigo = dto.Codigo.Trim(),
            Nombre = dto.Nombre.Trim(),
            CategoriaId = dto.CategoriaId,
            TipoVenta = dto.TipoVenta,
            PrecioPorKilo = dto.PrecioPorKilo,
            PrecioPorUnidad = dto.PrecioPorUnidad,
            Activo = true,
            FechaCreacion = DateTime.Now
        };

        var stock = new Stock
        {
            Producto = producto,
            StockActual = dto.StockInicial,
            UnidadMedida = dto.UnidadMedida,
            StockMinimo = dto.StockMinimo,
            FechaActualizacion = DateTime.Now
        };

        _context.Productos.Add(producto);
        _context.Stock.Add(stock);

        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetProducto), new { id = producto.Id }, new
        {
            mensaje = "Producto creado correctamente.",
            producto.Id
        });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> ActualizarProducto(int id, [FromBody] ActualizarProductoDto dto)
    {
        var producto = await _context.Productos
            .Include(p => p.Stock)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (producto == null)
            return NotFound(new { mensaje = "Producto no encontrado." });

        if (string.IsNullOrWhiteSpace(dto.Codigo))
            return BadRequest(new { mensaje = "El código es obligatorio." });

        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest(new { mensaje = "El nombre es obligatorio." });

        if (!await _context.Categorias.AnyAsync(c => c.Id == dto.CategoriaId))
            return BadRequest(new { mensaje = "La categoría indicada no existe." });

        if (!TipoVentaValido(dto.TipoVenta))
            return BadRequest(new { mensaje = "TipoVenta debe ser Peso, Unidad o Ambos." });

        if (!UnidadMedidaValida(dto.UnidadMedida))
            return BadRequest(new { mensaje = "UnidadMedida debe ser Kg o Unidad." });

        if (!PreciosValidos(dto.TipoVenta, dto.PrecioPorKilo, dto.PrecioPorUnidad))
            return BadRequest(new { mensaje = "Los precios no coinciden con el tipo de venta." });

        var codigoUsadoPorOtro = await _context.Productos
            .AnyAsync(p => p.Codigo == dto.Codigo && p.Id != id);

        if (codigoUsadoPorOtro)
            return BadRequest(new { mensaje = "Ya existe otro producto con ese código." });

        producto.Codigo = dto.Codigo.Trim();
        producto.Nombre = dto.Nombre.Trim();
        producto.CategoriaId = dto.CategoriaId;
        producto.TipoVenta = dto.TipoVenta;
        producto.PrecioPorKilo = dto.PrecioPorKilo;
        producto.PrecioPorUnidad = dto.PrecioPorUnidad;
        producto.Activo = dto.Activo;

        if (producto.Stock == null)
        {
            producto.Stock = new Stock
            {
                ProductoId = producto.Id,
                StockActual = dto.StockActual,
                UnidadMedida = dto.UnidadMedida,
                StockMinimo = dto.StockMinimo,
                FechaActualizacion = DateTime.Now
            };
        }
        else
        {
            producto.Stock.StockActual = dto.StockActual;
            producto.Stock.UnidadMedida = dto.UnidadMedida;
            producto.Stock.StockMinimo = dto.StockMinimo;
            producto.Stock.FechaActualizacion = DateTime.Now;
        }

        await _context.SaveChangesAsync();

        return Ok(new { mensaje = "Producto actualizado correctamente." });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DarDeBajaProducto(int id)
    {
        var producto = await _context.Productos.FindAsync(id);

        if (producto == null)
            return NotFound(new { mensaje = "Producto no encontrado." });

        producto.Activo = false;

        await _context.SaveChangesAsync();

        return Ok(new { mensaje = "Producto dado de baja correctamente." });
    }

    private static bool TipoVentaValido(string tipoVenta)
    {
        return tipoVenta == "Peso" || tipoVenta == "Unidad" || tipoVenta == "Ambos";
    }

    private static bool UnidadMedidaValida(string unidadMedida)
    {
        return unidadMedida == "Kg" || unidadMedida == "Unidad";
    }

    private static bool PreciosValidos(string tipoVenta, decimal? precioPorKilo, decimal? precioPorUnidad)
    {
        return tipoVenta switch
        {
            "Peso" => precioPorKilo.HasValue && precioPorKilo > 0,
            "Unidad" => precioPorUnidad.HasValue && precioPorUnidad > 0,
            "Ambos" => precioPorKilo.HasValue && precioPorKilo > 0
                       && precioPorUnidad.HasValue && precioPorUnidad > 0,
            _ => false
        };
    }
}