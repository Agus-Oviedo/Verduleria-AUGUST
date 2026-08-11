# Paginación y filtros

Los listados paginados reciben `pagina` (desde 1) y `tamanoPagina` (entre 1 y
100). Si no se indican, la API usa la página 1 con 20 elementos.

```json
{
  "items": [],
  "total": 45,
  "pagina": 1,
  "tamanoPagina": 20,
  "totalPaginas": 3
}
```

## Endpoints y filtros

- `GET /api/Productos`: `buscar`, `categoriaId`, `activo`, `tipoVenta`.
- `GET /api/Ventas`: `buscar`, `cajaId`, `estado`, `desde`, `hasta`.
- `GET /api/Stock`: `buscar`, `categoriaId`, `bajoStock`.
- `GET /api/Stock/movimientos`: `buscar`, `productoId`, `tipoMovimiento`, `desde`, `hasta`.
- `GET /api/Cajas`: `buscar`, `activa`, `conSesionAbierta`.
- `GET /api/Categorias`: `buscar`, `activa`.
- `GET /api/FormasPago`: `buscar`, `incluirInactivas`, `esEfectivo`.
- `GET /api/Productos/{id}/historial-precios`: `desde`, `hasta`.

`desde` y `hasta` se envían como fecha y hora ISO 8601. Además de los filtros
indicados, todos estos endpoints aceptan `pagina` y `tamanoPagina`.

Ejemplo:

`/api/Productos?buscar=tomate&activo=true&pagina=1&tamanoPagina=20`
