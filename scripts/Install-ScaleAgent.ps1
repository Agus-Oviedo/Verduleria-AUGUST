param(
    [Parameter(Mandatory = $true)]
    [string]$PublishDirectory,

    [Parameter(Mandatory = $true)]
    [string]$ApiBaseUrl,

    [Parameter(Mandatory = $true)]
    [int]$ScaleId,

    [ValidateSet("Simulator", "Serial")]
    [string]$Mode = "Serial",

    [string]$PortName = "COM1",
    [int]$BaudRate = 9600,
    [string]$ServiceName = "VerduleriaAugust.ScaleAgent"
)

$ErrorActionPreference = "Stop"

if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "Ejecutá este script desde PowerShell como administrador."
}

$publishPath = [System.IO.Path]::GetFullPath($PublishDirectory)
$executable = Join-Path $publishPath "VerduleriaAugust.ScaleAgent.exe"
if (-not (Test-Path -LiteralPath $executable)) {
    throw "No se encontró el ejecutable publicado: $executable"
}
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    throw "El servicio $ServiceName ya existe. Desinstalalo antes de reinstalarlo."
}

$secureApiKey = Read-Host "Pegá la API key de la balanza" -AsSecureString
$bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureApiKey)
try {
    $plainApiKey = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    $clearBytes = [Text.Encoding]::UTF8.GetBytes($plainApiKey)
    $entropy = [Text.Encoding]::UTF8.GetBytes("VerduleriaAugust.ScaleAgent.ApiKey.v1")
    $protectedBytes = [Security.Cryptography.ProtectedData]::Protect(
        $clearBytes,
        $entropy,
        [Security.Cryptography.DataProtectionScope]::LocalMachine)
}
finally {
    if ($clearBytes) { [Security.Cryptography.CryptographicOperations]::ZeroMemory($clearBytes) }
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    $plainApiKey = $null
}

$dataDirectory = Join-Path $env:ProgramData "VerduleriaAugust\ScaleAgent"
$keyFile = Join-Path $dataDirectory "agent.key"
New-Item -ItemType Directory -Path $dataDirectory -Force | Out-Null
[Convert]::ToBase64String($protectedBytes) | Set-Content -LiteralPath $keyFile -NoNewline -Encoding ASCII

$acl = New-Object Security.AccessControl.FileSecurity
$acl.SetAccessRuleProtection($true, $false)
$acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule(
    "SYSTEM", "FullControl", "Allow")))
$acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule(
    "BUILTIN\Administrators", "FullControl", "Allow")))
Set-Acl -LiteralPath $keyFile -AclObject $acl

$settings = @{
    Scale = @{
        Mode = $Mode
        PortName = $PortName
        BaudRate = $BaudRate
        PollIntervalMilliseconds = 500
        ResponseTimeoutMilliseconds = 1000
        InterByteTimeoutMilliseconds = 20
        MaxRetryDelaySeconds = 30
        SimulatorWeightKg = 1.250
        SimulatorStable = $true
    }
    Api = @{
        BaseUrl = $ApiBaseUrl.TrimEnd('/')
        ScaleId = $ScaleId
        ApiKeyProtectedFile = $keyFile
    }
    Logging = @{
        LogLevel = @{
            Default = "Information"
            "Microsoft.Hosting.Lifetime" = "Information"
            "System.Net.Http.HttpClient" = "Warning"
        }
    }
}
$settings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $publishPath "appsettings.json") -Encoding UTF8

$binaryPath = '"{0}"' -f $executable
& sc.exe create $ServiceName binPath= $binaryPath start= auto obj= LocalSystem | Out-Host
& sc.exe description $ServiceName "Lee la balanza local y envía el peso a Verdulería August." | Out-Host
& sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/15000/restart/30000 | Out-Host
& sc.exe start $ServiceName | Out-Host

Write-Host "Servicio instalado e iniciado: $ServiceName"
Write-Host "Clave protegida en: $keyFile"
