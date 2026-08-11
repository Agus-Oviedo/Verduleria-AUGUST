# Auditoría de stock

`StockActual` ya no forma parte de `ActualizarProductoDto`. La edición del
producto puede cambiar unidad de medida y stock mínimo, pero no la existencia.

Toda modificación de cantidad utiliza uno de estos endpoints:

- `POST /api/Stock/entrada`
- `POST /api/Stock/salida`
- `POST /api/Stock/ajuste`

Las operaciones requieren `Administrador` o `Encargado`, un motivo de entre 5
y 200 caracteres y un token que identifique al usuario. El movimiento conserva
cantidad, stock anterior, stock nuevo, fecha, motivo y usuario.

Las ventas y anulaciones también generan movimientos automáticos y registran
al usuario que ejecutó la operación. Los movimientos históricos anteriores a
esta función conservan `UsuarioId = NULL`.
