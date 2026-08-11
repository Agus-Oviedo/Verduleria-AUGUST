# Configuración de Verdulería August

La cadena de conexión no se guarda en Git. La API busca el valor
`ConnectionStrings:DefaultConnection` y se detiene con un mensaje claro si no
está configurado.

## Desarrollo local

El proyecto utiliza User Secrets de .NET:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=SERVIDOR_LOCAL;Database=AugustDb;Trusted_Connection=True;TrustServerCertificate=True;" --project VerduleriaAugust.Api
```

Los secretos se almacenan en el perfil local de Windows y no se incluyen en el
repositorio.

## Producción

Configurar una variable de entorno en el servidor:

```text
ConnectionStrings__DefaultConnection=Server=SERVIDOR;Database=AugustDb;User Id=USUARIO;Password=CLAVE;Encrypt=True;TrustServerCertificate=False;
```

Los dos guiones bajos representan los dos puntos de la clave de configuración
de .NET. No se debe subir la contraseña a `appsettings.json`.

### Origen del frontend

En producción también se debe indicar el dominio autorizado para CORS. Para un
solo frontend:

```text
Cors__AllowedOrigins__0=https://ventas.ejemplo.com
```

Para más orígenes se agregan índices consecutivos (`__1`, `__2`, etc.). La API
no inicia si la lista queda vacía. Swagger solo se publica en desarrollo.

## Autenticación

La firma de tokens y la creación del primer administrador requieren secretos
distintos. En desarrollo se configuran con User Secrets:

```powershell
dotnet user-secrets set "Jwt:Key" "UNA_CLAVE_ALEATORIA_DE_32_CARACTERES_O_MAS" --project VerduleriaAugust.Api
dotnet user-secrets set "Auth:BootstrapKey" "OTRA_CLAVE_ALEATORIA" --project VerduleriaAugust.Api
```

En producción se usan `Jwt__Key` y `Auth__BootstrapKey`. La clave inicial solo
sirve para `POST /api/auth/bootstrap`, que se deshabilita automáticamente en
cuanto existe el primer usuario. Las contraseñas se almacenan como hash.

## Pruebas SQL Server

Las pruebas de integración usan una variable separada y rechazan bases cuyo
nombre no termine en `_Test`:

```powershell
$env:AUGUST_TEST_CONNECTION_STRING = "Server=SERVIDOR_LOCAL;Database=AugustDb_Test;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet test VerduleriaAugust.Api.sln --filter Category=SqlServerIntegration
```
