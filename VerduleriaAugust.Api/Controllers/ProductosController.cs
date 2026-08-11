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
public class ProductosController : ControllerBase
{
    private readonly AugustDbContext _context;

    public ProductosController(AugustDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(RespuestaPaginada<ProductoListadoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProductos([FromQuery] ProductosConsultaDto consulta)
    {
        var query = _context.Productos
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(consulta.Buscar))
        {
            var buscar = consulta.Buscar.Trim();
            query = query.Where(p =>
                p.Nombre.Contains(buscar) || p.Codigo.Contains(buscar));
        }

        if (consulta.CategoriaId.HasValue)
            query = query.Where(p => p.CategoriaId == consulta.CategoriaId.Value);

        if (consulta.Activo.HasValue)
            query = query.Where(p => p.Activo == consulta.Activo.Value);

        if (!string.IsNullOrWhiteSpace(consulta.TipoVenta))
            query = query.Where(p => p.TipoVenta == consulta.TipoVenta);

        var total = await query.CountAsync();
        var productos = await query
            .OrderBy(p => p.Nombre)
            .ThenBy(p => p.Id)
            .Skip((consulta.Pagina - 1) * consulta.TamanoPagina)
            .Take(consulta.TamanoPagina)
            .Select(p => new ProductoListadoDto(
                p.Id, p.Codigo, p.Nombre, p.CategoriaId,
                p.Categoria != null ? p.Categoria.Nombre : null,
                p.TipoVenta, p.PrecioPorKilo, p.PrecioPorUnidad, p.Activo,
                p.Stock == null ? null : new StockResumenDto(
                    p.Stock.StockActual, p.Stock.UnidadMedida,
                    p.Stock.StockMinimo, p.Stock.FechaActualizacion)))
            .ToListAsync();

        return Ok(new RespuestaPaginada<ProductoListadoDto>(
            productos,
            total,
            consulta.Pagina,
            consulta.TamanoPagina));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductoListadoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProducto(int id)
    {
        var producto = await _context.Productos
            .Include(p => p.Categoria)
            .Include(p => p.Stock)
            .Where(p => p.Id == id)
            .Select(p => new ProductoListadoDto(
                p.Id, p.Codigo, p.Nombre, p.CategoriaId,
                p.Categoria != null ? p.Categoria.Nombre : null,
                p.TipoVenta, p.PrecioPorKilo, p.PrecioPorUnidad, p.Activo,
                p.Stock == null ? null : new StockResumenDto(
                    p.Stock.StockActual, p.Stock.UnidadMedida,
                    p.Stock.StockMinimo, p.Stock.FechaActualizacion)))
            .FirstOrDefaultAsync();

        if (producto == null)
            return NotFound(new { mensaje = "Producto no encontrado." });

        return Ok(producto);
    }

    [HttpGet("{id:int}/historial-precios")]
    [Authorize(Roles = Roles.Administracion)]
    [ProducesResponseType(typeof(RespuestaPaginada<HistorialPrecioListadoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistorialPrecios(
        int id,
        [FromQuery] HistorialPreciosConsultaDto consulta)
    {
        if (consulta.Desde.HasValue && consulta.Hasta.HasValue && consulta.Desde > consulta.Hasta)
            return BadRequest(new { mensaje = "La fecha Desde no puede ser posterior a Hasta." });

        if (!await _context.Productos.AnyAsync(p => p.Id == id))
            return NotFound(new { mensaje = "Producto no encontrado." });

        var query = _context.HistorialPrecios.AsNoTracking()
            .Where(h => h.ProductoId == id);

        if (consulta.Desde.HasValue)
            query = query.Where(h => h.FechaCambio >= consulta.Desde.Value);

        if (consulta.Hasta.HasValue)
            query = query.Where(h => h.FechaCambio <= consulta.Hasta.Value);

        var total = await query.CountAsync();
        var history = await query
            .OrderByDescending(h => h.FechaCambio)
            .ThenByDescending(h => h.Id)
            .Skip((consulta.Pagina - 1) * consulta.TamanoPagina)
            .Take(consulta.TamanoPagina)
            .Select(h => new HistorialPrecioListadoDto(
                h.Id, h.PrecioPorKiloAnterior, h.PrecioPorKiloNuevo,
                h.PrecioPorUnidadAnterior, h.PrecioPorUnidadNuevo,
                h.Motivo, h.FechaCambio,
                h.Usuario != null ? h.Usuario.NombreUsuario : null))
            .ToListAsync();
        return Ok(new RespuestaPaginada<HistorialPrecioListadoDto>(history,
            total, consulta.Pagina, consulta.TamanoPagina));
    }

    [HttpGet("{id:int}/costos-compra")]
    [Authorize(Roles = Roles.Administracion)]
    public async Task<IActionResult> GetCostosCompra(int id)
    {
        if (!await _context.Productos.AnyAsync(p => p.Id == id))
            return NotFound(new { mensaje = "Producto no encontrado." });

        var compras = await _context.RecepcionesMercaderiaDetalles.AsNoTracking()
            .Where(d => d.ProductoId == id && d.CostoUnitario.HasValue && d.RecepcionMercaderia!.Estado != "Anulada")
            .Select(d => new
            {
                d.CostoUnitario,
                CantidadNeta = d.Cantidad - d.RecepcionMercaderia!.Correcciones.SelectMany(c => c.Detalles).Where(cd => cd.ProductoId == id).Sum(cd => (decimal?)cd.Cantidad) ?? d.Cantidad,
                d.RecepcionMercaderia!.FechaRecepcion,
                Proveedor = d.RecepcionMercaderia.Proveedor!.Nombre,
                d.RecepcionMercaderia.NumeroRecepcion
            })
            .ToListAsync();

        var vigentes = compras.Where(c => c.CantidadNeta > 0).OrderByDescending(c => c.FechaRecepcion).ToList();
        if (vigentes.Count == 0) return Ok(new { ultimoCosto = (decimal?)null, costoPromedio = (decimal?)null, fechaUltimaCompra = (DateTime?)null, proveedorUltimaCompra = (string?)null, numeroRecepcion = (string?)null, cantidadConsiderada = 0m });
        var cantidad = vigentes.Sum(c => c.CantidadNeta);
        var promedio = cantidad > 0 ? Math.Round(vigentes.Sum(c => c.CantidadNeta * c.CostoUnitario!.Value) / cantidad, 2) : (decimal?)null;
        var ultima = vigentes[0];
        return Ok(new { ultimoCosto = ultima.CostoUnitario, costoPromedio = promedio, fechaUltimaCompra = (DateTime?)ultima.FechaRecepcion, proveedorUltimaCompra = ultima.Proveedor, numeroRecepcion = ultima.NumeroRecepcion, cantidadConsiderada = cantidad });
    }

    [HttpPost]
    [Authorize(Roles = Roles.Administracion)]
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

        if (!TryGetUserId(out var userId))
            return Unauthorized(new { mensaje = "El token no identifica un usuario válido." });

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
        _context.HistorialPrecios.Add(new HistorialPrecio
        {
            Producto = producto,
            PrecioPorKiloNuevo = dto.PrecioPorKilo,
            PrecioPorUnidadNuevo = dto.PrecioPorUnidad,
            UsuarioId = userId,
            Motivo = "Precio inicial",
            FechaCambio = DateTime.Now
        });

        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetProducto), new { id = producto.Id }, new
        {
            mensaje = "Producto creado correctamente.",
            producto.Id
        });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Administracion)]
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

        var precioCambio = producto.PrecioPorKilo != dto.PrecioPorKilo ||
                           producto.PrecioPorUnidad != dto.PrecioPorUnidad;
        if (precioCambio &&
            (string.IsNullOrWhiteSpace(dto.MotivoCambioPrecio) ||
             dto.MotivoCambioPrecio.Trim().Length < 5))
        {
            return BadRequest(new { mensaje = "El cambio de precio requiere un motivo de al menos 5 caracteres." });
        }
        if (dto.MotivoCambioPrecio?.Trim().Length > 300)
            return BadRequest(new { mensaje = "El motivo no puede superar 300 caracteres." });

        if (precioCambio)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized(new { mensaje = "El token no identifica un usuario válido." });
            _context.HistorialPrecios.Add(new HistorialPrecio
            {
                ProductoId = producto.Id,
                PrecioPorKiloAnterior = producto.PrecioPorKilo,
                PrecioPorKiloNuevo = dto.PrecioPorKilo,
                PrecioPorUnidadAnterior = producto.PrecioPorUnidad,
                PrecioPorUnidadNuevo = dto.PrecioPorUnidad,
                UsuarioId = userId,
                Motivo = dto.MotivoCambioPrecio!.Trim(),
                FechaCambio = DateTime.Now
            });
        }

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
                StockActual = 0,
                UnidadMedida = dto.UnidadMedida,
                StockMinimo = dto.StockMinimo,
                FechaActualizacion = DateTime.Now
            };
        }
        else
        {
            producto.Stock.UnidadMedida = dto.UnidadMedida;
            producto.Stock.StockMinimo = dto.StockMinimo;
            producto.Stock.FechaActualizacion = DateTime.Now;
        }

        await _context.SaveChangesAsync();

        return Ok(new { mensaje = "Producto actualizado correctamente." });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Administracion)]
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

    private bool TryGetUserId(out int userId)
    {
        var user = ControllerContext.HttpContext?.User;
        var value = user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? user?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(value, out userId);
    }
}
