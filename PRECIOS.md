# Historial de precios

Los productos nuevos registran una entrada `Precio inicial` asociada al usuario
que realizó el alta. Los productos anteriores a esta función conservan su
precio actual, pero no se inventa un autor para esos datos históricos.

Cuando `PUT /api/Productos/{id}` cambia el precio por kilo o por unidad, debe
incluir:

```json
{
  "motivoCambioPrecio": "Actualización de costos del proveedor"
}
```

El motivo debe tener entre 5 y 300 caracteres. La actualización guarda los
valores anteriores y nuevos, el usuario y la fecha.

El historial se consulta con:

```text
GET /api/Productos/{id}/historial-precios
```

Solo `Administrador` y `Encargado` pueden modificar precios o consultar esta
auditoría. Los importes guardados en detalles de ventas no cambian cuando se
actualiza el precio del producto.
