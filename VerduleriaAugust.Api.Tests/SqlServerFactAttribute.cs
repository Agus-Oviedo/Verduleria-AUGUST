using Microsoft.Data.SqlClient;

namespace VerduleriaAugust.Api.Tests;

/// <summary>
/// Habilita una prueba de integración solamente cuando se configuró una base
/// SQL Server dedicada cuyo nombre termina en _Test.
/// </summary>
internal sealed class SqlServerFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "AUGUST_TEST_CONNECTION_STRING";

    public SqlServerFactAttribute()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvironmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Skip = $"Requiere la variable {EnvironmentVariable}.";
            return;
        }

        try
        {
            var database = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
            if (!database.EndsWith("_Test", StringComparison.OrdinalIgnoreCase))
                Skip = "Por seguridad, la base de pruebas debe terminar en _Test.";
        }
        catch (ArgumentException)
        {
            Skip = $"La variable {EnvironmentVariable} no contiene una cadena válida.";
        }
    }
}
