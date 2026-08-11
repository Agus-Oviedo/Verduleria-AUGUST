# Integridad de ventas

## Clave de idempotencia

Antes de enviar una venta, Blazor debe generar un GUID y conservarlo hasta
recibir una respuesta definitiva:

```csharp
var idempotencyKey = Guid.NewGuid().ToString();
```

La solicitud incluye esa clave:

```json
{
  "idempotencyKey": "0f0edb13-d9d4-4ef0-81cf-99561574db06",
  "cajaId": 1,
  "descuento": 0,
  "items": [],
  "pagos": []
}
```

Si hay un corte de red o se desconoce el resultado, el frontend debe reenviar
la misma solicitud con la misma clave. No debe generar otra clave para un
reintento. La API devolverá la venta original sin volver a descontar stock.

Una venta distinta siempre utiliza un GUID nuevo.

## Numeración

Los números nuevos tienen el formato `V-AAAAMMDD-ID`, donde `ID` proviene del
identity atómico de SQL Server. Esto garantiza que varias cajas no generen el
mismo número. Los saltos de numeración provocados por transacciones revertidas
son normales y no representan ventas faltantes.

## Anulación

`POST /api/Ventas/{id}/anular` requiere rol `Administrador` o `Encargado` y un
motivo de entre 5 y 300 caracteres:

```json
{
  "motivo": "Error de carga del cajero"
}
```

La operación cambia el estado a `Anulada`, repone el stock, crea movimientos
`AnulacionVenta` y registra usuario, fecha y motivo. Todo ocurre en una sola
transacción. Una venta no puede anularse dos veces.
