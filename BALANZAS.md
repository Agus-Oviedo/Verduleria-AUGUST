# Balanzas y agentes locales

Cada balanza pertenece a una caja. Una caja puede conservar balanzas inactivas
en su historial, pero solamente puede tener una balanza activa a la vez.

## Administración

Estas operaciones requieren rol `Administrador` o `Encargado`:

- `GET /api/Balanzas`: listado paginado; filtros `buscar`, `cajaId` y `activa`.
- `GET /api/Balanzas/{id}`: detalle sin información secreta.
- `POST /api/Balanzas`: registra una balanza y genera la clave del agente.
- `PUT /api/Balanzas/{id}`: modifica datos o realiza la baja lógica.
- `POST /api/Balanzas/{id}/regenerar-clave`: invalida la clave anterior.

La clave (`apiKey`) se entrega solamente al crearla o regenerarla. Debe copiarse
a la configuración segura del agente de esa PC. La base guarda únicamente el
hash SHA-256 y ningún endpoint permite recuperarla posteriormente.

Ejemplo de creación:

```json
{
  "cajaId": 1,
  "marca": "Systel",
  "modelo": "Croma",
  "puertoCom": "COM3",
  "baudRate": 9600,
  "dataBits": 8,
  "paridad": "None",
  "stopBits": "One",
  "numeroSerie": "SER-001",
  "activa": true
}
```

## Base de datos

La tabla `Balanzas` ya formaba parte de AugustDb y conserva `Marca`, `Modelo`,
`PuertoCom`, `BaudRate`, `DataBits`, `Paridad` y `StopBits`. La migración
`AddBalanzas` agrega solamente `ApiKeyHash`, `NumeroSerie`, `UltimaConexion` y
sus índices. No modifica los registros preexistentes.

## Próxima etapa

El agente envía cada lectura mediante:

`POST /api/Balanzas/{balanzaId}/lecturas`

Debe incluir `X-Agent-Key` y un cuerpo con `lecturaId`, `pesoKg`, `estable` y
`fechaLectura`. La API compara el hash en tiempo constante, limita la frecuencia,
rechaza lecturas vencidas o fuera de orden y actualiza `UltimaConexion` como
máximo una vez cada 30 segundos.

Blazor consulta:

`GET /api/Balanzas/caja/{cajaId}/lectura-actual`

La lectura incluye `vigente`. Después de cinco segundos sin mensajes del agente
queda marcada como no vigente y no debe utilizarse para agregar peso a la venta.

Las lecturas son temporales y no se escriben continuamente en SQL Server. Si en
el futuro se ejecutan varias instancias de la API, este almacén deberá trasladarse
a Redis o a otro almacenamiento distribuido.
