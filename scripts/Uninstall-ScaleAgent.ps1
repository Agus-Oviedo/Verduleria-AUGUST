param(
    [string]$ServiceName = "VerduleriaAugust.ScaleAgent",
    [switch]$RemoveProtectedKey
)

$ErrorActionPreference = "Stop"
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -ne "Stopped") {
        & sc.exe stop $ServiceName | Out-Host
        $service.WaitForStatus("Stopped", [TimeSpan]::FromSeconds(30))
    }
    & sc.exe delete $ServiceName | Out-Host
}

if ($RemoveProtectedKey) {
    $dataDirectory = Join-Path $env:ProgramData "VerduleriaAugust\ScaleAgent"
    $keyFile = Join-Path $dataDirectory "agent.key"
    if (Test-Path -LiteralPath $keyFile) {
        Remove-Item -LiteralPath $keyFile
        Write-Host "Clave protegida eliminada: $keyFile"
    }
}

Write-Host "Servicio desinstalado: $ServiceName"
