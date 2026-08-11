using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VerduleriaAugust.Api.Controllers;
using VerduleriaAugust.Api.DTOs;
using VerduleriaAugust.Api.Models;

namespace VerduleriaAugust.Api.Tests;

public class EstadisticasControllerTests
{
    [Fact]
    public async Task Dashboard_FiltraCajaYExcluyeVentasAnuladas()
    {
        await using var context = TestDbContextFactory.Create();
        var categoria = new Categoria { Nombre = "Frutas", Activa = true };
        var producto = new Producto { Codigo = "PER-1", Nombre = "Pera", Categoria = categoria, TipoVenta = "Peso", Activo = true };
        var cajaUno = new Caja { Codigo = "C-1", Nombre = "Caja 1", Activa = true };
        var cajaDos = new Caja { Codigo = "C-2", Nombre = "Caja 2", Activa = true };
        var efectivo = new FormaPago { Nombre = "Efectivo", EsEfectivo = true, Activa = true };
        context.AddRange(categoria, producto, cajaUno, cajaDos, efectivo);
        await context.SaveChangesAsync();
        context.Ventas.AddRange(
            Sale("V-1", cajaUno.Id, "Finalizada", 2000m, producto.Id, efectivo.Id),
            Sale("V-2", cajaUno.Id, "Anulada", 900m, producto.Id, efectivo.Id),
            Sale("V-3", cajaDos.Id, "Finalizada", 5000m, producto.Id, efectivo.Id));
        await context.SaveChangesAsync();

        var result = Assert.IsType<OkObjectResult>(await new EstadisticasController(context)
            .GetDashboard(new EstadisticasDashboardConsultaDto
            {
                Desde = DateTime.Today.AddDays(-1),
                Hasta = DateTime.Today,
                CajaId = cajaUno.Id
            }));
        var dashboard = Assert.IsType<EstadisticasDashboardDto>(result.Value);

        Assert.Equal(1, dashboard.Resumen.CantidadVentas);
        Assert.Equal(2000m, dashboard.Resumen.FacturacionTotal);
        Assert.Equal(1, dashboard.Resumen.VentasAnuladas);
        Assert.Equal(50m, dashboard.Resumen.PorcentajeAnulacion);
        Assert.Equal(2m, dashboard.Resumen.KilosVendidos);
        Assert.Single(dashboard.VentasPorCaja);
        Assert.Equal(cajaUno.Id, dashboard.VentasPorCaja[0].CajaId);
        Assert.Single(dashboard.FormasPago);
        Assert.Single(dashboard.VentasPorCategoria);
        Assert.Single(dashboard.VentasPorHora);
        Assert.Equal(1, dashboard.Alertas.VentasAnuladas);
        Assert.Equal(900m, dashboard.Alertas.ImporteAnulado);
        Assert.Equal(0, dashboard.Alertas.ProductosSinVentas);
    }

    [Fact]
    public async Task Dashboard_PeriodoInvalido_DevuelveBadRequest()
    {
        await using var context = TestDbContextFactory.Create();
        var result = await new EstadisticasController(context).GetDashboard(
            new EstadisticasDashboardConsultaDto
            {
                Desde = DateTime.Today,
                Hasta = DateTime.Today.AddDays(-1)
            });
        Assert.IsType<BadRequestObjectResult>(result);
    }

    private static Venta Sale(string number, int cashRegisterId, string status, decimal total,
        int productId, int paymentMethodId) => new()
    {
        NumeroVenta = number,
        CajaId = cashRegisterId,
        FechaVenta = DateTime.Now,
        Estado = status,
        Subtotal = total,
        Total = total,
        Detalles =
        [
            new VentaDetalle
            {
                ProductoId = productId, TipoVenta = "Peso", Cantidad = 2m,
                PrecioUnitario = total / 2m, TotalLinea = total
            }
        ],
        Pagos = [new PagoVenta { FormaPagoId = paymentMethodId, Importe = total }]
    };
}
