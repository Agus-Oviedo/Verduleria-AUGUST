using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VerduleriaAugust.Api.Controllers;
using VerduleriaAugust.Api.DTOs;
using VerduleriaAugust.Api.Models;

namespace VerduleriaAugust.Api.Tests;

public class StockControllerTests
{
    [Fact]
    public async Task SalidaStock_SinCantidadSuficiente_NoModificaStock()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context);
        var producto = await context.Productos.Include(p => p.Stock).SingleAsync();

        var result = await new StockController(context).SalidaStock(
            new MovimientoStockDto { ProductoId = producto.Id, Cantidad = 11m });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(10m, producto.Stock!.StockActual);
        Assert.Empty(context.MovimientosStock);
    }

    [Fact]
    public async Task EntradaStock_Valida_ActualizaStockYRegistraMovimiento()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context);
        var producto = await context.Productos.Include(p => p.Stock).SingleAsync();
        var usuario = await context.Usuarios.SingleAsync();

        var result = await CreateController(context, usuario.Id).EntradaStock(new MovimientoStockDto
        {
            ProductoId = producto.Id, Cantidad = 2.5m, Motivo = "Ingreso de proveedor"
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(12.5m, producto.Stock!.StockActual);
        var movimiento = await context.MovimientosStock.SingleAsync();
        Assert.Equal("Entrada", movimiento.TipoMovimiento);
        Assert.Equal(10m, movimiento.StockAnterior);
        Assert.Equal(12.5m, movimiento.StockNuevo);
        Assert.Equal(usuario.Id, movimiento.UsuarioId);
    }

    [Fact]
    public async Task SalidaStock_Valida_DescuentaYRegistraMovimiento()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context);
        var producto = await context.Productos.Include(p => p.Stock).SingleAsync();
        var usuario = await context.Usuarios.SingleAsync();

        var result = await CreateController(context, usuario.Id).SalidaStock(new MovimientoStockDto
        {
            ProductoId = producto.Id, Cantidad = 3m, Motivo = "Merma"
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(7m, producto.Stock!.StockActual);
        var movimiento = await context.MovimientosStock.SingleAsync();
        Assert.Equal("Salida", movimiento.TipoMovimiento);
        Assert.Equal(3m, movimiento.Cantidad);
        Assert.Equal(7m, movimiento.StockNuevo);
        Assert.Equal(usuario.Id, movimiento.UsuarioId);
    }

    [Fact]
    public async Task AjusteStock_Valido_ReemplazaStockYRegistraDiferencia()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context);
        var producto = await context.Productos.Include(p => p.Stock).SingleAsync();
        var usuario = await context.Usuarios.SingleAsync();

        var result = await CreateController(context, usuario.Id).AjusteStock(new AjusteStockDto
        {
            ProductoId = producto.Id, NuevoStock = 8.25m, Motivo = "Conteo físico"
        });

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(8.25m, producto.Stock!.StockActual);
        var movimiento = await context.MovimientosStock.SingleAsync();
        Assert.Equal("Ajuste", movimiento.TipoMovimiento);
        Assert.Equal(-1.75m, movimiento.Cantidad);
        Assert.Equal(usuario.Id, movimiento.UsuarioId);
    }

    [Fact]
    public async Task AjusteStock_Negativo_DevuelveBadRequest()
    {
        await using var context = TestDbContextFactory.Create();
        var result = await new StockController(context).AjusteStock(
            new AjusteStockDto { ProductoId = 1, NuevoStock = -0.01m });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(context.MovimientosStock);
    }

    [Fact]
    public async Task EntradaStock_ProductoInexistente_DevuelveNotFound()
    {
        await using var context = TestDbContextFactory.Create();
        var result = await new StockController(context).EntradaStock(
            new MovimientoStockDto
            {
                ProductoId = 999, Cantidad = 1m, Motivo = "Producto inexistente"
            });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    private static StockController CreateController(AugustDbContext context, int userId)
    {
        var controller = new StockController(context);
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
}
