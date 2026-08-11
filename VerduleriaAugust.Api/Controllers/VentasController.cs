using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using VerduleriaAugust.Api.DTOs;
using VerduleriaAugust.Api.Models;
using VerduleriaAugust.Api.Security;
using VerduleriaAugust.Api.Services;

namespace VerduleriaAugust.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = Roles.OperacionVenta)]
public class VentasController : ControllerBase
{
    private readonly AugustDbContext _context;
    private readonly ILogger<VentasController>? _logger;
    private readonly BalanzaLecturaStore _lecturaStore;

    public VentasController(
        AugustDbContext context,
        ILogger<VentasController>? logger = null,
        BalanzaLecturaStore? lecturaStore = null)
    {
        _context = context;
        _logger = logger;
        _lecturaStore = lecturaStore ?? new();
    }

    [HttpGet]
    [ProducesResponseType(typeof(RespuestaPaginada<VentaListadoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVentas([FromQuery] VentasConsultaDto consulta)
    {
        if (consulta.Desde.HasValue && consulta.Hasta.HasValue && consulta.Desde > consulta.Hasta)
            return BadRequest(new { mensaje = "La fecha Desde no puede ser posterior a Hasta." });

        var query = _context.Ventas
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(consulta.Buscar))
        {
            var buscar = consulta.Buscar.Trim();
            query = query.Where(v => v.NumeroVenta.Contains(buscar));
        }

        if (consulta.CajaId.HasValue)
            query = query.Where(v => v.CajaId == consulta.CajaId.Value);

        if (!string.IsNullOrWhiteSpace(consulta.Estado))
            query = query.Where(v => v.Estado == consulta.Estado);

        if (consulta.Desde.HasValue)
            query = query.Where(v => v.FechaVenta >= consulta.Desde.Value);

        if (consulta.Hasta.HasValue)
            query = query.Where(v => v.FechaVenta <= consulta.Hasta.Value);

        var total = await query.CountAsync();
        var ventas = await query
            .OrderByDescending(v => v.FechaVenta)
            .ThenByDescending(v => v.Id)
            .Skip((consulta.Pagina - 1) * consulta.TamanoPagina)
            .Take(consulta.TamanoPagina)
            .Select(v => new VentaListadoDto(
                v.Id, v.NumeroVenta, v.IdempotencyKey, v.CajaId,
                v.Caja != null ? v.Caja.Nombre : null,
                v.FechaVenta, v.Subtotal, v.Descuento, v.Total, v.Estado,
                string.Join(", ", v.Pagos.Select(p => p.FormaPago!.Nombre).Distinct())))
            .ToListAsync();

        return Ok(new RespuestaPaginada<VentaListadoDto>(
            ventas,
            total,
            consulta.Pagina,
            consulta.TamanoPagina));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(VentaRespuestaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVenta(int id)
    {
        var venta = await _context.Ventas
            .AsNoTracking()
            .Include(v => v.Caja)
            .Include(v => v.Detalles)
                .ThenInclude(d => d.Producto)
            .Include(v => v.Pagos)
                .ThenInclude(p => p.FormaPago)
            .Include(v => v.UsuarioDescuento)
            .Include(v => v.Anulacion)
                .ThenInclude(a => a!.Usuario)
            .Where(v => v.Id == id)
            .Select(v => new VentaRespuestaDto(
                v.Id, v.NumeroVenta, v.IdempotencyKey, v.CajaId,
                v.Caja != null ? v.Caja.Nombre : null,
                v.FechaVenta, v.Subtotal, v.Descuento, v.Total, v.Estado,
                v.Detalles.Select(d => new VentaDetalleRespuestaDto(
                    d.Id, d.ProductoId,
                    d.Producto != null ? d.Producto.Nombre : null,
                    d.TipoVenta, d.Cantidad, d.PrecioUnitario, d.TotalLinea)),
                v.Pagos.Select(p => new PagoVentaRespuestaDto(
                    p.Id, p.FormaPagoId,
                    p.FormaPago != null ? p.FormaPago.Nombre : null,
                    p.Importe)),
                v.UsuarioDescuentoId == null || v.MotivoDescuento == null
                    ? null
                    : new DescuentoVentaRespuestaDto(
                        v.MotivoDescuento, v.UsuarioDescuentoId.Value,
                        v.UsuarioDescuento != null ? v.UsuarioDescuento.NombreUsuario : null),
                v.Anulacion == null ? null : new AnulacionVentaRespuestaDto(
                    v.Anulacion.Motivo, v.Anulacion.FechaAnulacion,
                    v.Anulacion.Usuario != null
                        ? v.Anulacion.Usuario.NombreUsuario
                        : null)))
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
    [ProducesResponseType(typeof(VentaCreadaRespuestaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(VentaExistenteRespuestaDto), StatusCodes.Status200OK)]
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

        var idempotencyKey = Guid.Parse(dto.IdempotencyKey).ToString("D");
        var ventaExistente = await _context.Ventas
            .AsNoTracking()
            .SingleOrDefaultAsync(v => v.IdempotencyKey == idempotencyKey);

        if (ventaExistente != null)
            return RespuestaVentaExistente(ventaExistente);

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

        var sesionCaja = await _context.SesionesCaja
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.CajaId == dto.CajaId && s.Estado == "Abierta");

        if (sesionCaja == null)
            return Conflict(new { mensaje = "La caja no tiene una sesión abierta." });

        var currentUser = ControllerContext.HttpContext?.User;
        var currentUserIdValue = currentUser?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? currentUser?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        int? currentUserId = int.TryParse(currentUserIdValue, out var parsedUserId)
            ? parsedUserId
            : null;
        if (currentUser?.IsInRole(Roles.Cajero) == true)
        {
            if (!currentUserId.HasValue || sesionCaja.UsuarioAperturaId != currentUserId.Value)
                return Forbid();
        }

        if (dto.Descuento > 0)
        {
            if (currentUser?.IsInRole(Roles.Administrador) != true &&
                currentUser?.IsInRole(Roles.Encargado) != true)
                return Forbid();

            if (!currentUserId.HasValue)
                return Forbid();

            if (string.IsNullOrWhiteSpace(dto.MotivoDescuento) || dto.MotivoDescuento.Trim().Length < 5)
                return BadRequest(new { mensaje = "Debe indicar un motivo de descuento de al menos 5 caracteres." });
        }

        var lecturasReservadas = new HashSet<Guid>();
        await using var transaction =
            await _context.Database.BeginTransactionAsync();

        try
        {
            var venta = new Venta
            {
                // Valor único temporal hasta que SQL Server asigne el Id.
                NumeroVenta = $"P-{idempotencyKey}",
                IdempotencyKey = idempotencyKey,
                CajaId = dto.CajaId,
                SesionCajaId = sesionCaja.Id,
                FechaVenta = DateTime.Now,
                Subtotal = 0,
                Descuento = dto.Descuento,
                MotivoDescuento = dto.Descuento > 0 ? dto.MotivoDescuento!.Trim() : null,
                UsuarioDescuentoId = dto.Descuento > 0 ? currentUserId : null,
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

            // El identity de SQL Server es atómico entre todas las cajas.
            venta.NumeroVenta = $"V-{venta.FechaVenta:yyyyMMdd}-{venta.Id:0000000000}";
            var numeroVenta = venta.NumeroVenta;

            decimal subtotal = 0;

            foreach (var item in dto.Items)
            {
                if (item.LecturaBalanzaId.HasValue)
                {
                    if (item.TipoVenta != "Peso" ||
                        !_lecturaStore.TryReserve(
                            item.LecturaBalanzaId.Value,
                            dto.CajaId,
                            DateTimeOffset.UtcNow,
                            out var lectura) ||
                        lectura is null)
                    {
                        throw new VentaValidationException(
                            "La lectura de balanza no es válida, está vencida, " +
                            "es inestable o ya fue utilizada.");
                    }

                    lecturasReservadas.Add(item.LecturaBalanzaId.Value);
                    if (lectura.PesoKg != item.Cantidad)
                    {
                        throw new VentaValidationException(
                            "El peso enviado no coincide con la lectura recibida desde la balanza.");
                    }
                }

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
                    UsuarioId = currentUserId,
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
            foreach (var lecturaId in lecturasReservadas)
                _lecturaStore.Complete(lecturaId);

            return CreatedAtAction(
                nameof(GetVenta),
                new { id = venta.Id },
                new VentaCreadaRespuestaDto(
                    "Venta registrada correctamente.", venta.Id,
                    venta.NumeroVenta, venta.Subtotal, venta.Descuento,
                    venta.Total, venta.IdempotencyKey));
        }
        catch (VentaValidationException ex)
        {
            await transaction.RollbackAsync();
            foreach (var lecturaId in lecturasReservadas)
                _lecturaStore.Release(lecturaId);

            return BadRequest(new
            {
                mensaje = ex.Message
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            foreach (var lecturaId in lecturasReservadas)
                _lecturaStore.Release(lecturaId);

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
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            foreach (var lecturaId in lecturasReservadas)
                _lecturaStore.Release(lecturaId);
            _context.ChangeTracker.Clear();

            // Dos solicitudes iguales pueden superar juntas la consulta inicial.
            // El índice único decide cuál se guarda; la otra devuelve la ya creada.
            var duplicada = await _context.Ventas
                .AsNoTracking()
                .SingleOrDefaultAsync(v => v.IdempotencyKey == idempotencyKey);

            if (duplicada != null)
                return RespuestaVentaExistente(duplicada);

            throw;
        }
        catch
        {
            await transaction.RollbackAsync();
            foreach (var lecturaId in lecturasReservadas)
                _lecturaStore.Release(lecturaId);
            throw;
        }
    }

    private static string? ValidarDatosIniciales(CrearVentaDto dto)
    {
        if (dto.CajaId <= 0)
        {
            return "Debe indicar una caja válida.";
        }

        if (!Guid.TryParse(dto.IdempotencyKey, out _))
        {
            return "IdempotencyKey debe ser un GUID válido generado por la caja.";
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

    private static IActionResult RespuestaVentaExistente(Venta venta)
    {
        return new OkObjectResult(new VentaExistenteRespuestaDto(
            "La venta ya había sido registrada.", true, venta.Id,
            venta.NumeroVenta, venta.Subtotal, venta.Descuento,
            venta.Total, venta.IdempotencyKey));
    }

    [HttpPost("{id:int}/anular")]
    [Authorize(Roles = Roles.Administracion)]
    [ProducesResponseType(typeof(VentaAnuladaRespuestaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AnularVenta(int id, [FromBody] AnularVentaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Motivo) || dto.Motivo.Trim().Length < 5)
            return BadRequest(new { mensaje = "El motivo debe tener al menos 5 caracteres." });
        if (dto.Motivo.Trim().Length > 300)
            return BadRequest(new { mensaje = "El motivo no puede superar 300 caracteres." });

        var userIdValue = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
        if (!int.TryParse(userIdValue, out var userId))
            return Unauthorized(new { mensaje = "El token no identifica un usuario válido." });

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var venta = await _context.Ventas
                .Include(v => v.Anulacion)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                        .ThenInclude(p => p!.Stock)
                .SingleOrDefaultAsync(v => v.Id == id);

            if (venta == null)
                return NotFound(new { mensaje = "Venta no encontrada." });
            if (venta.Estado != "Finalizada" || venta.Anulacion != null)
                return Conflict(new { mensaje = "La venta ya está anulada o no puede anularse." });

            foreach (var group in venta.Detalles.GroupBy(d => d.ProductoId))
            {
                var producto = group.First().Producto;
                if (producto?.Stock == null)
                    throw new InvalidOperationException(
                        $"El producto {group.Key} no tiene stock configurado.");

                var cantidad = group.Sum(d => d.Cantidad);
                var stockAnterior = producto.Stock.StockActual;
                producto.Stock.StockActual += cantidad;
                producto.Stock.FechaActualizacion = DateTime.Now;
                var motivoMovimiento = $"Anulación {venta.NumeroVenta}: {dto.Motivo.Trim()}";

                _context.MovimientosStock.Add(new MovimientoStock
                {
                    ProductoId = producto.Id,
                    UsuarioId = userId,
                    // La restricción CK_MovimientosStock_Tipo de la base admite
                    // Entrada, Salida y Ajuste. Una anulación repone mercadería,
                    // por lo que se registra como Entrada y se audita en Motivo.
                    TipoMovimiento = "Entrada",
                    Cantidad = cantidad,
                    StockAnterior = stockAnterior,
                    StockNuevo = producto.Stock.StockActual,
                    Motivo = motivoMovimiento.Length <= 200
                        ? motivoMovimiento
                        : motivoMovimiento[..200],
                    FechaMovimiento = DateTime.Now
                });
            }

            venta.Estado = "Anulada";
            _context.VentaAnulaciones.Add(new VentaAnulacion
            {
                VentaId = venta.Id,
                UsuarioId = userId,
                Motivo = dto.Motivo.Trim(),
                FechaAnulacion = DateTime.Now
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new VentaAnuladaRespuestaDto(
                "Venta anulada y stock repuesto correctamente.",
                venta.Id, venta.NumeroVenta, venta.Estado));
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            return Conflict(new
            {
                mensaje = "El stock cambió mientras se anulaba la venta.",
                codigo = "STOCK_CONCURRENCIA"
            });
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync();
            _context.ChangeTracker.Clear();
            _logger?.LogError(ex, "Error de base de datos al anular la venta {VentaId}.", id);

            var yaAnulada = await _context.Ventas.AsNoTracking()
                .AnyAsync(v => v.Id == id && v.Estado == "Anulada");
            if (yaAnulada)
                return Ok(new VentaAnuladaRespuestaDto(
                    "La venta ya estaba anulada; no se repuso stock nuevamente.",
                    id, string.Empty, "Anulada"));

            return Conflict(new
            {
                mensaje = "La base de datos rechazó la anulación. No se modificó la venta ni el stock.",
                codigo = "ANULACION_DB_ERROR"
            });
        }
    }

    private sealed class VentaValidationException : Exception
    {
        public VentaValidationException(string message)
            : base(message)
        {
        }
    }
}
