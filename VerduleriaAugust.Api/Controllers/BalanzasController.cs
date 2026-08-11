using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using VerduleriaAugust.Api.DTOs;
using VerduleriaAugust.Api.Models;
using VerduleriaAugust.Api.Security;
using VerduleriaAugust.Api.Services;

namespace VerduleriaAugust.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = Roles.Administracion)]
public sealed class BalanzasController(
    AugustDbContext context,
    BalanzaLecturaStore? lecturaStore = null) : ControllerBase
{
    private readonly BalanzaLecturaStore _lecturaStore = lecturaStore ?? new();
    [HttpGet]
    [ProducesResponseType(typeof(RespuestaPaginada<BalanzaRespuestaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] BalanzasConsultaDto consulta)
    {
        var query = context.Balanzas.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(consulta.Buscar))
        {
            var buscar = consulta.Buscar.Trim();
            query = query.Where(b => b.Marca.Contains(buscar) ||
                b.Modelo.Contains(buscar) ||
                b.PuertoCom.Contains(buscar) ||
                (b.NumeroSerie != null && b.NumeroSerie.Contains(buscar)));
        }
        if (consulta.CajaId.HasValue)
            query = query.Where(b => b.CajaId == consulta.CajaId.Value);
        if (consulta.Activa.HasValue)
            query = query.Where(b => b.Activa == consulta.Activa.Value);

        var total = await query.CountAsync();
        var items = await Project(query.OrderBy(b => b.Marca).ThenBy(b => b.Modelo)
                .ThenBy(b => b.Id)
                .Skip((consulta.Pagina - 1) * consulta.TamanoPagina)
                .Take(consulta.TamanoPagina))
            .ToListAsync();
        return Ok(new RespuestaPaginada<BalanzaRespuestaDto>(
            items, total, consulta.Pagina, consulta.TamanoPagina));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BalanzaRespuestaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(int id)
    {
        var balanza = await Project(context.Balanzas.AsNoTracking().Where(b => b.Id == id))
            .SingleOrDefaultAsync();
        return balanza == null
            ? NotFound(new { mensaje = "Balanza no encontrada." })
            : Ok(balanza);
    }

    [HttpPost]
    [ProducesResponseType(typeof(BalanzaCreadaRespuestaDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Crear([FromBody] GuardarBalanzaDto dto)
    {
        var error = await ValidarAsync(dto);
        if (error != null)
            return Conflict(new { mensaje = error });

        var apiKey = GenerateApiKey();
        var balanza = new Balanza
        {
            CajaId = dto.CajaId,
            Marca = dto.Marca.Trim(),
            Modelo = dto.Modelo.Trim(),
            PuertoCom = dto.PuertoCom.Trim().ToUpperInvariant(),
            BaudRate = dto.BaudRate,
            DataBits = dto.DataBits,
            Paridad = dto.Paridad,
            StopBits = dto.StopBits,
            NumeroSerie = NormalizeOptional(dto.NumeroSerie),
            ApiKeyHash = HashApiKey(apiKey),
            Activa = dto.Activa,
            FechaCreacion = DateTime.Now
        };
        context.Balanzas.Add(balanza);
        await context.SaveChangesAsync();

        var response = ToResponse(balanza, null);
        return CreatedAtAction(nameof(Get), new { id = balanza.Id },
            new BalanzaCreadaRespuestaDto(
                "Balanza creada. Guardá la clave: no volverá a mostrarse.",
                response, apiKey));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] GuardarBalanzaDto dto)
    {
        var balanza = await context.Balanzas.FindAsync(id);
        if (balanza == null)
            return NotFound(new { mensaje = "Balanza no encontrada." });
        var error = await ValidarAsync(dto, id);
        if (error != null)
            return Conflict(new { mensaje = error });

        balanza.CajaId = dto.CajaId;
        balanza.Marca = dto.Marca.Trim();
        balanza.Modelo = dto.Modelo.Trim();
        balanza.PuertoCom = dto.PuertoCom.Trim().ToUpperInvariant();
        balanza.BaudRate = dto.BaudRate;
        balanza.DataBits = dto.DataBits;
        balanza.Paridad = dto.Paridad;
        balanza.StopBits = dto.StopBits;
        balanza.NumeroSerie = NormalizeOptional(dto.NumeroSerie);
        balanza.Activa = dto.Activa;
        await context.SaveChangesAsync();
        return Ok(new { mensaje = "Balanza actualizada correctamente." });
    }

    [HttpPost("{id:int}/regenerar-clave")]
    [ProducesResponseType(typeof(ApiKeyRegeneradaRespuestaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RegenerarClave(int id)
    {
        var balanza = await context.Balanzas.FindAsync(id);
        if (balanza == null)
            return NotFound(new { mensaje = "Balanza no encontrada." });
        var apiKey = GenerateApiKey();
        balanza.ApiKeyHash = HashApiKey(apiKey);
        await context.SaveChangesAsync();
        return Ok(new ApiKeyRegeneradaRespuestaDto(
            "Clave regenerada. La clave anterior dejó de funcionar.", id, apiKey));
    }

    [AllowAnonymous]
    [HttpPost("{id:int}/lecturas")]
    [EnableRateLimiting("AgenteBalanza")]
    [ProducesResponseType(typeof(LecturaBalanzaAceptadaDto), StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RegistrarLectura(
        int id,
        [FromHeader(Name = "X-Agent-Key")] string? apiKey,
        [FromBody] RegistrarLecturaBalanzaDto dto)
    {
        var balanza = await context.Balanzas.SingleOrDefaultAsync(b => b.Id == id && b.Activa);
        if (balanza == null || !ApiKeyMatches(apiKey, balanza.ApiKeyHash))
            return Unauthorized(new { mensaje = "Credencial de agente inválida." });

        var now = DateTimeOffset.UtcNow;
        if (dto.FechaLectura == default ||
            dto.FechaLectura < now.AddMinutes(-2) ||
            dto.FechaLectura > now.AddSeconds(30))
        {
            return BadRequest(new { mensaje = "La fecha de lectura está fuera del rango permitido." });
        }

        if (!_lecturaStore.TrySet(balanza.Id, balanza.CajaId, dto, now, out _))
            return Conflict(new { mensaje = "La lectura es anterior a la última recibida." });

        if (!balanza.UltimaConexion.HasValue || balanza.UltimaConexion < DateTime.UtcNow.AddSeconds(-30))
        {
            balanza.UltimaConexion = DateTime.UtcNow;
            await context.SaveChangesAsync();
        }

        return Accepted(new LecturaBalanzaAceptadaDto(
            "Lectura recibida correctamente.", dto.LecturaId, now));
    }

    [Authorize(Roles = Roles.OperacionVenta)]
    [HttpGet("caja/{cajaId:int}/lectura-actual")]
    [ProducesResponseType(typeof(LecturaBalanzaRespuestaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLecturaActual(int cajaId)
    {
        if (!await context.Cajas.AsNoTracking().AnyAsync(c => c.Id == cajaId && c.Activa))
            return NotFound(new { mensaje = "La caja no existe o está inactiva." });

        var lectura = _lecturaStore.GetCurrent(cajaId, DateTimeOffset.UtcNow);
        return lectura == null
            ? NotFound(new { mensaje = "La caja todavía no recibió lecturas de peso." })
            : Ok(lectura);
    }

    private async Task<string?> ValidarAsync(GuardarBalanzaDto dto, int? exceptId = null)
    {
        if (!await context.Cajas.AnyAsync(c => c.Id == dto.CajaId && c.Activa))
            return "La caja no existe o está inactiva.";
        if (dto.Activa && await context.Balanzas.AnyAsync(b =>
                b.CajaId == dto.CajaId && b.Activa && b.Id != exceptId))
            return "La caja ya tiene una balanza activa. Desactivala antes de vincular otra.";
        var serial = NormalizeOptional(dto.NumeroSerie);
        if (serial != null && await context.Balanzas.AnyAsync(b =>
                b.NumeroSerie == serial && b.Id != exceptId))
            return "El número de serie ya está registrado.";
        return null;
    }

    private static IQueryable<BalanzaRespuestaDto> Project(IQueryable<Balanza> query) =>
        query.Select(b => new BalanzaRespuestaDto(
            b.Id, b.CajaId, b.Caja != null ? b.Caja.Nombre : null,
            b.Marca, b.Modelo, b.PuertoCom, b.BaudRate, b.DataBits,
            b.Paridad, b.StopBits, b.NumeroSerie, b.Activa,
            b.FechaCreacion, b.UltimaConexion));

    private static BalanzaRespuestaDto ToResponse(Balanza b, string? caja) =>
        new(b.Id, b.CajaId, caja, b.Marca, b.Modelo,
            b.PuertoCom, b.BaudRate, b.DataBits, b.Paridad, b.StopBits, b.NumeroSerie,
            b.Activa, b.FechaCreacion, b.UltimaConexion);

    private static string GenerateApiKey() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    internal static string HashApiKey(string apiKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));

    private static bool ApiKeyMatches(string? apiKey, string? expectedHash)
    {
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(expectedHash))
            return false;
        var provided = Convert.FromHexString(HashApiKey(apiKey));
        var expected = Convert.FromHexString(expectedHash);
        return CryptographicOperations.FixedTimeEquals(provided, expected);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
