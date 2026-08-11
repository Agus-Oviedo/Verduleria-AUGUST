using System.Security.Claims;
using System.Data;
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
public class CajasController : ControllerBase
{
    private readonly AugustDbContext _context;

    public CajasController(AugustDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [ProducesResponseType(typeof(RespuestaPaginada<CajaListadoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCajas([FromQuery] CajasConsultaDto consulta)
    {
        var query = _context.Cajas.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(consulta.Buscar))
        {
            var buscar = consulta.Buscar.Trim();
            query = query.Where(c => c.Nombre.Contains(buscar) ||
                c.Codigo.Contains(buscar) ||
                (c.PcIdentificador != null && c.PcIdentificador.Contains(buscar)));
        }

        if (consulta.Activa.HasValue)
            query = query.Where(c => c.Activa == consulta.Activa.Value);

        if (consulta.ConSesionAbierta == true)
            query = query.Where(c => c.Sesiones.Any(s => s.Estado == "Abierta"));
        else if (consulta.ConSesionAbierta == false)
            query = query.Where(c => !c.Sesiones.Any(s => s.Estado == "Abierta"));

        var total = await query.CountAsync();
        var cajas = await query
            .OrderBy(c => c.Nombre)
            .ThenBy(c => c.Id)
            .Skip((consulta.Pagina - 1) * consulta.TamanoPagina)
            .Take(consulta.TamanoPagina)
            .Select(c => new CajaListadoDto(
                c.Id, c.Nombre, c.Codigo, c.PcIdentificador, c.Activa,
                c.Sesiones.Where(s => s.Estado == "Abierta")
                    .Select(s => new SesionCajaResumenDto(
                        s.Id, s.FechaApertura, s.SaldoInicial, s.UsuarioAperturaId))
                    .FirstOrDefault()))
            .ToListAsync();
        return Ok(new RespuestaPaginada<CajaListadoDto>(cajas,
            total, consulta.Pagina, consulta.TamanoPagina));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CajaRespuestaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCaja(int id)
    {
        var caja = await _context.Cajas.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CajaRespuestaDto(
                c.Id, c.Nombre, c.Codigo, c.PcIdentificador, c.Activa))
            .SingleOrDefaultAsync();
        return caja == null ? NotFound() : Ok(caja);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Administracion)]
    public async Task<IActionResult> Crear([FromBody] GuardarCajaDto dto)
    {
        var error = ValidarCaja(dto);
        if (error != null)
            return BadRequest(new { mensaje = error });
        var codigo = dto.Codigo.Trim().ToUpperInvariant();
        if (await _context.Cajas.AnyAsync(c => c.Codigo == codigo))
            return Conflict(new { mensaje = "Ya existe una caja con ese código." });

        var caja = new Caja
        {
            Nombre = dto.Nombre.Trim(), Codigo = codigo,
            PcIdentificador = NormalizeOptional(dto.PcIdentificador),
            Activa = dto.Activa, FechaCreacion = DateTime.Now
        };
        _context.Cajas.Add(caja);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetCaja), new { id = caja.Id }, new
        {
            mensaje = "Caja creada correctamente.", caja.Id
        });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Administracion)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] GuardarCajaDto dto)
    {
        var error = ValidarCaja(dto);
        if (error != null)
            return BadRequest(new { mensaje = error });
        var caja = await _context.Cajas.FindAsync(id);
        if (caja == null)
            return NotFound(new { mensaje = "Caja no encontrada." });
        var codigo = dto.Codigo.Trim().ToUpperInvariant();
        if (await _context.Cajas.AnyAsync(c => c.Codigo == codigo && c.Id != id))
            return Conflict(new { mensaje = "Ya existe otra caja con ese código." });
        if (!dto.Activa && await _context.SesionesCaja.AnyAsync(s =>
                s.CajaId == id && s.Estado == "Abierta"))
            return Conflict(new { mensaje = "No se puede desactivar una caja abierta." });

        caja.Nombre = dto.Nombre.Trim();
        caja.Codigo = codigo;
        caja.PcIdentificador = NormalizeOptional(dto.PcIdentificador);
        caja.Activa = dto.Activa;
        await _context.SaveChangesAsync();
        return Ok(new { mensaje = "Caja actualizada correctamente." });
    }

    [HttpPost("{cajaId:int}/abrir")]
    [ProducesResponseType(typeof(CajaAbiertaRespuestaDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Abrir(int cajaId, [FromBody] AbrirCajaDto dto)
    {
        if (dto.SaldoInicial < 0)
            return BadRequest(new { mensaje = "El saldo inicial no puede ser negativo." });
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { mensaje = "El token no identifica un usuario válido." });
        if (!await _context.Cajas.AnyAsync(c => c.Id == cajaId && c.Activa))
            return NotFound(new { mensaje = "La caja no existe o está inactiva." });
        if (await _context.SesionesCaja.AnyAsync(s => s.CajaId == cajaId && s.Estado == "Abierta"))
            return Conflict(new { mensaje = "La caja ya tiene una sesión abierta." });

        var sesion = new SesionCaja
        {
            CajaId = cajaId,
            UsuarioAperturaId = userId,
            FechaApertura = DateTime.Now,
            SaldoInicial = dto.SaldoInicial,
            Estado = "Abierta"
        };
        _context.SesionesCaja.Add(sesion);
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { mensaje = "La caja fue abierta por otra operación." });
        }

        return StatusCode(StatusCodes.Status201Created, new CajaAbiertaRespuestaDto(
            "Caja abierta correctamente.", sesion.Id, sesion.CajaId,
            sesion.FechaApertura, sesion.SaldoInicial));
    }

    [HttpPost("sesiones/{sesionId:int}/cerrar")]
    [ProducesResponseType(typeof(CajaCerradaRespuestaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cerrar(int sesionId, [FromBody] CerrarCajaDto dto)
    {
        if (dto.EfectivoDeclarado < 0)
            return BadRequest(new { mensaje = "El efectivo declarado no puede ser negativo." });
        if (dto.Observaciones?.Length > 500)
            return BadRequest(new { mensaje = "Las observaciones no pueden superar 500 caracteres." });
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { mensaje = "El token no identifica un usuario válido." });

        var sesion = await _context.SesionesCaja.SingleOrDefaultAsync(s => s.Id == sesionId);
        if (sesion == null)
            return NotFound(new { mensaje = "Sesión de caja no encontrada." });
        if (sesion.Estado != "Abierta")
            return Conflict(new { mensaje = "La sesión de caja ya está cerrada." });
        if (User.IsInRole(Roles.Cajero) && sesion.UsuarioAperturaId != userId)
            return Forbid();

        var ventasEfectivo = await _context.PagosVenta.AsNoTracking()
            .Where(p => p.Venta!.SesionCajaId == sesion.Id &&
                        p.Venta.Estado == "Finalizada" &&
                        p.FormaPago!.EsEfectivo)
            .SumAsync(p => (decimal?)p.Importe) ?? 0m;
        var ingresos = await _context.MovimientosCaja.AsNoTracking().Where(m => m.SesionCajaId == sesion.Id && !m.Anulado && m.Tipo == "Ingreso").SumAsync(m => (decimal?)m.Importe) ?? 0m;
        var retiros = await _context.MovimientosCaja.AsNoTracking().Where(m => m.SesionCajaId == sesion.Id && !m.Anulado && m.Tipo == "Retiro").SumAsync(m => (decimal?)m.Importe) ?? 0m;
        var esperado = Math.Round(sesion.SaldoInicial + ventasEfectivo + ingresos - retiros, 2);
        var declarado = Math.Round(dto.EfectivoDeclarado, 2);

        sesion.Estado = "Cerrada";
        sesion.UsuarioCierreId = userId;
        sesion.FechaCierre = DateTime.Now;
        sesion.EfectivoEsperado = esperado;
        sesion.EfectivoDeclarado = declarado;
        sesion.Diferencia = declarado - esperado;
        sesion.ObservacionesCierre = dto.Observaciones?.Trim();

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { mensaje = "La sesión fue modificada por otra operación." });
        }

        return Ok(new CajaCerradaRespuestaDto(
            "Caja cerrada correctamente.", sesion.Id,
            sesion.EfectivoEsperado, sesion.EfectivoDeclarado, sesion.Diferencia));
    }

    [HttpPost("sesiones/{sesionId:int}/movimientos")]
    [Authorize(Roles = Roles.Administracion)]
    public async Task<IActionResult> CrearMovimiento(int sesionId, [FromBody] CrearMovimientoCajaDto dto)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { mensaje = "El token no identifica un usuario válido." });
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var sesion = await _context.SesionesCaja.SingleOrDefaultAsync(s => s.Id == sesionId);
        if (sesion is null) return NotFound(new { mensaje = "Sesión de caja no encontrada." });
        if (sesion.Estado != "Abierta") return Conflict(new { mensaje = "Solo se pueden registrar movimientos en una caja abierta." });
        if (dto.Tipo == "Retiro")
        {
            var ventasEfectivo = await _context.PagosVenta.Where(p => p.Venta!.SesionCajaId == sesionId && p.Venta.Estado == "Finalizada" && p.FormaPago!.EsEfectivo).SumAsync(p => (decimal?)p.Importe) ?? 0m;
            var ingresos = await _context.MovimientosCaja.Where(m => m.SesionCajaId == sesionId && !m.Anulado && m.Tipo == "Ingreso").SumAsync(m => (decimal?)m.Importe) ?? 0m;
            var retiros = await _context.MovimientosCaja.Where(m => m.SesionCajaId == sesionId && !m.Anulado && m.Tipo == "Retiro").SumAsync(m => (decimal?)m.Importe) ?? 0m;
            var disponible = Math.Round(sesion.SaldoInicial + ventasEfectivo + ingresos - retiros, 2);
            if (Math.Round(dto.Importe, 2) > disponible)
                return Conflict(new { mensaje = $"El retiro supera el efectivo disponible ($ {disponible:N2})." });
        }
        var movimiento = new MovimientoCaja { SesionCajaId = sesionId, UsuarioId = userId, Tipo = dto.Tipo, Importe = Math.Round(dto.Importe, 2), Motivo = dto.Motivo.Trim(), Fecha = DateTime.Now };
        _context.MovimientosCaja.Add(movimiento);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return StatusCode(StatusCodes.Status201Created, new MovimientoCajaRespuestaDto(movimiento.Id, movimiento.Tipo, movimiento.Importe, movimiento.Motivo, movimiento.Fecha, movimiento.UsuarioId, User.Identity?.Name, false, null, null, null));
    }

    [HttpPost("movimientos/{movimientoId:int}/anular")]
    [Authorize(Roles = Roles.Administracion)]
    public async Task<IActionResult> AnularMovimiento(int movimientoId, [FromBody] AnularMovimientoCajaDto dto)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { mensaje = "El token no identifica un usuario válido." });
        var movimiento = await _context.MovimientosCaja.Include(m => m.SesionCaja).SingleOrDefaultAsync(m => m.Id == movimientoId);
        if (movimiento is null) return NotFound(new { mensaje = "Movimiento de caja no encontrado." });
        if (movimiento.Anulado) return Conflict(new { mensaje = "El movimiento ya está anulado." });
        if (movimiento.SesionCaja?.Estado != "Abierta") return Conflict(new { mensaje = "No se puede corregir un movimiento de un turno cerrado." });
        movimiento.Anulado = true; movimiento.UsuarioAnulacionId = userId; movimiento.FechaAnulacion = DateTime.Now; movimiento.MotivoAnulacion = dto.Motivo.Trim();
        await _context.SaveChangesAsync();
        return Ok(new { mensaje = "Movimiento anulado correctamente." });
    }

    [HttpGet("sesiones/{sesionId:int}/arqueo")]
    [ProducesResponseType(typeof(ArqueoCajaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetArqueo(int sesionId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { mensaje = "El token no identifica un usuario válido." });

        var sesion = await _context.SesionesCaja.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == sesionId);
        if (sesion == null)
            return NotFound(new { mensaje = "Sesión de caja no encontrada." });
        if (sesion.Estado != "Abierta")
            return Conflict(new { mensaje = "La sesión de caja ya está cerrada." });
        if (User.IsInRole(Roles.Cajero) && sesion.UsuarioAperturaId != userId)
            return Forbid();

        var ventas = _context.Ventas.AsNoTracking()
            .Where(v => v.SesionCajaId == sesion.Id && v.Estado == "Finalizada");
        var cantidadVentas = await ventas.CountAsync();
        var totalVendido = await ventas.SumAsync(v => (decimal?)v.Total) ?? 0m;
        var totalesPorForma = await _context.PagosVenta.AsNoTracking()
            .Where(p => p.Venta!.SesionCajaId == sesion.Id &&
                        p.Venta.Estado == "Finalizada")
            .GroupBy(p => new { p.FormaPagoId, p.FormaPago!.Nombre, p.FormaPago.EsEfectivo })
            .Select(group => new
            {
                group.Key.FormaPagoId,
                group.Key.Nombre,
                group.Key.EsEfectivo,
                Total = group.Sum(payment => payment.Importe)
            })
            .OrderByDescending(item => item.EsEfectivo)
            .ThenBy(item => item.Nombre)
            .ToListAsync();
        var formasPago = totalesPorForma.Select(item => new ArqueoFormaPagoDto(
            item.FormaPagoId, item.Nombre, item.EsEfectivo, item.Total)).ToList();
        var ventasEfectivo = formasPago.Where(item => item.EsEfectivo).Sum(item => item.Total);
        var movimientos = await _context.MovimientosCaja.AsNoTracking()
            .Where(m => m.SesionCajaId == sesion.Id).OrderByDescending(m => m.Fecha)
            .Select(m => new MovimientoCajaRespuestaDto(m.Id, m.Tipo, m.Importe, m.Motivo, m.Fecha, m.UsuarioId, m.Usuario != null ? m.Usuario.NombreUsuario : null, m.Anulado, m.FechaAnulacion, m.MotivoAnulacion, m.UsuarioAnulacion != null ? m.UsuarioAnulacion.NombreUsuario : null)).ToListAsync();
        var ingresos = movimientos.Where(m => !m.Anulado && m.Tipo == "Ingreso").Sum(m => m.Importe);
        var retiros = movimientos.Where(m => !m.Anulado && m.Tipo == "Retiro").Sum(m => m.Importe);

        return Ok(new ArqueoCajaDto(
            sesion.Id,
            sesion.FechaApertura,
            sesion.SaldoInicial,
            cantidadVentas,
            Math.Round(totalVendido, 2),
            Math.Round(ventasEfectivo, 2),
            Math.Round(ingresos, 2),
            Math.Round(retiros, 2),
            Math.Round(sesion.SaldoInicial + ventasEfectivo + ingresos - retiros, 2),
            formasPago,
            movimientos));
    }

    [HttpGet("sesiones")]
    [ProducesResponseType(typeof(RespuestaPaginada<TurnoCajaListadoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTurnos([FromQuery] TurnosCajaConsultaDto consulta)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { mensaje = "El token no identifica un usuario válido." });

        var query = _context.SesionesCaja.AsNoTracking().AsQueryable();
        if (User.IsInRole(Roles.Cajero))
            query = query.Where(session => session.UsuarioAperturaId == userId);
        if (consulta.CajaId.HasValue)
            query = query.Where(session => session.CajaId == consulta.CajaId.Value);
        if (!string.IsNullOrWhiteSpace(consulta.Usuario))
        {
            var usuario = consulta.Usuario.Trim();
            query = query.Where(session =>
                session.UsuarioApertura!.NombreUsuario.Contains(usuario) ||
                (session.UsuarioCierre != null && session.UsuarioCierre.NombreUsuario.Contains(usuario)));
        }
        if (!string.IsNullOrWhiteSpace(consulta.Estado))
            query = query.Where(session => session.Estado == consulta.Estado);
        if (consulta.Desde.HasValue)
            query = query.Where(session => session.FechaApertura >= consulta.Desde.Value);
        if (consulta.Hasta.HasValue)
            query = query.Where(session => session.FechaApertura <= consulta.Hasta.Value);

        var total = await query.CountAsync();
        var sessions = await query
            .OrderByDescending(session => session.FechaApertura)
            .ThenByDescending(session => session.Id)
            .Skip((consulta.Pagina - 1) * consulta.TamanoPagina)
            .Take(consulta.TamanoPagina)
            .Select(session => new
            {
                session.Id,
                session.CajaId,
                Caja = session.Caja!.Nombre,
                CodigoCaja = session.Caja.Codigo,
                session.Estado,
                session.UsuarioAperturaId,
                UsuarioApertura = session.UsuarioApertura!.NombreUsuario,
                session.UsuarioCierreId,
                UsuarioCierre = session.UsuarioCierre != null ? session.UsuarioCierre.NombreUsuario : null,
                session.FechaApertura,
                session.FechaCierre,
                session.SaldoInicial,
                CantidadVentas = session.Ventas.Count(venta => venta.Estado == "Finalizada"),
                TotalVendido = session.Ventas.Where(venta => venta.Estado == "Finalizada")
                    .Sum(venta => (decimal?)venta.Total) ?? 0m,
                session.EfectivoEsperado,
                session.EfectivoDeclarado,
                session.Diferencia,
                Observaciones = session.ObservacionesCierre
            })
            .ToListAsync();

        var sessionIds = sessions.Select(session => session.Id).ToList();
        var paymentTotals = await _context.PagosVenta.AsNoTracking()
            .Where(payment => payment.Venta!.SesionCajaId.HasValue &&
                              sessionIds.Contains(payment.Venta.SesionCajaId.Value) &&
                              payment.Venta.Estado == "Finalizada")
            .GroupBy(payment => new
            {
                SesionId = payment.Venta!.SesionCajaId!.Value,
                payment.FormaPagoId,
                payment.FormaPago!.Nombre,
                payment.FormaPago.EsEfectivo
            })
            .Select(group => new
            {
                group.Key.SesionId,
                group.Key.FormaPagoId,
                group.Key.Nombre,
                group.Key.EsEfectivo,
                Total = group.Sum(payment => payment.Importe)
            })
            .ToListAsync();
        var cashMovements = await _context.MovimientosCaja.AsNoTracking()
            .Where(movement => sessionIds.Contains(movement.SesionCajaId))
            .OrderByDescending(movement => movement.Fecha)
            .Select(movement => new { movement.SesionCajaId, Item = new MovimientoCajaRespuestaDto(movement.Id, movement.Tipo, movement.Importe, movement.Motivo, movement.Fecha, movement.UsuarioId, movement.Usuario != null ? movement.Usuario.NombreUsuario : null, movement.Anulado, movement.FechaAnulacion, movement.MotivoAnulacion, movement.UsuarioAnulacion != null ? movement.UsuarioAnulacion.NombreUsuario : null) })
            .ToListAsync();

        var items = sessions.Select(session => new TurnoCajaListadoDto(
            session.Id, session.CajaId, session.Caja, session.CodigoCaja, session.Estado,
            session.UsuarioAperturaId, session.UsuarioApertura,
            session.UsuarioCierreId, session.UsuarioCierre,
            session.FechaApertura, session.FechaCierre,
            session.FechaCierre.HasValue
                ? Math.Max(0, (int)(session.FechaCierre.Value - session.FechaApertura).TotalMinutes)
                : null,
            session.SaldoInicial, session.CantidadVentas, Math.Round(session.TotalVendido, 2),
            session.EfectivoEsperado, session.EfectivoDeclarado, session.Diferencia,
            session.Observaciones,
            cashMovements.Where(movement => movement.SesionCajaId == session.Id && !movement.Item.Anulado && movement.Item.Tipo == "Ingreso").Sum(movement => movement.Item.Importe),
            cashMovements.Where(movement => movement.SesionCajaId == session.Id && !movement.Item.Anulado && movement.Item.Tipo == "Retiro").Sum(movement => movement.Item.Importe),
            paymentTotals.Where(payment => payment.SesionId == session.Id)
                .OrderByDescending(payment => payment.EsEfectivo)
                .ThenBy(payment => payment.Nombre)
                .Select(payment => new ArqueoFormaPagoDto(
                    payment.FormaPagoId, payment.Nombre, payment.EsEfectivo, payment.Total))
                .ToList(),
            cashMovements.Where(movement => movement.SesionCajaId == session.Id).Select(movement => movement.Item).ToList())).ToList();

        return Ok(new RespuestaPaginada<TurnoCajaListadoDto>(
            items, total, consulta.Pagina, consulta.TamanoPagina));
    }

    private bool TryGetUserId(out int userId)
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        return int.TryParse(value, out userId);
    }

    private static string? ValidarCaja(GuardarCajaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre) || dto.Nombre.Trim().Length > 100)
            return "El nombre es obligatorio y no puede superar 100 caracteres.";
        if (string.IsNullOrWhiteSpace(dto.Codigo) || dto.Codigo.Trim().Length > 50)
            return "El código es obligatorio y no puede superar 50 caracteres.";
        if (dto.PcIdentificador?.Trim().Length > 100)
            return "El identificador de PC no puede superar 100 caracteres.";
        return null;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
