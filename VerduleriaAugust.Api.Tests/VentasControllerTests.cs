using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VerduleriaAugust.Api.Controllers;
using VerduleriaAugust.Api.DTOs;
using Models = VerduleriaAugust.Api.Models;

namespace VerduleriaAugust.Api.Tests;

public class VentasControllerTests
{
    [Fact]
    public async Task CrearVenta_SinItems_DevuelveBadRequest()
    {
        await using var context = TestDbContextFactory.Create();
        var result = await new VentasController(context).CrearVenta(new CrearVentaDto
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            CajaId = 1,
            Pagos = [new CrearPagoVentaDto { FormaPagoId = 1, Importe = 100m }]
        });
        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(context.Ventas);
    }

    [Fact]
    public async Task CrearVenta_Valida_CalculaTotalesYDescuentaStock()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context);
        var producto = await context.Productos.Include(p => p.Stock).SingleAsync();
        var caja = await context.Cajas.SingleAsync();
        var formaPago = await context.FormasPago.SingleAsync();
        var usuario = await context.Usuarios.SingleAsync();
        var controller = new VentasController(context);
        SetAuthenticatedUser(controller, usuario.Id, "Administrador");

        var result = await controller.CrearVenta(new CrearVentaDto
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            CajaId = caja.Id, Descuento = 1m, MotivoDescuento = "Promoción autorizada",
            Items = [new CrearVentaItemDto { ProductoId = producto.Id, TipoVenta = "Peso", Cantidad = 0.333m }],
            Pagos = [new CrearPagoVentaDto { FormaPagoId = formaPago.Id, Importe = 415.42m }]
        });

        Assert.IsType<CreatedAtActionResult>(result);
        var venta = await context.Ventas.SingleAsync();
        Assert.Equal(416.42m, venta.Subtotal);
        Assert.Equal(415.42m, venta.Total);
        Assert.Equal(9.667m, producto.Stock!.StockActual);
        Assert.Single(context.VentaDetalles);
        Assert.Single(context.PagosVenta);
        Assert.Single(context.MovimientosStock);
        Assert.Matches(@"^V-\d{8}-\d{10}$", venta.NumeroVenta);
        Assert.NotNull(venta.SesionCajaId);
        Assert.Equal(usuario.Id, venta.UsuarioDescuentoId);
        Assert.Equal("Promoción autorizada", venta.MotivoDescuento);
    }

    [Fact]
    public async Task CrearVenta_CajeroConDescuento_DevuelveForbid()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context);
        var producto = await context.Productos.SingleAsync();
        var caja = await context.Cajas.SingleAsync();
        var formaPago = await context.FormasPago.SingleAsync();
        var usuario = await context.Usuarios.SingleAsync();
        var controller = new VentasController(context);
        SetAuthenticatedUser(controller, usuario.Id, "Cajero");

        var result = await controller.CrearVenta(new CrearVentaDto
        {
            IdempotencyKey = Guid.NewGuid().ToString(), CajaId = caja.Id,
            Descuento = 50m, MotivoDescuento = "Descuento no autorizado",
            Items = [new CrearVentaItemDto { ProductoId = producto.Id, TipoVenta = "Peso", Cantidad = 1m }],
            Pagos = [new CrearPagoVentaDto { FormaPagoId = formaPago.Id, Importe = 1200.50m }]
        });

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(context.Ventas);
    }

    [Fact]
    public async Task CrearVenta_ConStockInsuficiente_DevuelveBadRequestYSinDescontarStock()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context);
        var producto = await context.Productos.Include(p => p.Stock).SingleAsync();
        var caja = await context.Cajas.SingleAsync();
        var formaPago = await context.FormasPago.SingleAsync();

        var result = await new VentasController(context).CrearVenta(new CrearVentaDto
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            CajaId = caja.Id,
            Items = [new CrearVentaItemDto { ProductoId = producto.Id, TipoVenta = "Peso", Cantidad = 11m }],
            Pagos = [new CrearPagoVentaDto { FormaPagoId = formaPago.Id, Importe = 13755.50m }]
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(10m, producto.Stock!.StockActual);
        Assert.Empty(context.VentaDetalles);
        Assert.Empty(context.MovimientosStock);
    }

    [Fact]
    public async Task CrearVenta_ConDescuentoNegativo_DevuelveBadRequest()
    {
        await using var context = TestDbContextFactory.Create();
        var result = await new VentasController(context).CrearVenta(new CrearVentaDto
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            CajaId = 1, Descuento = -1m,
            Items = [new CrearVentaItemDto { ProductoId = 1, TipoVenta = "Peso", Cantidad = 1m }],
            Pagos = [new CrearPagoVentaDto { FormaPagoId = 1, Importe = 1m }]
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(context.Ventas);
    }

    [Fact]
    public async Task CrearVenta_ConCajaInactiva_DevuelveBadRequest()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context);
        var caja = await context.Cajas.SingleAsync();
        caja.Activa = false;
        await context.SaveChangesAsync();

        var result = await new VentasController(context).CrearVenta(new CrearVentaDto
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            CajaId = caja.Id,
            Items = [new CrearVentaItemDto { ProductoId = 1, TipoVenta = "Peso", Cantidad = 1m }],
            Pagos = [new CrearPagoVentaDto { FormaPagoId = 1, Importe = 1m }]
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(context.Ventas);
    }

    [Fact]
    public async Task CrearVenta_ConProductoInactivo_DevuelveBadRequest()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context);
        var producto = await context.Productos.SingleAsync();
        producto.Activo = false;
        await context.SaveChangesAsync();
        var caja = await context.Cajas.SingleAsync();

        var result = await new VentasController(context).CrearVenta(new CrearVentaDto
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            CajaId = caja.Id,
            Items = [new CrearVentaItemDto { ProductoId = producto.Id, TipoVenta = "Peso", Cantidad = 1m }],
            Pagos = [new CrearPagoVentaDto { FormaPagoId = 1, Importe = 1250.50m }]
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CrearVenta_ConPagosQueNoCoinciden_DevuelveBadRequest()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context);
        var producto = await context.Productos.SingleAsync();
        var caja = await context.Cajas.SingleAsync();
        var formaPago = await context.FormasPago.SingleAsync();

        var result = await new VentasController(context).CrearVenta(new CrearVentaDto
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            CajaId = caja.Id,
            Items = [new CrearVentaItemDto { ProductoId = producto.Id, TipoVenta = "Peso", Cantidad = 1m }],
            Pagos = [new CrearPagoVentaDto { FormaPagoId = formaPago.Id, Importe = 100m }]
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CrearVenta_ConFormaPagoInactiva_DevuelveBadRequest()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context);
        var producto = await context.Productos.SingleAsync();
        var caja = await context.Cajas.SingleAsync();
        var formaPago = await context.FormasPago.SingleAsync();
        formaPago.Activa = false;
        await context.SaveChangesAsync();

        var result = await new VentasController(context).CrearVenta(new CrearVentaDto
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            CajaId = caja.Id,
            Items = [new CrearVentaItemDto { ProductoId = producto.Id, TipoVenta = "Peso", Cantidad = 1m }],
            Pagos = [new CrearPagoVentaDto { FormaPagoId = formaPago.Id, Importe = 1250.50m }]
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task CrearVenta_RepetidaConMismaClave_NoDuplicaVentaNiStock()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context);
        var producto = await context.Productos.Include(p => p.Stock).SingleAsync();
        var caja = await context.Cajas.SingleAsync();
        var formaPago = await context.FormasPago.SingleAsync();
        var dto = new CrearVentaDto
        {
            IdempotencyKey = Guid.NewGuid().ToString(),
            CajaId = caja.Id,
            Items = [new CrearVentaItemDto { ProductoId = producto.Id, TipoVenta = "Peso", Cantidad = 1m }],
            Pagos = [new CrearPagoVentaDto { FormaPagoId = formaPago.Id, Importe = 1250.50m }]
        };
        var controller = new VentasController(context);

        var firstResult = await controller.CrearVenta(dto);
        var repeatedResult = await controller.CrearVenta(dto);

        Assert.IsType<CreatedAtActionResult>(firstResult);
        Assert.IsType<OkObjectResult>(repeatedResult);
        Assert.Single(context.Ventas);
        Assert.Single(context.VentaDetalles);
        Assert.Single(context.PagosVenta);
        Assert.Single(context.MovimientosStock);
        Assert.Equal(9m, producto.Stock!.StockActual);
    }

    [Fact]
    public async Task CrearVenta_SinClaveIdempotenteValida_DevuelveBadRequest()
    {
        await using var context = TestDbContextFactory.Create();
        var result = await new VentasController(context).CrearVenta(new CrearVentaDto
        {
            IdempotencyKey = "no-es-un-guid",
            CajaId = 1,
            Items = [new CrearVentaItemDto { ProductoId = 1, TipoVenta = "Peso", Cantidad = 1m }],
            Pagos = [new CrearPagoVentaDto { FormaPagoId = 1, Importe = 1m }]
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(context.Ventas);
    }

    [Fact]
    public async Task CrearVenta_ConCajaCerrada_DevuelveConflict()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context, includeOpenSession: false);
        var producto = await context.Productos.SingleAsync();
        var caja = await context.Cajas.SingleAsync();
        var formaPago = await context.FormasPago.SingleAsync();

        var result = await new VentasController(context).CrearVenta(new CrearVentaDto
        {
            IdempotencyKey = Guid.NewGuid().ToString(), CajaId = caja.Id,
            Items = [new CrearVentaItemDto { ProductoId = producto.Id, TipoVenta = "Peso", Cantidad = 1m }],
            Pagos = [new CrearPagoVentaDto { FormaPagoId = formaPago.Id, Importe = 1250.50m }]
        });

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Empty(context.Ventas);
    }

    [Fact]
    public async Task CrearVenta_CajeroDistintoAlQueAbrio_DevuelveForbid()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context);
        var producto = await context.Productos.SingleAsync();
        var caja = await context.Cajas.SingleAsync();
        var formaPago = await context.FormasPago.SingleAsync();
        var controller = new VentasController(context);
        SetAuthenticatedUser(controller, 999, "Cajero");

        var result = await controller.CrearVenta(new CrearVentaDto
        {
            IdempotencyKey = Guid.NewGuid().ToString(), CajaId = caja.Id,
            Items = [new CrearVentaItemDto { ProductoId = producto.Id, TipoVenta = "Peso", Cantidad = 1m }],
            Pagos = [new CrearPagoVentaDto { FormaPagoId = formaPago.Id, Importe = 1250.50m }]
        });

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(context.Ventas);
    }

    [Fact]
    public async Task AnularVenta_Finalizada_ReponeStockYRegistraAuditoria()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context);
        var usuario = new Models.Usuario
        {
            NombreUsuario = "admin", NombreUsuarioNormalizado = "ADMIN",
            PasswordHash = "hash", Rol = "Administrador", Activo = true
        };
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();
        var producto = await context.Productos.Include(p => p.Stock).SingleAsync();
        var caja = await context.Cajas.SingleAsync();
        var formaPago = await context.FormasPago.SingleAsync();
        var controller = new VentasController(context);
        var saleResult = await controller.CrearVenta(new CrearVentaDto
        {
            IdempotencyKey = Guid.NewGuid().ToString(), CajaId = caja.Id,
            Items = [new CrearVentaItemDto { ProductoId = producto.Id, TipoVenta = "Peso", Cantidad = 2m }],
            Pagos = [new CrearPagoVentaDto { FormaPagoId = formaPago.Id, Importe = 2501m }]
        });
        var sale = Assert.IsType<CreatedAtActionResult>(saleResult);
        var saleId = (int)sale.RouteValues!["id"]!;
        SetAuthenticatedUser(controller, usuario.Id);

        var result = await controller.AnularVenta(saleId, new AnularVentaDto
        {
            Motivo = "Error de carga del cajero"
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Anulada", (await context.Ventas.SingleAsync()).Estado);
        Assert.Equal(10m, producto.Stock!.StockActual);
        var audit = await context.VentaAnulaciones.SingleAsync();
        Assert.Equal(usuario.Id, audit.UsuarioId);
        Assert.Equal("Error de carga del cajero", audit.Motivo);
        Assert.Equal(2, await context.MovimientosStock.CountAsync());
        Assert.Contains(context.MovimientosStock, m =>
            m.TipoMovimiento == "Entrada" &&
            m.Motivo != null && m.Motivo.StartsWith("Anulación "));
    }

    [Fact]
    public async Task AnularVenta_YaAnulada_DevuelveConflictSinReponerDosVeces()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context);
        var usuario = new Models.Usuario
        {
            NombreUsuario = "admin", NombreUsuarioNormalizado = "ADMIN",
            PasswordHash = "hash", Rol = "Administrador", Activo = true
        };
        context.Usuarios.Add(usuario);
        var venta = new Models.Venta
        {
            NumeroVenta = "V-TEST-1", Caja = await context.Cajas.SingleAsync(),
            Estado = "Anulada", Anulacion = new Models.VentaAnulacion
            {
                Usuario = usuario, Motivo = "Ya anulada", FechaAnulacion = DateTime.Now
            }
        };
        context.Ventas.Add(venta);
        await context.SaveChangesAsync();
        var controller = new VentasController(context);
        SetAuthenticatedUser(controller, usuario.Id);

        var result = await controller.AnularVenta(venta.Id, new AnularVentaDto
        {
            Motivo = "Segundo intento"
        });

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(context.VentaAnulaciones);
        Assert.Empty(context.MovimientosStock);
    }

    private static void SetAuthenticatedUser(
        VentasController controller,
        int userId,
        string role = "Administrador")
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            ],
            "TestAuthentication");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }
}
