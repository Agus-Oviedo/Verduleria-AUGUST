# Apertura, ventas y cierre de caja

## Flujo

1. El cajero inicia sesión.
2. Consulta `GET /api/Cajas`.
3. Abre su terminal con `POST /api/Cajas/{cajaId}/abrir`.
4. Registra ventas normalmente indicando `cajaId`.
5. Cierra con `POST /api/Cajas/sesiones/{sesionId}/cerrar`.

La API vincula cada venta nueva a la sesión abierta. Una caja cerrada no puede
vender. Un usuario con rol `Cajero` solo puede vender y cerrar en la sesión que
él mismo abrió.

## Apertura

```json
{
  "saldoInicial": 5000.00
}
```

Solo puede existir una sesión abierta por caja, incluso ante solicitudes
simultáneas.

## Cierre y arqueo

```json
{
  "efectivoDeclarado": 25750.00,
  "observaciones": "Arqueo de fin de turno"
}
```

El efectivo esperado es el saldo inicial más los pagos en efectivo de ventas
finalizadas pertenecientes a la sesión. Las ventas anuladas no se suman. La
diferencia se calcula como `declarado - esperado`.

Las ventas anteriores a la incorporación de sesiones conservan
`SesionCajaId = NULL` y no forman parte de arqueos nuevos.
