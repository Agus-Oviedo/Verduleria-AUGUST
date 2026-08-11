using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VerduleriaAugust.Api.Controllers;
using VerduleriaAugust.Api.DTOs;
using VerduleriaAugust.Api.Models;

namespace VerduleriaAugust.Api.Tests;

public class ConfiguracionOperativaTests
{
    [Fact]
    public async Task CrearCaja_Valida_NormalizaCodigo()
    {
        await using var context = TestDbContextFactory.Create();
        var result = await new CajasController(context).Crear(new GuardarCajaDto
        {
            Nombre = "Caja secundaria", Codigo = " caja-02 ", PcIdentificador = " PC-02 "
        });

        Assert.IsType<CreatedAtActionResult>(result);
        var caja = await context.Cajas.SingleAsync();
        Assert.Equal("CAJA-02", caja.Codigo);
        Assert.Equal("PC-02", caja.PcIdentificador);
    }

    [Fact]
    public async Task CrearCaja_CodigoDuplicado_DevuelveConflict()
    {
        await using var context = TestDbContextFactory.Create();
        var controller = new CajasController(context);
        var dto = new GuardarCajaDto { Nombre = "Caja", Codigo = "CAJA-01" };
        await controller.Crear(dto);

        var result = await controller.Crear(new GuardarCajaDto
        {
            Nombre = "Otra", Codigo = "caja-01"
        });

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(context.Cajas);
    }

    [Fact]
    public async Task ActualizarCaja_AbiertaComoInactiva_DevuelveConflict()
    {
        await using var context = TestDbContextFactory.Create();
        await TestDbContextFactory.SeedVentaAsync(context);
        var caja = await context.Cajas.SingleAsync();

        var result = await new CajasController(context).Actualizar(caja.Id, new GuardarCajaDto
        {
            Nombre = caja.Nombre, Codigo = caja.Codigo, Activa = false
        });

        Assert.IsType<ConflictObjectResult>(result);
        Assert.True(caja.Activa);
    }

    [Fact]
    public async Task CrearFormaPago_Valida_GuardaTipoEfectivo()
    {
        await using var context = TestDbContextFactory.Create();
        var result = await new FormasPagoController(context).Crear(new GuardarFormaPagoDto
        {
            Nombre = "Efectivo", EsEfectivo = true, Orden = 10
        });

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        var forma = await context.FormasPago.SingleAsync();
        Assert.True(forma.EsEfectivo);
        Assert.Equal(10, forma.Orden);
    }

    [Fact]
    public async Task ActualizarFormaPago_PermiteBajaLogica()
    {
        await using var context = TestDbContextFactory.Create();
        var forma = new FormaPago { Nombre = "Transferencia", Activa = true };
        context.FormasPago.Add(forma);
        await context.SaveChangesAsync();

        var result = await new FormasPagoController(context).Actualizar(forma.Id,
            new GuardarFormaPagoDto
            {
                Nombre = "Transferencia", EsEfectivo = false, Activa = false, Orden = 20
            });

        Assert.IsType<OkObjectResult>(result);
        Assert.False(forma.Activa);
        Assert.Equal(20, forma.Orden);
        Assert.Single(context.FormasPago);
    }
}
