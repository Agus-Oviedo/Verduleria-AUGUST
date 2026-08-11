using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using VerduleriaAugust.Api.Controllers;
using VerduleriaAugust.Api.DTOs;
using VerduleriaAugust.Api.Models;

namespace VerduleriaAugust.Api.Tests;

public class ProductosControllerTests
{
    [Fact]
    public async Task CrearProducto_ConCodigoVacio_DevuelveBadRequest()
    {
        await using var context = TestDbContextFactory.Create();
        var result = await new ProductosController(context).CrearProducto(
            new CrearProductoDto { Codigo = " ", Nombre = "Manzana" });
        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(context.Productos);
    }

    [Fact]
    public async Task CrearProducto_Valido_CreaProductoYStock()
    {
        await using var context = TestDbContextFactory.Create();
        var categoria = new Categoria { Nombre = "Verduras", Activa = true };
        context.Categorias.Add(categoria);
        var usuario = new Usuario
        {
            NombreUsuario = "admin", NombreUsuarioNormalizado = "ADMIN",
            PasswordHash = "hash", Rol = "Administrador", Activo = true
        };
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();

        var result = await CreateController(context, usuario.Id).CrearProducto(new CrearProductoDto
        {
            Codigo = "TOM-001", Nombre = "Tomate", CategoriaId = categoria.Id,
            TipoVenta = "Peso", PrecioPorKilo = 900m, StockInicial = 15m,
            StockMinimo = 3m, UnidadMedida = "Kg"
        });

        Assert.IsType<CreatedAtActionResult>(result);
        var producto = await context.Productos.Include(p => p.Stock).SingleAsync();
        Assert.Equal("TOM-001", producto.Codigo);
        Assert.Equal(15m, producto.Stock!.StockActual);
        var history = await context.HistorialPrecios.SingleAsync();
        Assert.Equal(900m, history.PrecioPorKiloNuevo);
        Assert.Equal("Precio inicial", history.Motivo);
        Assert.Equal(usuario.Id, history.UsuarioId);
    }

    [Theory]
    [InlineData("Desconocido", 100.0, null)]
    [InlineData("Peso", null, null)]
    [InlineData("Unidad", 100.0, null)]
    public async Task CrearProducto_ConTipoOPrecioInvalido_DevuelveBadRequest(
        string tipoVenta, double? precioKilo, double? precioUnidad)
    {
        await using var context = TestDbContextFactory.Create();
        var categoria = new Categoria { Nombre = "Frutas" };
        context.Categorias.Add(categoria);
        await context.SaveChangesAsync();

        var result = await new ProductosController(context).CrearProducto(new CrearProductoDto
        {
            Codigo = "PER-001", Nombre = "Pera", CategoriaId = categoria.Id,
            TipoVenta = tipoVenta,
            PrecioPorKilo = precioKilo.HasValue ? (decimal)precioKilo.Value : null,
            PrecioPorUnidad = precioUnidad.HasValue ? (decimal)precioUnidad.Value : null,
            UnidadMedida = "Kg"
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(context.Productos);
    }

    [Fact]
    public async Task CrearProducto_ConCodigoDuplicado_DevuelveBadRequest()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context);
        var categoria = await context.Categorias.SingleAsync();

        var result = await new ProductosController(context).CrearProducto(new CrearProductoDto
        {
            Codigo = "MAN-001", Nombre = "Otra manzana", CategoriaId = categoria.Id,
            TipoVenta = "Peso", PrecioPorKilo = 100m, UnidadMedida = "Kg"
        });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Single(context.Productos);
    }

    [Fact]
    public async Task ActualizarProducto_ConCambioPrecio_RegistraValoresYMotivo()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context);
        var producto = await context.Productos.Include(p => p.Stock).SingleAsync();
        var usuario = await context.Usuarios.SingleAsync();

        var result = await CreateController(context, usuario.Id).ActualizarProducto(
            producto.Id,
            new ActualizarProductoDto
            {
                Codigo = producto.Codigo, Nombre = producto.Nombre, CategoriaId = producto.CategoriaId,
                TipoVenta = "Peso", PrecioPorKilo = 1500m, Activo = true,
                UnidadMedida = "Kg",
                StockMinimo = producto.Stock!.StockMinimo,
                MotivoCambioPrecio = "Actualización de costos"
            });

        Assert.IsType<OkObjectResult>(result);
        var history = await context.HistorialPrecios.SingleAsync();
        Assert.Equal(1250.50m, history.PrecioPorKiloAnterior);
        Assert.Equal(1500m, history.PrecioPorKiloNuevo);
        Assert.Equal("Actualización de costos", history.Motivo);
        Assert.Equal(usuario.Id, history.UsuarioId);
    }

    [Fact]
    public async Task ActualizarProducto_CambioPrecioSinMotivo_DevuelveBadRequest()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context);
        var producto = await context.Productos.Include(p => p.Stock).SingleAsync();

        var result = await new ProductosController(context).ActualizarProducto(
            producto.Id,
            new ActualizarProductoDto
            {
                Codigo = producto.Codigo, Nombre = producto.Nombre, CategoriaId = producto.CategoriaId,
                TipoVenta = "Peso", PrecioPorKilo = 1600m, Activo = true,
                UnidadMedida = "Kg"
            });

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(1250.50m, producto.PrecioPorKilo);
        Assert.Empty(context.HistorialPrecios);
    }

    private static ProductosController CreateController(AugustDbContext context, int userId)
    {
        var controller = new ProductosController(context);
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
