using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VerduleriaAugust.Api.DTOs;
using VerduleriaAugust.Api.Models;
using VerduleriaAugust.Api.Security;

namespace VerduleriaAugust.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = Roles.OperacionVenta)]
public class FormasPagoController : ControllerBase
{
    private readonly AugustDbContext _context;

    public FormasPagoController(AugustDbContext context) => _context = context;

    [HttpGet]
    [ProducesResponseType(typeof(RespuestaPaginada<FormaPagoListadoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] FormasPagoConsultaDto consulta)
    {
        var query = _context.FormasPago.AsNoTracking()
            .Where(f => consulta.IncluirInactivas || f.Activa);
        if (!string.IsNullOrWhiteSpace(consulta.Buscar))
        {
            var buscar = consulta.Buscar.Trim();
            query = query.Where(f => f.Nombre.Contains(buscar));
        }
        if (consulta.EsEfectivo.HasValue)
            query = query.Where(f => f.EsEfectivo == consulta.EsEfectivo.Value);

        var total = await query.CountAsync();
        var formas = await query
            .OrderBy(f => f.Orden)
            .ThenBy(f => f.Nombre)
            .ThenBy(f => f.Id)
            .Skip((consulta.Pagina - 1) * consulta.TamanoPagina)
            .Take(consulta.TamanoPagina)
            .Select(f => new FormaPagoListadoDto(
                f.Id, f.Nombre, f.EsEfectivo, f.Activa, f.Orden, f.PagosVenta.Count))
            .ToListAsync();
        return Ok(new RespuestaPaginada<FormaPagoListadoDto>(formas,
            total, consulta.Pagina, consulta.TamanoPagina));
    }

    [HttpPost]
    [Authorize(Roles = Roles.Administracion)]
    public async Task<IActionResult> Crear([FromBody] GuardarFormaPagoDto dto)
    {
        var error = Validar(dto);
        if (error != null)
            return BadRequest(new { mensaje = error });
        var nombre = dto.Nombre.Trim();
        if (await ExisteNombreAsync(nombre))
            return Conflict(new { mensaje = "Ya existe una forma de pago con ese nombre." });
        var forma = new FormaPago
        {
            Nombre = nombre, EsEfectivo = dto.EsEfectivo,
            Activa = dto.Activa, Orden = dto.Orden
        };
        _context.FormasPago.Add(forma);
        await _context.SaveChangesAsync();
        return StatusCode(StatusCodes.Status201Created, new
        {
            mensaje = "Forma de pago creada correctamente.", forma.Id
        });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Administracion)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] GuardarFormaPagoDto dto)
    {
        var error = Validar(dto);
        if (error != null)
            return BadRequest(new { mensaje = error });
        var forma = await _context.FormasPago.FindAsync(id);
        if (forma == null)
            return NotFound(new { mensaje = "Forma de pago no encontrada." });
        var nombre = dto.Nombre.Trim();
        if (await ExisteNombreAsync(nombre, id))
            return Conflict(new { mensaje = "Ya existe otra forma de pago con ese nombre." });
        forma.Nombre = nombre;
        forma.EsEfectivo = dto.EsEfectivo;
        forma.Activa = dto.Activa;
        forma.Orden = dto.Orden;
        await _context.SaveChangesAsync();
        return Ok(new { mensaje = "Forma de pago actualizada correctamente." });
    }

    private Task<bool> ExisteNombreAsync(string nombre, int? exceptId = null) =>
        _context.FormasPago.AnyAsync(f =>
            f.Nombre.ToUpper() == nombre.ToUpper() && (!exceptId.HasValue || f.Id != exceptId));

    private static string? Validar(GuardarFormaPagoDto dto) =>
        string.IsNullOrWhiteSpace(dto.Nombre) || dto.Nombre.Trim().Length > 100
            ? "El nombre es obligatorio y no puede superar 100 caracteres."
            : null;
}
