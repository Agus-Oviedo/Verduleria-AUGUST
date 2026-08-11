using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VerduleriaAugust.Api.Controllers;
using VerduleriaAugust.Api.DTOs;
using VerduleriaAugust.Api.Models;
using VerduleriaAugust.Api.Services;

namespace VerduleriaAugust.Api.Tests;

public class BalanzasControllerTests
{
    [Fact]
    public async Task Crear_GeneraClaveYGuardaSolamenteElHash()
    {
        await using var context = TestDbContextFactory.Create();
        var caja = new Caja { Nombre = "Caja 1", Codigo = "C1", Activa = true };
        context.Cajas.Add(caja);
        await context.SaveChangesAsync();

        var result = await new BalanzasController(context).Crear(new GuardarBalanzaDto
        {
            CajaId = caja.Id,
            Marca = "Systel",
            Modelo = "Systel Croma",
            NumeroSerie = "SER-001",
            Activa = true
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var response = Assert.IsType<BalanzaCreadaRespuestaDto>(created.Value);
        var stored = await context.Balanzas.SingleAsync();
        Assert.NotEqual(response.ApiKey, stored.ApiKeyHash);
        Assert.Equal(Hash(response.ApiKey), stored.ApiKeyHash);
        Assert.Equal(64, stored.ApiKeyHash!.Length);
    }

    [Fact]
    public async Task Crear_SegundaBalanzaActivaEnMismaCaja_DevuelveConflict()
    {
        await using var context = TestDbContextFactory.Create();
        var caja = new Caja { Nombre = "Caja 1", Codigo = "C1", Activa = true };
        context.Cajas.Add(caja);
        context.Balanzas.Add(new Balanza
        {
            Caja = caja, Marca = "Systel", Modelo = "Croma",
            PuertoCom = "COM1", Paridad = "None", StopBits = "One",
            ApiKeyHash = new string('A', 64), Activa = true
        });
        await context.SaveChangesAsync();

        var result = await new BalanzasController(context).Crear(new GuardarBalanzaDto
        {
            CajaId = caja.Id, Marca = "Systel", Modelo = "Croma", Activa = true
        });

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(context.Balanzas);
    }

    [Fact]
    public async Task Crear_CuandoLaBalanzaAnteriorEstaInactiva_PermiteConservarHistorial()
    {
        await using var context = TestDbContextFactory.Create();
        var caja = new Caja { Nombre = "Caja 1", Codigo = "C1", Activa = true };
        context.Cajas.Add(caja);
        context.Balanzas.Add(new Balanza
        {
            Caja = caja, Marca = "Systel", Modelo = "Anterior",
            PuertoCom = "COM1", Paridad = "None", StopBits = "One",
            ApiKeyHash = new string('A', 64), Activa = false
        });
        await context.SaveChangesAsync();

        var result = await new BalanzasController(context).Crear(new GuardarBalanzaDto
        {
            CajaId = caja.Id, Marca = "Systel", Modelo = "Nueva", Activa = true
        });

        Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(2, await context.Balanzas.CountAsync());
        Assert.Single(await context.Balanzas.Where(b => b.Activa).ToListAsync());
    }

    [Fact]
    public async Task RegenerarClave_InvalidaLaAnterior()
    {
        await using var context = TestDbContextFactory.Create();
        var balanza = new Balanza
        {
            Caja = new Caja { Nombre = "Caja", Codigo = "C1", Activa = true },
            Marca = "Systel", Modelo = "Croma", PuertoCom = "COM1",
            Paridad = "None", StopBits = "One", ApiKeyHash = new string('A', 64), Activa = true
        };
        context.Balanzas.Add(balanza);
        await context.SaveChangesAsync();
        var oldHash = balanza.ApiKeyHash;

        var result = await new BalanzasController(context).RegenerarClave(balanza.Id);

        var response = Assert.IsType<ApiKeyRegeneradaRespuestaDto>(
            Assert.IsType<OkObjectResult>(result).Value);
        Assert.NotEqual(oldHash, balanza.ApiKeyHash);
        Assert.Equal(Hash(response.ApiKey), balanza.ApiKeyHash);
    }

    [Fact]
    public async Task RegistrarLectura_ClaveValida_PublicaPesoParaLaCaja()
    {
        await using var context = TestDbContextFactory.Create();
        const string apiKey = "clave-agente-segura";
        var balanza = CrearBalanza(Hash(apiKey));
        context.Balanzas.Add(balanza);
        await context.SaveChangesAsync();
        var store = new BalanzaLecturaStore();
        var controller = new BalanzasController(context, store);
        var lecturaId = Guid.NewGuid();

        var result = await controller.RegistrarLectura(balanza.Id, apiKey,
            new RegistrarLecturaBalanzaDto
            {
                LecturaId = lecturaId,
                PesoKg = 1.250m,
                Estable = true,
                FechaLectura = DateTimeOffset.UtcNow
            });

        Assert.IsType<AcceptedResult>(result);
        var lectura = store.GetCurrent(balanza.CajaId, DateTimeOffset.UtcNow);
        Assert.NotNull(lectura);
        Assert.Equal(lecturaId, lectura.LecturaId);
        Assert.Equal(1.250m, lectura.PesoKg);
        Assert.True(lectura.Vigente);
        Assert.NotNull(balanza.UltimaConexion);
    }

    [Fact]
    public async Task RegistrarLectura_ClaveIncorrecta_NoPublicaPeso()
    {
        await using var context = TestDbContextFactory.Create();
        var balanza = CrearBalanza(Hash("correcta"));
        context.Balanzas.Add(balanza);
        await context.SaveChangesAsync();
        var store = new BalanzaLecturaStore();

        var result = await new BalanzasController(context, store).RegistrarLectura(
            balanza.Id, "incorrecta", new RegistrarLecturaBalanzaDto
            {
                LecturaId = Guid.NewGuid(), PesoKg = 2,
                Estable = true, FechaLectura = DateTimeOffset.UtcNow
            });

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Null(store.GetCurrent(balanza.CajaId, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Store_RechazaLecturaAnteriorYMarcaLecturaVencida()
    {
        var store = new BalanzaLecturaStore();
        var now = DateTimeOffset.UtcNow;
        var newer = new RegistrarLecturaBalanzaDto
        {
            LecturaId = Guid.NewGuid(), PesoKg = 2,
            Estable = true, FechaLectura = now
        };
        var older = new RegistrarLecturaBalanzaDto
        {
            LecturaId = Guid.NewGuid(), PesoKg = 1,
            Estable = true, FechaLectura = now.AddSeconds(-1)
        };

        Assert.True(store.TrySet(1, 10, newer, now, out _));
        Assert.False(store.TrySet(1, 10, older, now, out _));
        Assert.False(store.GetCurrent(10, now.AddSeconds(6))!.Vigente);
        Assert.Equal(2, store.GetCurrent(10, now)!.PesoKg);
    }

    [Fact]
    public void Store_LecturaUtilizada_NoPuedeReservarseNuevamente()
    {
        var store = new BalanzaLecturaStore();
        var now = DateTimeOffset.UtcNow;
        var dto = new RegistrarLecturaBalanzaDto
        {
            LecturaId = Guid.NewGuid(), PesoKg = 1.250m,
            Estable = true, FechaLectura = now
        };
        store.TrySet(1, 10, dto, now, out _);

        Assert.True(store.TryReserve(dto.LecturaId, 10, now, out var reading));
        Assert.Equal(1.250m, reading!.PesoKg);
        store.Complete(dto.LecturaId);
        Assert.False(store.TryReserve(dto.LecturaId, 10, now, out _));
    }

    private static Balanza CrearBalanza(string hash) => new()
    {
        Caja = new Caja { Nombre = "Caja", Codigo = Guid.NewGuid().ToString(), Activa = true },
        Marca = "Systel", Modelo = "Croma", PuertoCom = "COM1",
        Paridad = "None", StopBits = "One", ApiKeyHash = hash, Activa = true
    };

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
