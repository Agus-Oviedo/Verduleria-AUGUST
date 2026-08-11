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
public class CategoriasController : ControllerBase
{
    private readonly AugustDbContext _context;

    public CategoriasController(AugustDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(RespuestaPaginada<CategoriaListadoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategorias([FromQuery] CategoriasConsultaDto consulta)
    {
        var query = _context.Categorias.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(consulta.Buscar))
        {
            var buscar = consulta.Buscar.Trim();
            query = query.Where(c => c.Nombre.Contains(buscar));
        }
        if (consulta.Activa.HasValue)
            query = query.Where(c => c.Activa == consulta.Activa.Value);

        var total = await query.CountAsync();
        var categorias = await query
            .OrderBy(c => c.Nombre)
            .ThenBy(c => c.Id)
            .Skip((consulta.Pagina - 1) * consulta.TamanoPagina)
            .Take(consulta.TamanoPagina)
            .Select(c => new CategoriaListadoDto(c.Id, c.Nombre, c.Activa, c.FechaCreacion))
            .ToListAsync();

        return Ok(new RespuestaPaginada<CategoriaListadoDto>(categorias,
            total, consulta.Pagina, consulta.TamanoPagina));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CategoriaListadoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategoria(int id)
    {
        var categoria = await _context.Categorias.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoriaListadoDto(c.Id, c.Nombre, c.Activa, c.FechaCreacion))
            .SingleOrDefaultAsync();

        if (categoria == null)
            return NotFound();

        return Ok(categoria);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Administracion)]
    public async Task<IActionResult> Crear([FromBody] GuardarCategoriaDto dto)
    {
        var nombre = dto.Nombre?.Trim();
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 100)
            return BadRequest(new { mensaje = "El nombre es obligatorio y no puede superar 100 caracteres." });
        if (await _context.Categorias.AnyAsync(c => c.Nombre.ToUpper() == nombre.ToUpper()))
            return Conflict(new { mensaje = "Ya existe una categoría con ese nombre." });

        var categoria = new Categoria { Nombre = nombre, Activa = dto.Activa, FechaCreacion = DateTime.Now };
        _context.Categorias.Add(categoria);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetCategoria), new { id = categoria.Id }, new { mensaje = "Categoría creada correctamente.", categoria.Id });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Administracion)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] GuardarCategoriaDto dto)
    {
        var categoria = await _context.Categorias.FindAsync(id);
        if (categoria == null) return NotFound(new { mensaje = "Categoría no encontrada." });
        var nombre = dto.Nombre?.Trim();
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Length > 100)
            return BadRequest(new { mensaje = "El nombre es obligatorio y no puede superar 100 caracteres." });
        if (await _context.Categorias.AnyAsync(c => c.Id != id && c.Nombre.ToUpper() == nombre.ToUpper()))
            return Conflict(new { mensaje = "Ya existe otra categoría con ese nombre." });
        if (!dto.Activa && await _context.Productos.AnyAsync(p => p.CategoriaId == id && p.Activo))
            return Conflict(new { mensaje = "No se puede desactivar una categoría con productos activos." });

        categoria.Nombre = nombre;
        categoria.Activa = dto.Activa;
        await _context.SaveChangesAsync();
        return Ok(new { mensaje = "Categoría actualizada correctamente." });
    }
}
