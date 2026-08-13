using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VerduleriaAugust.Api.DTOs;
using VerduleriaAugust.Api.Models;
using VerduleriaAugust.Api.Security;

namespace VerduleriaAugust.Api.Controllers;

[ApiController, Route("api/recepciones-mercaderia"), Authorize(Roles = Roles.Administracion)]
public sealed class RecepcionesMercaderiaController(AugustDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRecepciones([FromQuery] int pagina = 1, [FromQuery] int tamanoPagina = 10, [FromQuery] string? buscar = null, [FromQuery] int? proveedorId = null, [FromQuery] string? estado = null, [FromQuery] DateTime? desde = null, [FromQuery] DateTime? hasta = null)
    {
        pagina = Math.Max(1, pagina); tamanoPagina = Math.Clamp(tamanoPagina, 1, 50);
        if (desde.HasValue && hasta.HasValue && desde.Value.Date > hasta.Value.Date) return BadRequest(new { mensaje = "La fecha desde no puede ser posterior a la fecha hasta." });
        var query = context.RecepcionesMercaderia.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(buscar)) { var texto = buscar.Trim(); query = query.Where(r => r.NumeroRecepcion.Contains(texto) || (r.Comprobante != null && r.Comprobante.Contains(texto))); }
        if (proveedorId.HasValue) query = query.Where(r => r.ProveedorId == proveedorId.Value);
        if (!string.IsNullOrWhiteSpace(estado)) query = query.Where(r => r.Estado == estado);
        if (desde.HasValue) query = query.Where(r => r.FechaRecepcion >= desde.Value.Date);
        if (hasta.HasValue) { var limite = hasta.Value.Date.AddDays(1); query = query.Where(r => r.FechaRecepcion < limite); }
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(r => r.FechaRecepcion).ThenByDescending(r => r.Id).Skip((pagina - 1) * tamanoPagina).Take(tamanoPagina)
            .Select(r => new { r.Id, r.NumeroRecepcion, r.FechaRecepcion, r.ProveedorId, Proveedor = r.Proveedor!.Nombre, Usuario = r.Usuario!.NombreUsuario, r.Comprobante, r.Observaciones, r.TotalCosto, r.Estado, Detalles = r.Detalles.Select(d => new { d.ProductoId, Producto = d.Producto!.Nombre, d.Cantidad, CantidadCorregida = r.Correcciones.SelectMany(c => c.Detalles).Where(cd => cd.ProductoId == d.ProductoId).Sum(cd => (decimal?)cd.Cantidad) ?? 0, d.CostoUnitario, d.TotalCosto }) }).ToListAsync();
        return Ok(new { items, total, pagina, tamanoPagina, totalPaginas = (int)Math.Ceiling(total / (double)tamanoPagina) });
    }

    [HttpGet("proveedores")]
    public async Task<IActionResult> GetProveedores() => Ok(await context.Proveedores.AsNoTracking().Where(p => p.Activo).OrderBy(p => p.Nombre).ToListAsync());

    [HttpGet("{id:int}/auditoria"), Authorize(Roles = Roles.Administrador)]
    public async Task<IActionResult> GetAuditoria(int id)
    {
        var recepcion = await context.RecepcionesMercaderia.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new { r.Id, r.NumeroRecepcion, r.FechaRecepcion, Usuario = r.Usuario!.NombreUsuario })
            .SingleOrDefaultAsync();
        if (recepcion is null) return NotFound(new { mensaje = "La recepción no existe." });

        var eventos = await context.RecepcionesMercaderiaEventos.AsNoTracking()
            .Where(e => e.RecepcionMercaderiaId == id)
            .OrderByDescending(e => e.Fecha)
            .Select(e => new { e.Id, e.Tipo, e.Detalle, e.Fecha, Usuario = e.Usuario!.NombreUsuario })
            .ToListAsync();
        eventos.Add(new { Id = 0, Tipo = "Creada", Detalle = $"Recepción {recepcion.NumeroRecepcion} registrada.", Fecha = recepcion.FechaRecepcion, recepcion.Usuario });
        return Ok(eventos.OrderByDescending(e => e.Fecha));
    }

    [HttpPost("proveedores")]
    public async Task<IActionResult> CrearProveedor([FromBody] CrearProveedorDto dto)
    {
        var nombre = dto.Nombre.Trim();
        if (await context.Proveedores.AnyAsync(p => p.Nombre == nombre)) return Conflict(new { mensaje = "Ya existe un proveedor con ese nombre." });
        var proveedor = new Proveedor { Nombre = nombre, Cuit = Normalizar(dto.Cuit), Contacto = Normalizar(dto.Contacto), Activo = true, FechaCreacion = DateTime.Now };
        context.Proveedores.Add(proveedor); await context.SaveChangesAsync();
        return StatusCode(201, proveedor);
    }

    [HttpPost]
    public async Task<IActionResult> CrearRecepcion([FromBody] CrearRecepcionMercaderiaDto dto)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { mensaje = "El token no identifica un usuario válido." });
        if (dto.Items.GroupBy(i => i.ProductoId).Any(g => g.Count() > 1)) return BadRequest(new { mensaje = "Un producto no puede repetirse en la misma recepción." });
        if (!await context.Proveedores.AnyAsync(p => p.Id == dto.ProveedorId && p.Activo)) return BadRequest(new { mensaje = "El proveedor no existe o está inactivo." });
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var recepcion = new RecepcionMercaderia { NumeroRecepcion = $"P-{Guid.NewGuid():N}", ProveedorId = dto.ProveedorId, UsuarioId = userId, Comprobante = Normalizar(dto.Comprobante), Observaciones = Normalizar(dto.Observaciones), FechaRecepcion = DateTime.Now, Estado = "Confirmada" };
            context.RecepcionesMercaderia.Add(recepcion); await context.SaveChangesAsync();
            decimal totalCosto = 0;
            foreach (var item in dto.Items)
            {
                var stock = await context.Stock.Include(s => s.Producto).SingleOrDefaultAsync(s => s.ProductoId == item.ProductoId);
                if (stock?.Producto is null || !stock.Producto.Activo) throw new InvalidOperationException($"El producto {item.ProductoId} no existe o está inactivo.");
                var anterior = stock.StockActual; var nuevo = Math.Round(anterior + item.Cantidad, 3); decimal? totalLinea = item.CostoUnitario.HasValue ? Math.Round(item.Cantidad * item.CostoUnitario.Value, 2) : null;
                stock.StockActual = nuevo; stock.FechaActualizacion = DateTime.Now;
                recepcion.Detalles.Add(new RecepcionMercaderiaDetalle { ProductoId = item.ProductoId, Cantidad = item.Cantidad, CostoUnitario = item.CostoUnitario, TotalCosto = totalLinea });
                var motivo = $"Recepción {recepcion.Id} · {dto.Comprobante ?? "sin comprobante"}";
                context.MovimientosStock.Add(new MovimientoStock { ProductoId = item.ProductoId, UsuarioId = userId, TipoMovimiento = "Entrada", Cantidad = item.Cantidad, StockAnterior = anterior, StockNuevo = nuevo, Motivo = motivo[..Math.Min(200, motivo.Length)], FechaMovimiento = DateTime.Now });
                totalCosto += totalLinea ?? 0;
            }
            recepcion.TotalCosto = totalCosto; recepcion.NumeroRecepcion = $"R-{recepcion.FechaRecepcion:yyyyMMdd}-{recepcion.Id:D8}";
            await context.SaveChangesAsync(); await transaction.CommitAsync();
            return StatusCode(201, new { mensaje = "Mercadería ingresada correctamente.", recepcion.Id, recepcion.NumeroRecepcion, recepcion.TotalCosto });
        }
        catch (InvalidOperationException ex) { await transaction.RollbackAsync(); return BadRequest(new { mensaje = ex.Message }); }
        catch (DbUpdateConcurrencyException) { await transaction.RollbackAsync(); return Conflict(new { mensaje = "El stock cambió durante la recepción. Volvé a intentarlo." }); }
    }

    [HttpPut("{id:int}/datos")]
    public async Task<IActionResult> ActualizarDatos(int id, [FromBody] ActualizarDatosRecepcionDto dto)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { mensaje = "El token no identifica un usuario válido." });
        var recepcion = await context.RecepcionesMercaderia.SingleOrDefaultAsync(r => r.Id == id);
        if (recepcion is null) return NotFound(new { mensaje = "La recepción no existe." });
        if (!await context.Proveedores.AnyAsync(p => p.Id == dto.ProveedorId && p.Activo)) return BadRequest(new { mensaje = "El proveedor no existe o está inactivo." });

        var comprobanteAnterior = recepcion.Comprobante ?? "sin comprobante";
        var observacionesAnteriores = recepcion.Observaciones ?? "sin observaciones";
        var proveedorAnterior = recepcion.ProveedorId;
        recepcion.ProveedorId = dto.ProveedorId;
        recepcion.Comprobante = Normalizar(dto.Comprobante);
        recepcion.Observaciones = Normalizar(dto.Observaciones);
        context.RecepcionesMercaderiaEventos.Add(new RecepcionMercaderiaEvento
        {
            RecepcionMercaderiaId = id,
            UsuarioId = userId,
            Tipo = "DatosEditados",
            Detalle = $"Motivo: {dto.Motivo.Trim()}. Proveedor {proveedorAnterior} → {dto.ProveedorId}. Comprobante: {comprobanteAnterior} → {recepcion.Comprobante ?? "sin comprobante"}. Observaciones: {observacionesAnteriores} → {recepcion.Observaciones ?? "sin observaciones"}.",
            Fecha = DateTime.Now
        });
        await context.SaveChangesAsync();
        return Ok(new { mensaje = "Datos del comprobante actualizados. El stock no fue modificado." });
    }

    [HttpPost("{id:int}/corregir")]
    public async Task<IActionResult> Corregir(int id, [FromBody] CorregirRecepcionDto dto)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { mensaje = "El token no identifica un usuario válido." });
        if (dto.Items.GroupBy(i => i.ProductoId).Any(g => g.Count() > 1)) return BadRequest(new { mensaje = "Un producto no puede repetirse en la corrección." });
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var recepcion = await context.RecepcionesMercaderia.Include(r => r.Detalles).Include(r => r.Correcciones).ThenInclude(c => c.Detalles).SingleOrDefaultAsync(r => r.Id == id);
            if (recepcion is null) return NotFound(new { mensaje = "La recepción no existe." });
            if (recepcion.Estado == "Anulada") return Conflict(new { mensaje = "No se puede corregir una recepción anulada." });

            var originales = recepcion.Detalles.ToDictionary(d => d.ProductoId);
            foreach (var item in dto.Items)
            {
                if (!originales.TryGetValue(item.ProductoId, out var original)) return BadRequest(new { mensaje = $"El producto {item.ProductoId} no pertenece a la recepción." });
                var yaCorregido = recepcion.Correcciones.SelectMany(c => c.Detalles).Where(d => d.ProductoId == item.ProductoId).Sum(d => d.Cantidad);
                if (item.Cantidad > original.Cantidad - yaCorregido) return BadRequest(new { mensaje = $"La corrección supera la cantidad pendiente del producto {item.ProductoId}." });
            }

            var productIds = dto.Items.Select(i => i.ProductoId).ToList();
            var stocks = await context.Stock.Where(s => productIds.Contains(s.ProductoId)).ToDictionaryAsync(s => s.ProductoId);
            if (dto.Items.Any(i => !stocks.TryGetValue(i.ProductoId, out var stock) || stock.StockActual < i.Cantidad))
                return Conflict(new { mensaje = "No hay stock suficiente para realizar esta corrección." });

            var correccion = new RecepcionMercaderiaCorreccion { RecepcionMercaderiaId = id, UsuarioId = userId, Motivo = dto.Motivo.Trim(), Fecha = DateTime.Now };
            context.RecepcionesMercaderiaCorrecciones.Add(correccion);
            foreach (var item in dto.Items)
            {
                var stock = stocks[item.ProductoId];
                var anterior = stock.StockActual;
                var nuevo = Math.Round(anterior - item.Cantidad, 3);
                stock.StockActual = nuevo;
                stock.FechaActualizacion = DateTime.Now;
                correccion.Detalles.Add(new RecepcionMercaderiaCorreccionDetalle { ProductoId = item.ProductoId, Cantidad = item.Cantidad });
                var motivoMovimiento = $"Corrección {recepcion.NumeroRecepcion}: {dto.Motivo.Trim()}";
                context.MovimientosStock.Add(new MovimientoStock { ProductoId = item.ProductoId, UsuarioId = userId, TipoMovimiento = "Salida", Cantidad = item.Cantidad, StockAnterior = anterior, StockNuevo = nuevo, Motivo = motivoMovimiento[..Math.Min(200, motivoMovimiento.Length)], FechaMovimiento = DateTime.Now });
            }
            context.RecepcionesMercaderiaEventos.Add(new RecepcionMercaderiaEvento { RecepcionMercaderiaId = id, UsuarioId = userId, Tipo = "CorreccionParcial", Detalle = dto.Motivo.Trim(), Fecha = DateTime.Now });
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { mensaje = $"Corrección registrada para {recepcion.NumeroRecepcion}. El stock fue actualizado." });
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return Conflict(new { mensaje = "El stock cambió mientras se corregía la recepción. Volvé a intentarlo." });
        }
    }

    [HttpPost("{id:int}/anular")]
    public async Task<IActionResult> Anular(int id, [FromBody] AnularRecepcionDto dto)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized(new { mensaje = "El token no identifica un usuario válido." });
        await using var transaction = await context.Database.BeginTransactionAsync();
        try
        {
            var recepcion = await context.RecepcionesMercaderia.Include(r => r.Detalles).Include(r => r.Correcciones).ThenInclude(c => c.Detalles).SingleOrDefaultAsync(r => r.Id == id);
            if (recepcion is null) return NotFound(new { mensaje = "La recepción no existe." });
            if (recepcion.Estado == "Anulada") return Conflict(new { mensaje = "La recepción ya fue anulada." });

            var cantidadesPendientes = recepcion.Detalles.ToDictionary(d => d.ProductoId, d => d.Cantidad - recepcion.Correcciones.SelectMany(c => c.Detalles).Where(cd => cd.ProductoId == d.ProductoId).Sum(cd => cd.Cantidad));
            var productIds = cantidadesPendientes.Where(x => x.Value > 0).Select(x => x.Key).ToList();
            var stocks = await context.Stock.Where(s => productIds.Contains(s.ProductoId)).ToDictionaryAsync(s => s.ProductoId);
            var faltantes = cantidadesPendientes.Where(x => x.Value > 0 && (!stocks.TryGetValue(x.Key, out var stock) || stock.StockActual < x.Value)).ToList();
            if (faltantes.Count > 0)
            {
                await transaction.RollbackAsync();
                return Conflict(new { mensaje = "No se puede anular completamente porque parte de la mercadería ya no está en stock. Realizá una corrección parcial." });
            }

            foreach (var pendiente in cantidadesPendientes.Where(x => x.Value > 0))
            {
                var stock = stocks[pendiente.Key];
                var anterior = stock.StockActual;
                var nuevo = Math.Round(anterior - pendiente.Value, 3);
                stock.StockActual = nuevo;
                stock.FechaActualizacion = DateTime.Now;
                var motivoMovimiento = $"Anulación {recepcion.NumeroRecepcion}: {dto.Motivo.Trim()}";
                context.MovimientosStock.Add(new MovimientoStock { ProductoId = pendiente.Key, UsuarioId = userId, TipoMovimiento = "Salida", Cantidad = pendiente.Value, StockAnterior = anterior, StockNuevo = nuevo, Motivo = motivoMovimiento[..Math.Min(200, motivoMovimiento.Length)], FechaMovimiento = DateTime.Now });
            }

            recepcion.Estado = "Anulada";
            context.RecepcionesMercaderiaEventos.Add(new RecepcionMercaderiaEvento { RecepcionMercaderiaId = id, UsuarioId = userId, Tipo = "Anulada", Detalle = dto.Motivo.Trim(), Fecha = DateTime.Now });
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
            return Ok(new { mensaje = $"Recepción {recepcion.NumeroRecepcion} anulada y stock revertido." });
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            return Conflict(new { mensaje = "El stock cambió mientras se anulaba la recepción. Volvé a intentarlo." });
        }
    }

    private bool TryGetUserId(out int id) => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out id);
    private static string? Normalizar(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
