using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VerduleriaAugust.Api.Controllers;
using VerduleriaAugust.Api.DTOs;
using VerduleriaAugust.Api.Models;

namespace VerduleriaAugust.Api.Tests;

public class CajasControllerTests
{
    [Fact]
    public async Task MovimientoCaja_RetiroSuperiorAlDisponible_DevuelveConflict()
    {
        await using var context = TestDbContextFactory.Create();
        var usuario = await AddUserAsync(context);
        var caja = new Caja { Nombre = "Caja", Codigo = "CAJA-MOV", Activa = true };
        context.Cajas.Add(caja); await context.SaveChangesAsync();
        var controller = CreateController(context, usuario.Id, "Administrador");
        await controller.Abrir(caja.Id, new AbrirCajaDto { SaldoInicial = 1000m });
        var sesion = await context.SesionesCaja.SingleAsync();

        var result = await controller.CrearMovimiento(sesion.Id, new CrearMovimientoCajaDto { Tipo = "Retiro", Importe = 1000.01m, Motivo = "Retiro excesivo" });

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Empty(context.MovimientosCaja);
    }

    [Fact]
    public async Task MovimientoCaja_IngresoYRetiro_ActualizanArqueo()
    {
        await using var context = TestDbContextFactory.Create();
        var usuario = await AddUserAsync(context);
        var caja = new Caja { Nombre = "Caja", Codigo = "CAJA-ARQ", Activa = true };
        context.Cajas.Add(caja); await context.SaveChangesAsync();
        var controller = CreateController(context, usuario.Id, "Administrador");
        await controller.Abrir(caja.Id, new AbrirCajaDto { SaldoInicial = 1000m });
        var sesion = await context.SesionesCaja.SingleAsync();
        await controller.CrearMovimiento(sesion.Id, new CrearMovimientoCajaDto { Tipo = "Ingreso", Importe = 500m, Motivo = "Fondo adicional" });
        await controller.CrearMovimiento(sesion.Id, new CrearMovimientoCajaDto { Tipo = "Retiro", Importe = 200m, Motivo = "Pago proveedor" });

        var result = Assert.IsType<OkObjectResult>(await controller.GetArqueo(sesion.Id));
        var arqueo = Assert.IsType<ArqueoCajaDto>(result.Value);
        Assert.Equal(500m, arqueo.IngresosEfectivo);
        Assert.Equal(200m, arqueo.RetirosEfectivo);
        Assert.Equal(1300m, arqueo.EfectivoEsperado);
        Assert.Equal(2, arqueo.Movimientos.Count);
    }

    [Fact]
    public async Task AnularMovimiento_DejaAuditoriaYLoExcluyeDelArqueo()
    {
        await using var context = TestDbContextFactory.Create();
        var usuario = await AddUserAsync(context);
        var caja = new Caja { Nombre = "Caja", Codigo = "CAJA-ANU", Activa = true };
        context.Cajas.Add(caja); await context.SaveChangesAsync();
        var controller = CreateController(context, usuario.Id, "Administrador");
        await controller.Abrir(caja.Id, new AbrirCajaDto { SaldoInicial = 1000m });
        var sesion = await context.SesionesCaja.SingleAsync();
        await controller.CrearMovimiento(sesion.Id, new CrearMovimientoCajaDto { Tipo = "Ingreso", Importe = 500m, Motivo = "Fondo adicional" });
        var movimiento = await context.MovimientosCaja.SingleAsync();

        Assert.IsType<OkObjectResult>(await controller.AnularMovimiento(movimiento.Id, new AnularMovimientoCajaDto { Motivo = "Importe cargado por error" }));
        var arqueoResult = Assert.IsType<OkObjectResult>(await controller.GetArqueo(sesion.Id));
        var arqueo = Assert.IsType<ArqueoCajaDto>(arqueoResult.Value);
        Assert.Equal(0m, arqueo.IngresosEfectivo);
        Assert.Equal(1000m, arqueo.EfectivoEsperado);
        Assert.True(arqueo.Movimientos.Single().Anulado);
        Assert.Equal(usuario.Id, movimiento.UsuarioAnulacionId);
    }

    [Fact]
    public async Task AbrirCaja_Valida_CreaSesionAuditada()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context, includeOpenSession: false);
        var usuario = await AddUserAsync(context);
        var caja = await context.Cajas.SingleAsync();
        var controller = CreateController(context, usuario.Id, "Cajero");

        var result = await controller.Abrir(caja.Id, new AbrirCajaDto { SaldoInicial = 5000m });

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        var sesion = await context.SesionesCaja.SingleAsync();
        Assert.Equal("Abierta", sesion.Estado);
        Assert.Equal(5000m, sesion.SaldoInicial);
        Assert.Equal(usuario.Id, sesion.UsuarioAperturaId);
    }

    [Fact]
    public async Task AbrirCaja_DosVeces_DevuelveConflict()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context, includeOpenSession: false);
        var usuario = await AddUserAsync(context);
        var caja = await context.Cajas.SingleAsync();
        var controller = CreateController(context, usuario.Id, "Cajero");
        await controller.Abrir(caja.Id, new AbrirCajaDto { SaldoInicial = 1000m });

        var result = await controller.Abrir(caja.Id, new AbrirCajaDto { SaldoInicial = 2000m });

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(context.SesionesCaja);
    }

    [Fact]
    public async Task CerrarCaja_ConVentaEnEfectivo_CalculaArqueoYDiferencia()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context, includeOpenSession: false);
        var usuario = await AddUserAsync(context);
        var caja = await context.Cajas.SingleAsync();
        var controller = CreateController(context, usuario.Id, "Cajero");
        await controller.Abrir(caja.Id, new AbrirCajaDto { SaldoInicial = 5000m });
        var sesion = await context.SesionesCaja.SingleAsync();
        var formaPago = await context.FormasPago.SingleAsync();
        var transferencia = new FormaPago
        {
            Nombre = "Transferencia",
            Activa = true,
            EsEfectivo = false
        };
        context.FormasPago.Add(transferencia);
        context.Ventas.Add(new Venta
        {
            NumeroVenta = "V-TEST-CAJA", CajaId = caja.Id, SesionCajaId = sesion.Id,
            FechaVenta = DateTime.Now, Subtotal = 150m, Total = 150m, Estado = "Finalizada",
            Pagos =
            [
                new PagoVenta { FormaPagoId = formaPago.Id, Importe = 100m },
                new PagoVenta { FormaPago = transferencia, Importe = 50m }
            ]
        });
        await context.SaveChangesAsync();

        var previewResult = Assert.IsType<OkObjectResult>(await controller.GetArqueo(sesion.Id));
        var preview = Assert.IsType<ArqueoCajaDto>(previewResult.Value);
        Assert.Equal(1, preview.CantidadVentas);
        Assert.Equal(150m, preview.TotalVendido);
        Assert.Equal(100m, preview.VentasEfectivo);
        Assert.Equal(5100m, preview.EfectivoEsperado);
        Assert.Equal(2, preview.FormasPago.Count);

        var result = await controller.Cerrar(sesion.Id, new CerrarCajaDto
        {
            EfectivoDeclarado = 5090m,
            Observaciones = "Faltante de cambio"
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Cerrada", sesion.Estado);
        Assert.Equal(5100m, sesion.EfectivoEsperado);
        Assert.Equal(5090m, sesion.EfectivoDeclarado);
        Assert.Equal(-10m, sesion.Diferencia);
        Assert.Equal(usuario.Id, sesion.UsuarioCierreId);
    }

    [Fact]
    public async Task Historial_CajeroVeSoloSusTurnos_YAdministradorPuedeFiltrarTodos()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context, includeOpenSession: false);
        var caja = await context.Cajas.SingleAsync();
        var cajeroUno = await AddUserAsync(context);
        var cajeroDos = new Usuario
        {
            NombreUsuario = "caja2", NombreUsuarioNormalizado = "CAJA2",
            PasswordHash = "hash", Rol = "Cajero", Activo = true
        };
        context.Usuarios.Add(cajeroDos);
        await context.SaveChangesAsync();
        context.SesionesCaja.AddRange(
            new SesionCaja
            {
                CajaId = caja.Id, UsuarioAperturaId = cajeroUno.Id,
                FechaApertura = DateTime.Now.AddHours(-5), FechaCierre = DateTime.Now.AddHours(-3),
                SaldoInicial = 1000m, Estado = "Cerrada", UsuarioCierreId = cajeroUno.Id,
                EfectivoEsperado = 1000m, EfectivoDeclarado = 990m, Diferencia = -10m
            },
            new SesionCaja
            {
                CajaId = caja.Id, UsuarioAperturaId = cajeroDos.Id,
                FechaApertura = DateTime.Now.AddHours(-2), SaldoInicial = 500m, Estado = "Abierta"
            });
        await context.SaveChangesAsync();

        var cashierResult = Assert.IsType<OkObjectResult>(await
            CreateController(context, cajeroUno.Id, "Cajero").GetTurnos(new TurnosCajaConsultaDto()));
        var cashierPage = Assert.IsType<RespuestaPaginada<TurnoCajaListadoDto>>(cashierResult.Value);
        Assert.Single(cashierPage.Items);
        Assert.Equal(cajeroUno.Id, cashierPage.Items[0].UsuarioAperturaId);
        Assert.Equal(-10m, cashierPage.Items[0].Diferencia);

        var adminResult = Assert.IsType<OkObjectResult>(await
            CreateController(context, cajeroUno.Id, "Administrador").GetTurnos(
                new TurnosCajaConsultaDto { Usuario = "caja2" }));
        var adminPage = Assert.IsType<RespuestaPaginada<TurnoCajaListadoDto>>(adminResult.Value);
        Assert.Single(adminPage.Items);
        Assert.Equal(cajeroDos.Id, adminPage.Items[0].UsuarioAperturaId);
    }

    private static async Task<Usuario> AddUserAsync(AugustDbContext context)
    {
        var usuario = new Usuario
        {
            NombreUsuario = "caja1", NombreUsuarioNormalizado = "CAJA1",
            PasswordHash = "hash", Rol = "Cajero", Activo = true
        };
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();
        return usuario;
    }

    private static CajasController CreateController(
        AugustDbContext context,
        int userId,
        string role)
    {
        var controller = new CajasController(context);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                        new Claim(ClaimTypes.Role, role)
                    ],
                    "TestAuthentication"))
            }
        };
        return controller;
    }
}
