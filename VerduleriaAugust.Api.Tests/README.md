# Pruebas de Verdulería August

Las pruebas unitarias usan una base en memoria y se ejecutan normalmente:

```powershell
dotnet test VerduleriaAugust.Api.sln
```

Las pruebas de integración crean y eliminan la base indicada en
`AUGUST_TEST_CONNECTION_STRING`. Por seguridad, su nombre debe terminar en
`_Test`; nunca se debe indicar `AugustDb` ni una base de producción.

```powershell
$env:AUGUST_TEST_CONNECTION_STRING = "Server=DESKTOP-4H7JKSR;Database=AugustDb_Test;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet test VerduleriaAugust.Api.sln --filter Category=SqlServerIntegration
```

Estas pruebas verifican el rollback transaccional y la concurrencia optimista
de `Stock.RowVersion` contra SQL Server real.
