# Frontend Blazor

`VerduleriaAugust.Web` es una aplicación Blazor WebAssembly separada. Se ejecuta
en el navegador de cada caja y se comunica únicamente con la API central. El
acceso al puerto serie permanece dentro de `VerduleriaAugust.ScaleAgent`.

## Primera pantalla

La ruta `/` contiene la maqueta interactiva del punto de venta:

- búsqueda y categorías;
- catálogo de productos;
- carrito con cantidades y subtotales;
- estado y peso de la balanza;
- resumen y acción de cobro;
- adaptación básica para pantallas angostas.

Los productos y el usuario son datos visuales temporales. El siguiente paso es
reemplazarlos por autenticación y consultas reales a la API.

## Autenticación

La ruta `/login` utiliza `POST /api/Auth/login`. El JWT se conserva en
`sessionStorage`, por lo que desaparece al cerrar la pestaña, y la pantalla de
caja requiere un usuario autenticado. El botón de salida elimina el token y
regresa al login.

En desarrollo, `wwwroot/appsettings.json` configura la API en
`http://localhost:5266/` y la política CORS permite el origen local de Blazor
`http://localhost:5275`.

## Desarrollo

```powershell
dotnet run --project VerduleriaAugust.Web
```

Perfil HTTP local: `http://localhost:5275`.
