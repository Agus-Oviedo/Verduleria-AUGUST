using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VerduleriaAugust.Api.Controllers;
using VerduleriaAugust.Api.DTOs;
using VerduleriaAugust.Api.Models;

namespace VerduleriaAugust.Api.Tests;

public class SqlServerIntegrationTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task VentaCompleta_DescuentaStockRegistraPagoYAnulacionReponeTodo()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await database.SeedVentaAsync();

        int productoId;
        int cajaId;
        int formaPagoId;
        int adminId;
        await using (var arrange = database.CreateContext())
        {
            productoId = await arrange.Productos.Select(p => p.Id).SingleAsync();
            cajaId = await arrange.Cajas.Select(c => c.Id).SingleAsync();
            formaPagoId = await arrange.FormasPago.Select(f => f.Id).SingleAsync();
            var admin = new Usuario
            {
                NombreUsuario = "admin-ciclo",
                NombreUsuarioNormalizado = "ADMIN-CICLO",
                PasswordHash = "hash",
                Rol = "Administrador",
                Activo = true
            };
            arrange.Usuarios.Add(admin);
            await arrange.SaveChangesAsync();
            adminId = admin.Id;
        }

        int ventaId;
        await using (var saleContext = database.CreateContext())
        {
            var result = await new VentasController(saleContext).CrearVenta(new CrearVentaDto
            {
                IdempotencyKey = Guid.NewGuid().ToString(),
                CajaId = cajaId,
                Items = [new CrearVentaItemDto
                {
                    ProductoId = productoId,
                    TipoVenta = "Peso",
                    Cantidad = 2m
                }],
                Pagos = [new CrearPagoVentaDto
                {
                    FormaPagoId = formaPagoId,
                    Importe = 2000m
                }]
            });
            ventaId = (int)Assert.IsType<CreatedAtActionResult>(result).RouteValues!["id"]!;
        }

        await using (var afterSale = database.CreateContext())
        {
            var venta = await afterSale.Ventas
                .Include(v => v.Detalles)
                .Include(v => v.Pagos)
                .SingleAsync(v => v.Id == ventaId);
            Assert.Equal("Finalizada", venta.Estado);
            Assert.Equal(2000m, venta.Total);
            Assert.Single(venta.Detalles);
            Assert.Single(venta.Pagos);
            Assert.Equal(8m, await afterSale.Stock.Select(s => s.StockActual).SingleAsync());
            var salida = Assert.Single(await afterSale.MovimientosStock.ToListAsync());
            Assert.Equal("Salida", salida.TipoMovimiento);
            Assert.Equal(2m, salida.Cantidad);
        }

        await using (var reconciliationContext = database.CreateContext())
        {
            var previewResult = Assert.IsType<OkObjectResult>(await
                CreateAuthenticatedCashController(reconciliationContext, adminId).GetArqueo(
                    await reconciliationContext.SesionesCaja.Select(s => s.Id).SingleAsync()));
            var preview = Assert.IsType<ArqueoCajaDto>(previewResult.Value);
            Assert.Equal(1, preview.CantidadVentas);
            Assert.Equal(2000m, preview.TotalVendido);
            Assert.Equal(2000m, preview.VentasEfectivo);
            Assert.Equal(2000m, preview.EfectivoEsperado);

            var historyResult = Assert.IsType<OkObjectResult>(await
                CreateAuthenticatedCashController(reconciliationContext, adminId).GetTurnos(
                    new TurnosCajaConsultaDto()));
            var history = Assert.IsType<RespuestaPaginada<TurnoCajaListadoDto>>(historyResult.Value);
            var shift = Assert.Single(history.Items);
            Assert.Equal(1, shift.CantidadVentas);
            Assert.Equal(2000m, shift.TotalVendido);
            Assert.Single(shift.FormasPago);

            var statisticsResult = Assert.IsType<OkObjectResult>(await
                new EstadisticasController(reconciliationContext).GetDashboard(
                    new EstadisticasDashboardConsultaDto
                    {
                        Desde = DateTime.Today.AddDays(-1),
                        Hasta = DateTime.Today
                    }));
            var statistics = Assert.IsType<EstadisticasDashboardDto>(statisticsResult.Value);
            Assert.Equal(1, statistics.Resumen.CantidadVentas);
            Assert.Equal(2000m, statistics.Resumen.FacturacionTotal);
            Assert.Single(statistics.VentasPorCaja);
            Assert.Single(statistics.FormasPago);
        }

        await using (var cancellationContext = database.CreateContext())
        {
            var result = await CreateAuthenticatedController(cancellationContext, adminId)
                .AnularVenta(ventaId, new AnularVentaDto
                {
                    Motivo = "Prueba integral de reposición"
                });
            Assert.IsType<OkObjectResult>(result);
        }

        await using var afterCancellation = database.CreateContext();
        Assert.Equal("Anulada", await afterCancellation.Ventas
            .Where(v => v.Id == ventaId).Select(v => v.Estado).SingleAsync());
        Assert.Equal(10m, await afterCancellation.Stock.Select(s => s.StockActual).SingleAsync());
        Assert.Single(await afterCancellation.VentaAnulaciones.Where(a => a.VentaId == ventaId).ToListAsync());
        var movements = await afterCancellation.MovimientosStock.OrderBy(m => m.Id).ToListAsync();
        Assert.Equal(2, movements.Count);
        Assert.Equal("Salida", movements[0].TipoMovimiento);
        Assert.Equal("Entrada", movements[1].TipoMovimiento);
        Assert.StartsWith("Anulación", movements[1].Motivo);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task CircuitoCaja_PagoMixtoDescuentoMovimientosYCierre_CuadraCorrectamente()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await database.SeedVentaAsync();
        int productoId, cajaId, efectivoId, transferenciaId, adminId;
        await using (var arrange = database.CreateContext())
        {
            productoId = await arrange.Productos.Select(p => p.Id).SingleAsync();
            cajaId = await arrange.Cajas.Select(c => c.Id).SingleAsync();
            efectivoId = await arrange.FormasPago.Select(f => f.Id).SingleAsync();
            var transferencia = new FormaPago { Nombre = "Transferencia", Activa = true, EsEfectivo = false };
            var admin = new Usuario { NombreUsuario = "admin-integral", NombreUsuarioNormalizado = "ADMIN-INTEGRAL", PasswordHash = "hash", Rol = "Administrador", Activo = true };
            arrange.AddRange(transferencia, admin); await arrange.SaveChangesAsync();
            transferenciaId = transferencia.Id; adminId = admin.Id;
        }

        await using (var saleContext = database.CreateContext())
        {
            var result = await CreateAuthenticatedController(saleContext, adminId).CrearVenta(new CrearVentaDto
            {
                IdempotencyKey = Guid.NewGuid().ToString(), CajaId = cajaId,
                Descuento = 100m, MotivoDescuento = "Promoción prueba integral",
                Items = [new CrearVentaItemDto { ProductoId = productoId, TipoVenta = "Peso", Cantidad = 2m }],
                Pagos = [new CrearPagoVentaDto { FormaPagoId = efectivoId, Importe = 900m }, new CrearPagoVentaDto { FormaPagoId = transferenciaId, Importe = 1000m }]
            });
            Assert.IsType<CreatedAtActionResult>(result);
        }

        await using (var cashContext = database.CreateContext())
        {
            var sessionId = await cashContext.SesionesCaja.Select(s => s.Id).SingleAsync();
            var controller = CreateAuthenticatedCashController(cashContext, adminId);
            await controller.CrearMovimiento(sessionId, new CrearMovimientoCajaDto { Tipo = "Ingreso", Importe = 300m, Motivo = "Fondo de prueba" });
            await controller.CrearMovimiento(sessionId, new CrearMovimientoCajaDto { Tipo = "Retiro", Importe = 200m, Motivo = "Pago de prueba" });
            var ingresoId = await cashContext.MovimientosCaja.Where(m => m.Tipo == "Ingreso").Select(m => m.Id).SingleAsync();
            await controller.AnularMovimiento(ingresoId, new AnularMovimientoCajaDto { Motivo = "Ingreso cargado por error" });
            var arqueo = Assert.IsType<ArqueoCajaDto>(Assert.IsType<OkObjectResult>(await controller.GetArqueo(sessionId)).Value);
            Assert.Equal(900m, arqueo.VentasEfectivo); Assert.Equal(0m, arqueo.IngresosEfectivo); Assert.Equal(200m, arqueo.RetirosEfectivo); Assert.Equal(700m, arqueo.EfectivoEsperado);
            Assert.IsType<OkObjectResult>(await controller.Cerrar(sessionId, new CerrarCajaDto { EfectivoDeclarado = 700m }));
        }

        await using var assertion = database.CreateContext();
        var venta = await assertion.Ventas.Include(v => v.Pagos).SingleAsync();
        Assert.Equal(1900m, venta.Total); Assert.Equal(adminId, venta.UsuarioDescuentoId); Assert.Equal(2, venta.Pagos.Count);
        Assert.Equal(8m, await assertion.Stock.Select(s => s.StockActual).SingleAsync());
        var sesion = await assertion.SesionesCaja.SingleAsync(); Assert.Equal(700m, sesion.EfectivoEsperado); Assert.Equal(0m, sesion.Diferencia);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task CrearVenta_CuandoFallaElPago_RevierteTodaLaTransaccion()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await database.SeedVentaAsync();

        int productoId;
        int cajaId;
        int formaPagoId;
        await using (var arrange = database.CreateContext())
        {
            productoId = await arrange.Productos.Select(p => p.Id).SingleAsync();
            cajaId = await arrange.Cajas.Select(c => c.Id).SingleAsync();
            formaPagoId = await arrange.FormasPago.Select(f => f.Id).SingleAsync();
        }

        await using (var actionContext = database.CreateContext())
        {
            var result = await new VentasController(actionContext).CrearVenta(new CrearVentaDto
            {
                IdempotencyKey = Guid.NewGuid().ToString(),
                CajaId = cajaId,
                Items = [new CrearVentaItemDto { ProductoId = productoId, TipoVenta = "Peso", Cantidad = 2m }],
                Pagos = [new CrearPagoVentaDto { FormaPagoId = formaPagoId, Importe = 1m }]
            });
            Assert.IsType<BadRequestObjectResult>(result);
        }

        await using var assertContext = database.CreateContext();
        Assert.Empty(await assertContext.Ventas.ToListAsync());
        Assert.Empty(await assertContext.VentaDetalles.ToListAsync());
        Assert.Empty(await assertContext.MovimientosStock.ToListAsync());
        Assert.Equal(10m, await assertContext.Stock.Select(s => s.StockActual).SingleAsync());
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task Stock_ModificadoEnDosContextos_DetectaConcurrencia()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await database.SeedVentaAsync();
        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();

        var firstStock = await firstContext.Stock.SingleAsync();
        var secondStock = await secondContext.Stock.SingleAsync();
        firstStock.StockActual -= 1m;
        secondStock.StockActual -= 2m;

        await firstContext.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => secondContext.SaveChangesAsync());
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task CrearVenta_DosSolicitudesConMismaClave_RegistraUnaSolaVenta()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await database.SeedVentaAsync();
        int productoId;
        int cajaId;
        int formaPagoId;
        await using (var arrange = database.CreateContext())
        {
            productoId = await arrange.Productos.Select(p => p.Id).SingleAsync();
            cajaId = await arrange.Cajas.Select(c => c.Id).SingleAsync();
            formaPagoId = await arrange.FormasPago.Select(f => f.Id).SingleAsync();
        }

        var idempotencyKey = Guid.NewGuid().ToString();
        CrearVentaDto CreateDto() => new()
        {
            IdempotencyKey = idempotencyKey,
            CajaId = cajaId,
            Items = [new CrearVentaItemDto { ProductoId = productoId, TipoVenta = "Peso", Cantidad = 1m }],
            Pagos = [new CrearPagoVentaDto { FormaPagoId = formaPagoId, Importe = 1000m }]
        };

        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var firstTask = new VentasController(firstContext).CrearVenta(CreateDto());
        var secondTask = new VentasController(secondContext).CrearVenta(CreateDto());
        var results = await Task.WhenAll(firstTask, secondTask);

        Assert.Contains(results, result => result is CreatedAtActionResult);
        Assert.Contains(results, result => result is OkObjectResult);
        await using var assertContext = database.CreateContext();
        Assert.Single(await assertContext.Ventas.ToListAsync());
        Assert.Single(await assertContext.VentaDetalles.ToListAsync());
        Assert.Single(await assertContext.MovimientosStock.ToListAsync());
        Assert.Equal(9m, await assertContext.Stock.Select(s => s.StockActual).SingleAsync());
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task AnularVenta_DosSolicitudesSimultaneas_ReponeStockUnaSolaVez()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await database.SeedVentaAsync();
        int ventaId;
        int usuarioId;
        await using (var arrange = database.CreateContext())
        {
            var usuario = new Usuario
            {
                NombreUsuario = "admin", NombreUsuarioNormalizado = "ADMIN",
                PasswordHash = "hash", Rol = "Administrador", Activo = true
            };
            arrange.Usuarios.Add(usuario);
            await arrange.SaveChangesAsync();
            usuarioId = usuario.Id;
            var productoId = await arrange.Productos.Select(p => p.Id).SingleAsync();
            var cajaId = await arrange.Cajas.Select(c => c.Id).SingleAsync();
            var formaPagoId = await arrange.FormasPago.Select(f => f.Id).SingleAsync();
            var sale = await new VentasController(arrange).CrearVenta(new CrearVentaDto
            {
                IdempotencyKey = Guid.NewGuid().ToString(), CajaId = cajaId,
                Items = [new CrearVentaItemDto { ProductoId = productoId, TipoVenta = "Peso", Cantidad = 2m }],
                Pagos = [new CrearPagoVentaDto { FormaPagoId = formaPagoId, Importe = 2000m }]
            });
            ventaId = (int)Assert.IsType<CreatedAtActionResult>(sale).RouteValues!["id"]!;
        }

        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var firstController = CreateAuthenticatedController(firstContext, usuarioId);
        var secondController = CreateAuthenticatedController(secondContext, usuarioId);
        var dto = new AnularVentaDto { Motivo = "Anulación concurrente de prueba" };
        var results = await Task.WhenAll(
            firstController.AnularVenta(ventaId, dto),
            secondController.AnularVenta(ventaId, dto));

        Assert.Contains(results, result => result is OkObjectResult);
        Assert.Contains(results, result => result is ConflictObjectResult);
        await using var assertContext = database.CreateContext();
        Assert.Single(await assertContext.VentaAnulaciones.ToListAsync());
        Assert.Equal("Anulada", await assertContext.Ventas.Select(v => v.Estado).SingleAsync());
        Assert.Equal(10m, await assertContext.Stock.Select(s => s.StockActual).SingleAsync());
        Assert.Single(await assertContext.MovimientosStock
            .Where(m => m.TipoMovimiento == "Entrada" &&
                        m.Motivo != null &&
                        m.Motivo.StartsWith("Anulación")).ToListAsync());
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task AbrirCaja_DosSolicitudesSimultaneas_CreaUnaSolaSesion()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await database.SeedVentaAsync(includeOpenSession: false);
        int userId;
        int cajaId;
        await using (var arrange = database.CreateContext())
        {
            var user = new Usuario
            {
                NombreUsuario = "caja1", NombreUsuarioNormalizado = "CAJA1",
                PasswordHash = "hash", Rol = "Cajero", Activo = true
            };
            arrange.Usuarios.Add(user);
            await arrange.SaveChangesAsync();
            userId = user.Id;
            cajaId = await arrange.Cajas.Select(c => c.Id).SingleAsync();
        }

        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        CajasController CreateController(AugustDbContext context)
        {
            var controller = new CajasController(context);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                        "TestAuthentication"))
                }
            };
            return controller;
        }

        var results = await Task.WhenAll(
            CreateController(firstContext).Abrir(cajaId, new AbrirCajaDto { SaldoInicial = 1000m }),
            CreateController(secondContext).Abrir(cajaId, new AbrirCajaDto { SaldoInicial = 1000m }));

        Assert.Contains(results, result => result is ObjectResult { StatusCode: 201 });
        Assert.Contains(results, result => result is ConflictObjectResult);
        await using var assertContext = database.CreateContext();
        Assert.Single(await assertContext.SesionesCaja.Where(s => s.Estado == "Abierta").ToListAsync());
    }

    [SqlServerFact]
    [Trait("Category", "SqlServerIntegration")]
    public async Task Recepcion_CorreccionYAnulacion_MantienenStockYAuditoriaConsistentes()
    {
        await using var database = await SqlServerTestDatabase.CreateAsync();
        await database.SeedVentaAsync();

        int productoId;
        int usuarioId;
        int proveedorId;
        await using (var arrange = database.CreateContext())
        {
            productoId = await arrange.Productos.Select(p => p.Id).SingleAsync();
            usuarioId = await arrange.Usuarios.Select(u => u.Id).SingleAsync();
            var proveedor = new Proveedor
            {
                Nombre = "Proveedor SQL",
                Activo = true,
                FechaCreacion = DateTime.Now
            };
            arrange.Proveedores.Add(proveedor);
            await arrange.SaveChangesAsync();
            proveedorId = proveedor.Id;
        }

        int recepcionId;
        await using (var receiveContext = database.CreateContext())
        {
            var result = await CreateAuthenticatedGoodsReceiptController(receiveContext, usuarioId)
                .CrearRecepcion(new CrearRecepcionMercaderiaDto
                {
                    ProveedorId = proveedorId,
                    Comprobante = "REMITO-SQL-001",
                    Items =
                    [
                        new CrearRecepcionDetalleDto
                        {
                            ProductoId = productoId,
                            Cantidad = 5m,
                            CostoUnitario = 100m
                        }
                    ]
                });
            var created = Assert.IsType<ObjectResult>(result);
            Assert.Equal(201, created.StatusCode);
            recepcionId = await receiveContext.RecepcionesMercaderia.Select(r => r.Id).SingleAsync();
        }

        await using (var afterReceipt = database.CreateContext())
        {
            Assert.Equal(15m, await afterReceipt.Stock.Select(s => s.StockActual).SingleAsync());
            var recepcion = await afterReceipt.RecepcionesMercaderia.Include(r => r.Detalles).SingleAsync();
            Assert.Equal("Confirmada", recepcion.Estado);
            Assert.Equal(500m, recepcion.TotalCosto);
            Assert.Single(recepcion.Detalles);
            var entrada = Assert.Single(await afterReceipt.MovimientosStock.ToListAsync());
            Assert.Equal("Entrada", entrada.TipoMovimiento);
            Assert.Equal(5m, entrada.Cantidad);
        }

        await using (var correctionContext = database.CreateContext())
        {
            var result = await CreateAuthenticatedGoodsReceiptController(correctionContext, usuarioId)
                .Corregir(recepcionId, new CorregirRecepcionDto
                {
                    Motivo = "Dos unidades no fueron recibidas",
                    Items = [new CorregirRecepcionDetalleDto { ProductoId = productoId, Cantidad = 2m }]
                });
            Assert.IsType<OkObjectResult>(result);
        }

        await using (var afterCorrection = database.CreateContext())
        {
            Assert.Equal(13m, await afterCorrection.Stock.Select(s => s.StockActual).SingleAsync());
            Assert.Single(await afterCorrection.RecepcionesMercaderiaCorrecciones.ToListAsync());
            Assert.Contains(await afterCorrection.MovimientosStock.ToListAsync(),
                m => m.TipoMovimiento == "Salida" && m.Cantidad == 2m);
        }

        await using (var cancellationContext = database.CreateContext())
        {
            var result = await CreateAuthenticatedGoodsReceiptController(cancellationContext, usuarioId)
                .Anular(recepcionId, new AnularRecepcionDto { Motivo = "Anulacion integral de prueba" });
            Assert.IsType<OkObjectResult>(result);
        }

        await using (var finalContext = database.CreateContext())
        {
            Assert.Equal(10m, await finalContext.Stock.Select(s => s.StockActual).SingleAsync());
            Assert.Equal("Anulada", await finalContext.RecepcionesMercaderia.Select(r => r.Estado).SingleAsync());
            Assert.Equal(3, await finalContext.MovimientosStock.CountAsync());
            Assert.Equal(2, await finalContext.RecepcionesMercaderiaEventos.CountAsync());
        }
    }

    private static VentasController CreateAuthenticatedController(
        AugustDbContext context,
        int userId)
    {
        var controller = new VentasController(context);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, "Administrador")
                    ],
                    "TestAuthentication"))
            }
        };
        return controller;
    }

    private static CajasController CreateAuthenticatedCashController(
        AugustDbContext context,
        int userId)
    {
        var controller = new CajasController(context);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, "Administrador")
                    ],
                    "TestAuthentication"))
            }
        };
        return controller;
    }

    private static RecepcionesMercaderiaController CreateAuthenticatedGoodsReceiptController(
        AugustDbContext context,
        int userId)
    {
        var controller = new RecepcionesMercaderiaController(context);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, "Administrador")
                    ],
                    "TestAuthentication"))
            }
        };
        return controller;
    }
}
