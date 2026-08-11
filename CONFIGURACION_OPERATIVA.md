# Configuración operativa

## Cajas

- `GET /api/Cajas`: lista terminales y su sesión abierta.
- `GET /api/Cajas/{id}`: obtiene una caja.
- `POST /api/Cajas`: crea una caja.
- `PUT /api/Cajas/{id}`: actualiza o desactiva una caja.

El código se normaliza a mayúsculas y debe ser único. Una caja abierta no
puede desactivarse; primero debe cerrarse mediante el flujo de arqueo.

## Formas de pago

- `GET /api/FormasPago`: lista formas activas.
- `GET /api/FormasPago?incluirInactivas=true`: incluye el historial inactivo.
- `POST /api/FormasPago`: crea una forma.
- `PUT /api/FormasPago/{id}`: actualiza o realiza la baja lógica.

`EsEfectivo` indica si los pagos participan en el arqueo físico de caja. Una
transferencia o tarjeta debe tenerlo en `false`. Los nombres son únicos.

Las operaciones de escritura requieren rol `Administrador` o `Encargado`.
Los cajeros solo tienen acceso de lectura.
