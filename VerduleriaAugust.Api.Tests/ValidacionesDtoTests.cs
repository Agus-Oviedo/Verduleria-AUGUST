using System.ComponentModel.DataAnnotations;
using VerduleriaAugust.Api.DTOs;

namespace VerduleriaAugust.Api.Tests;

public class ValidacionesDtoTests
{
    [Fact]
    public void CrearVenta_Invalida_DetectaCamposPrincipales()
    {
        var dto = new CrearVentaDto
        {
            IdempotencyKey = "no-es-guid",
            CajaId = 0,
            Descuento = -1,
            Items = [],
            Pagos = []
        };

        var errors = Validate(dto);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(dto.IdempotencyKey)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(dto.CajaId)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(dto.Descuento)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(dto.Items)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(dto.Pagos)));
    }

    [Fact]
    public void MovimientoStock_SinMotivoOConCantidadInvalida_NoEsValido()
    {
        var dto = new MovimientoStockDto { ProductoId = 0, Cantidad = 0, Motivo = null };

        Assert.True(Validate(dto).Count >= 3);
    }

    [Fact]
    public void GuardarCaja_RespetaLongitudesMaximas()
    {
        var dto = new GuardarCajaDto
        {
            Nombre = new string('A', 101),
            Codigo = new string('B', 51),
            PcIdentificador = new string('C', 101)
        };

        Assert.Equal(3, Validate(dto).Count);
    }

    [Fact]
    public void CrearProducto_ValidaCamposRangosYValoresPermitidos()
    {
        var dto = new CrearProductoDto
        {
            Codigo = "",
            Nombre = "",
            CategoriaId = 0,
            TipoVenta = "Desconocido",
            PrecioPorKilo = -1,
            StockInicial = -1,
            UnidadMedida = "Bolsa",
            StockMinimo = -1
        };

        Assert.True(Validate(dto).Count >= 8);
    }

    [Fact]
    public void CrearUsuario_ExigeRolValidoYPasswordMinimo()
    {
        var dto = new CrearUsuarioDto
        {
            NombreUsuario = "ab",
            Password = "corta",
            Rol = "Agente"
        };

        Assert.Equal(3, Validate(dto).Count);
    }

    [Fact]
    public void FormaPago_ImpideNombreVacioOMayorAlLimite()
    {
        Assert.NotEmpty(Validate(new GuardarFormaPagoDto { Nombre = "" }));
        Assert.NotEmpty(Validate(new GuardarFormaPagoDto { Nombre = new string('X', 101) }));
    }

    private static List<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, true);
        return results;
    }
}
