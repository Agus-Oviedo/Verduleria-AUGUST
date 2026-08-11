using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VerduleriaAugust.Api.Models;

namespace VerduleriaAugust.Api.Tests;

internal sealed class SqlServerTestDatabase : IAsyncDisposable
{
    private readonly string _connectionString;

    private SqlServerTestDatabase(string connectionString)
    {
        _connectionString = connectionString;
    }

    public static async Task<SqlServerTestDatabase> CreateAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            SqlServerFactAttribute.EnvironmentVariable)!;
        ValidateSafety(connectionString);

        var database = new SqlServerTestDatabase(connectionString);
        await using var context = database.CreateContext();
        await context.Database.EnsureDeletedAsync();
        // InitialBaseline representa una base que ya existía y no contiene DDL.
        // Para una base efímera creamos el esquema directamente desde el modelo.
        await context.Database.EnsureCreatedAsync();
        return database;
    }

    public AugustDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AugustDbContext>()
            .UseSqlServer(_connectionString)
            .Options;
        return new AugustDbContext(options);
    }

    public async Task SeedVentaAsync(bool includeOpenSession = true)
    {
        await using var context = CreateContext();
        var categoria = new Categoria { Nombre = "Frutas", Activa = true };
        var producto = new Producto
        {
            Codigo = "MAN-SQL-001", Nombre = "Manzana SQL", Categoria = categoria,
            TipoVenta = "Peso", PrecioPorKilo = 1000m, Activo = true,
            Stock = new Stock { StockActual = 10m, UnidadMedida = "Kg", StockMinimo = 2m }
        };
        var caja = new Caja { Nombre = "Caja SQL", Codigo = "CAJA-SQL", Activa = true };
        var user = new Usuario
        {
            NombreUsuario = "sql-seed", NombreUsuarioNormalizado = "SQL-SEED",
            PasswordHash = "hash", Rol = "Cajero", Activo = true
        };
        context.AddRange(categoria, producto, caja, user,
            new FormaPago { Nombre = "Efectivo SQL", Activa = true, EsEfectivo = true });
        await context.SaveChangesAsync();
        if (includeOpenSession)
        {
            context.SesionesCaja.Add(new SesionCaja
            {
                CajaId = caja.Id, UsuarioAperturaId = user.Id,
                FechaApertura = DateTime.Now, SaldoInicial = 0m, Estado = "Abierta"
            });
            await context.SaveChangesAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    private static void ValidateSafety(string connectionString)
    {
        var database = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        if (string.IsNullOrWhiteSpace(database) ||
            !database.EndsWith("_Test", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "La base SQL de integración debe tener un nombre terminado en _Test.");
        }
    }
}
