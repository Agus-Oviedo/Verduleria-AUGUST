using Microsoft.AspNetCore.Mvc;
using VerduleriaAugust.Api.Controllers;
using VerduleriaAugust.Api.DTOs;
using VerduleriaAugust.Api.Models;

namespace VerduleriaAugust.Api.Tests;

public class PaginacionYFiltrosTests
{
    [Fact]
    public async Task Productos_PaginaDos_DevuelveCantidadYTotalCorrectos()
    {
        await using var context = TestDbContextFactory.Create();
        var categoria = new Categoria { Nombre = "Frutas", Activa = true };
        context.Productos.AddRange(Enumerable.Range(1, 5).Select(index => new Producto
        {
            Codigo = $"FRU-{index:000}",
            Nombre = $"Fruta {index}",
            Categoria = categoria,
            TipoVenta = "Peso",
            PrecioPorKilo = 100,
            Activo = true
        }));
        await context.SaveChangesAsync();

        var result = await new ProductosController(context).GetProductos(
            new ProductosConsultaDto { Pagina = 2, TamanoPagina = 2 });

        var response = Paginada(result);
        Assert.Equal(5, response.Total);
        Assert.Equal(2, response.Items.Count);
        Assert.Equal(3, response.TotalPaginas);
    }

    [Fact]
    public async Task Productos_FiltrosCombinados_DevuelvenSoloCoincidencias()
    {
        await using var context = TestDbContextFactory.Create();
        var categoria = new Categoria { Nombre = "Verduras", Activa = true };
        context.Productos.AddRange(
            new Producto { Codigo = "TOM-1", Nombre = "Tomate", Categoria = categoria, TipoVenta = "Peso", Activo = true },
            new Producto { Codigo = "TOM-2", Nombre = "Tomate perita", Categoria = categoria, TipoVenta = "Peso", Activo = false },
            new Producto { Codigo = "PAP-1", Nombre = "Papa", Categoria = categoria, TipoVenta = "Peso", Activo = true });
        await context.SaveChangesAsync();

        var result = await new ProductosController(context).GetProductos(
            new ProductosConsultaDto { Buscar = "Tom", Activo = true });

        var response = Paginada(result);
        Assert.Equal(1, response.Total);
        Assert.Single(response.Items);
    }

    [Fact]
    public async Task Stock_BajoStock_ExcluyeProductosConExistenciaSuficiente()
    {
        await using var context = TestDbContextFactory.Create();
        context.Stock.AddRange(
            CrearStock("BAJO", 2, 3),
            CrearStock("NORMAL", 10, 3));
        await context.SaveChangesAsync();

        var result = await new StockController(context).GetStock(
            new StockConsultaDto { BajoStock = true });

        var response = Paginada(result);
        Assert.Equal(1, response.Total);
        Assert.Single(response.Items);
    }

    [Fact]
    public async Task Movimientos_RangoFechasInvalido_DevuelveBadRequest()
    {
        await using var context = TestDbContextFactory.Create();
        var result = await new StockController(context).GetMovimientos(
            new MovimientosStockConsultaDto
            {
                Desde = new DateTime(2026, 8, 5),
                Hasta = new DateTime(2026, 8, 4)
            });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Categorias_FiltraPorEstadoYPaginaResultados()
    {
        await using var context = TestDbContextFactory.Create();
        context.Categorias.AddRange(
            new Categoria { Nombre = "Frutas", Activa = true },
            new Categoria { Nombre = "Verduras", Activa = true },
            new Categoria { Nombre = "Archivada", Activa = false });
        await context.SaveChangesAsync();

        var result = await new CategoriasController(context).GetCategorias(
            new CategoriasConsultaDto { Activa = true, TamanoPagina = 1 });

        var response = Paginada(result);
        Assert.Equal(2, response.Total);
        Assert.Single(response.Items);
        Assert.Equal(2, response.TotalPaginas);
    }

    [Fact]
    public async Task FormasPago_PorDefectoExcluyeInactivasYFiltraEfectivo()
    {
        await using var context = TestDbContextFactory.Create();
        context.FormasPago.AddRange(
            new FormaPago { Nombre = "Efectivo", Activa = true, EsEfectivo = true },
            new FormaPago { Nombre = "Transferencia", Activa = true, EsEfectivo = false },
            new FormaPago { Nombre = "Efectivo anterior", Activa = false, EsEfectivo = true });
        await context.SaveChangesAsync();

        var result = await new FormasPagoController(context).GetAll(
            new FormasPagoConsultaDto { EsEfectivo = true });

        var response = Paginada(result);
        Assert.Equal(1, response.Total);
        Assert.Single(response.Items);
    }

    private static PaginaPrueba Paginada(IActionResult result)
    {
        var value = Assert.IsType<OkObjectResult>(result).Value;
        Assert.NotNull(value);
        var type = value.GetType();
        var items = Assert.IsAssignableFrom<System.Collections.ICollection>(
            type.GetProperty("Items")!.GetValue(value));
        return new PaginaPrueba(
            items.Count,
            (int)type.GetProperty("Total")!.GetValue(value)!,
            (int)type.GetProperty("TotalPaginas")!.GetValue(value)!);
    }

    private sealed record PaginaPrueba(int CantidadItems, int Total, int TotalPaginas)
    {
        public IReadOnlyList<int> Items => Enumerable.Range(0, CantidadItems).ToList();
    }

    private static Stock CrearStock(string codigo, decimal actual, decimal minimo) => new()
    {
        StockActual = actual,
        StockMinimo = minimo,
        UnidadMedida = "Kg",
        Producto = new Producto
        {
            Codigo = codigo,
            Nombre = codigo,
            TipoVenta = "Peso",
            Activo = true,
            Categoria = new Categoria { Nombre = $"Categoría {codigo}", Activa = true }
        }
    };
}
