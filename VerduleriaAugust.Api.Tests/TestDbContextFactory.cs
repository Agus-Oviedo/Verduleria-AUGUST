using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using VerduleriaAugust.Api.Models;

namespace VerduleriaAugust.Api.Tests;

internal static class TestDbContextFactory
{
    public static AugustDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AugustDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AugustDbContext(options);
    }

    public static async Task SeedVentaAsync(
        AugustDbContext context,
        bool includeOpenSession = true)
    {
        var categoria = new Categoria { Nombre = "Frutas", Activa = true };
        var producto = new Producto
        {
            Codigo = "MAN-001", Nombre = "Manzana", Categoria = categoria,
            TipoVenta = "Peso", PrecioPorKilo = 1250.50m, Activo = true,
            Stock = new Stock { StockActual = 10m, UnidadMedida = "Kg", StockMinimo = 2m }
        };
        var caja = new Caja { Nombre = "Caja principal", Codigo = "CAJA-01", Activa = true };
        var usuario = new Usuario
        {
            NombreUsuario = "usuario-semilla", NombreUsuarioNormalizado = "USUARIO-SEMILLA",
            PasswordHash = "hash", Rol = "Cajero", Activo = true
        };
        context.AddRange(categoria, producto, caja, usuario,
            new FormaPago { Nombre = "Efectivo", Activa = true, EsEfectivo = true });
        await context.SaveChangesAsync();

        if (includeOpenSession)
        {
            context.SesionesCaja.Add(new SesionCaja
            {
                CajaId = caja.Id, UsuarioAperturaId = usuario.Id,
                FechaApertura = DateTime.Now, SaldoInicial = 0m, Estado = "Abierta"
            });
            await context.SaveChangesAsync();
        }
    }
}
