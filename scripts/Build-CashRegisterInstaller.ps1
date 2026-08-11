param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$InnoCompiler = ""
)

$ErrorActionPreference = "Stop"
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$payload = Join-Path $root "artifacts\CajaPayload"
$agentOutput = Join-Path $payload "Agent"
$setupOutput = Join-Path $payload "Configurator"
$installerOutput = Join-Path $root "artifacts\Installer"

New-Item -ItemType Directory -Path $agentOutput -Force | Out-Null
New-Item -ItemType Directory -Path $setupOutput -Force | Out-Null
New-Item -ItemType Directory -Path $installerOutput -Force | Out-Null

dotnet publish (Join-Path $root "VerduleriaAugust.ScaleAgent\VerduleriaAugust.ScaleAgent.csproj") `
    --configuration $Configuration --runtime $Runtime --self-contained true --output $agentOutput
if ($LASTEXITCODE -ne 0) { throw "Falló la publicación del agente." }

dotnet publish (Join-Path $root "VerduleriaAugust.ScaleAgent.Setup\VerduleriaAugust.ScaleAgent.Setup.csproj") `
    --configuration $Configuration --runtime $Runtime --self-contained true --output $setupOutput
if ($LASTEXITCODE -ne 0) { throw "Falló la publicación del configurador." }

if ([string]::IsNullOrWhiteSpace($InnoCompiler)) {
    $candidate = Get-Command iscc.exe -ErrorAction SilentlyContinue
    if ($candidate) { $InnoCompiler = $candidate.Source }
    elseif (Test-Path "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") {
        $InnoCompiler = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    }
}

if ([string]::IsNullOrWhiteSpace($InnoCompiler) -or -not (Test-Path -LiteralPath $InnoCompiler)) {
    Write-Host "Publicación lista en $payload"
    Write-Warning "Inno Setup 6 no está instalado; no se generó todavía el instalador .exe."
    exit 2
}

& $InnoCompiler (Join-Path $root "installer\VerduleriaAugust-Caja.iss")
if ($LASTEXITCODE -ne 0) { throw "Falló la construcción del instalador." }

$installer = Join-Path $installerOutput "Augustu-Caja-Setup.exe"
if (-not (Test-Path -LiteralPath $installer)) { throw "No se encontró el instalador esperado." }
$hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
Write-Host "Instalador creado: $installer"
Write-Host "SHA256: $hash"
