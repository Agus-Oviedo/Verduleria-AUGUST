param(
    [string]$Configuration = "Release",
    [string]$OutputDirectory = "$PSScriptRoot\..\artifacts\ScaleAgent"
)

$ErrorActionPreference = "Stop"
$project = Join-Path $PSScriptRoot "..\VerduleriaAugust.ScaleAgent\VerduleriaAugust.ScaleAgent.csproj"

dotnet publish $project `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $OutputDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish falló con código $LASTEXITCODE."
}

Write-Host "Agente publicado en: $([System.IO.Path]::GetFullPath($OutputDirectory))"
