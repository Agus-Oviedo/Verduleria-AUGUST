# Autenticación y permisos

## Probar desde Swagger

1. Iniciar la API en desarrollo y abrir `/swagger`.
2. Ejecutar `POST /api/Auth/login` con el usuario y la contraseña.
3. Copiar únicamente el valor de `token` de la respuesta.
4. Presionar **Authorize**, pegar el token y confirmar.
5. Swagger enviará automáticamente `Authorization: Bearer <token>` en las
   operaciones protegidas.

No se debe escribir la palabra `Bearer` en el cuadro: Swagger la agrega.

## Roles

- `Administrador`: productos, stock, ventas, estadísticas y usuarios.
- `Cajero`: consultas necesarias y operaciones de venta.

Una solicitud sin token devuelve `401`. Un usuario autenticado que no tenga el
rol requerido recibe `403`.

## Cambiar contraseña

Ejecutar `POST /api/Auth/cambiar-password` con un token válido:

```json
{
  "passwordActual": "CONTRASEÑA_ACTUAL",
  "passwordNueva": "CONTRASEÑA_NUEVA"
}
```

La contraseña nueva debe tener al menos 10 caracteres y ser distinta de la
actual.
