# Agente local de balanza

`VerduleriaAugust.ScaleAgent` es una aplicación independiente que se instala en
cada PC de caja. Puede ejecutarse como consola durante el desarrollo y como
servicio de Windows en producción.

## Modo simulador

El archivo `appsettings.json` usa `Scale:Mode=Simulator` por defecto. El peso y
su estabilidad se controlan con:

```json
{
  "Scale": {
    "Mode": "Simulator",
    "SimulatorWeightKg": 1.250,
    "SimulatorStable": true,
    "PollIntervalMilliseconds": 500,
    "MaxRetryDelaySeconds": 30
  }
}
```

Antes de iniciarlo deben configurarse la URL, el identificador de balanza y la
clave entregada por la API. La clave no debe guardarse en `appsettings.json`:

```powershell
$env:Api__BaseUrl = "https://localhost:7001"
$env:Api__ScaleId = "1"
$env:Api__ApiKey = "CLAVE_ENTREGADA_POR_LA_API"
dotnet run --project VerduleriaAugust.ScaleAgent
```

El agente genera un `lecturaId` diferente en cada ciclo y envía la lectura a
`POST /api/Balanzas/{id}/lecturas` con el encabezado `X-Agent-Key`.

## Desconexiones

Si falla la red, la API o el puerto serie, el agente no termina. Espera 0,5; 1;
2; 4 segundos y continúa aumentando el intervalo hasta un máximo de 30 segundos.
Cuando la comunicación vuelve, restablece automáticamente el ciclo de 500 ms.

La pérdida de conexión se registra una sola vez como advertencia; los reintentos
posteriores quedan en nivel `Debug`. Esto evita llenar el disco o consumir CPU
innecesariamente durante una caída prolongada.

## Modo serial

Cuando haya una balanza disponible se cambia a:

```json
{
  "Scale": {
    "Mode": "Serial",
    "PortName": "COM3",
    "BaudRate": 9600
  }
}
```

El lector implementa la solicitud de peso estable Systel (`0x05`), respuesta
inestable (`0x11`), trama `STX + peso ASCII + ETX + XOR` y reconexión del puerto.
La velocidad y el puerto deberán confirmarse físicamente en la Croma.

## Situación actual

El simulador, el cliente HTTP y el parser serial están cubiertos por pruebas.
La comunicación física RS-232 queda pendiente hasta disponer de la balanza,
el cable o adaptador y conocer el puerto COM asignado por Windows.

## Publicación e instalación en Windows

Publicar una versión autocontenida para Windows x64:

```powershell
.\scripts\Publish-ScaleAgent.ps1
```

Desde PowerShell ejecutado como administrador, instalarla como servicio:

```powershell
.\scripts\Install-ScaleAgent.ps1 `
  -PublishDirectory ".\artifacts\ScaleAgent" `
  -ApiBaseUrl "https://servidor-api" `
  -ScaleId 1 `
  -Mode Simulator
```

El instalador solicita la API key mediante entrada oculta, la cifra con DPAPI
en alcance `LocalMachine` y limita el archivo a `SYSTEM` y administradores. La
clave no forma parte de argumentos, archivos JSON ni registros del instalador.

El servicio se configura con inicio automático y tres reinicios ante fallo. Para
desinstalarlo sin borrar la clave protegida:

```powershell
.\scripts\Uninstall-ScaleAgent.ps1
```

Para eliminar también la clave se debe indicar explícitamente
`-RemoveProtectedKey`.

## Medición Release

La publicación autocontenida Windows x64 fue probada sin una API disponible para
forzar el peor caso de reintentos. Resultados de una ejecución de 12,5 segundos:

- paquete instalado: 76,84 MB;
- paquete ZIP: aproximadamente 34 MB;
- memoria de trabajo estabilizada: 50 MB;
- memoria privada: 13,5 MB;
- CPU acumulada: 0,45 segundos;
- advertencias por desconexión: una.

Este consumo es adecuado para una caja con 8 GB de RAM.
