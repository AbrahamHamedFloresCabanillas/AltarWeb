# Levanta Altares y Catrinas en esta computadora, con bases locales y datos ficticios (un solo comando).
#
#   powershell -ExecutionPolicy Bypass -File tools/Local/levantar-local.ps1            # levantar todo
#   powershell -ExecutionPolicy Bypass -File tools/Local/levantar-local.ps1 -Detener   # detener las dos apps
#   powershell -ExecutionPolicy Bypass -File tools/Local/levantar-local.ps1 -Reiniciar # borrar bases y empezar de cero
#   (opciones: -SinDatos no carga cuentas/equipos ficticios; -Servidor "<servidor SQL>")
#
# Resultado: Altares en https://localhost:7185 y Catrinas en https://localhost:7186, cada una con SU base
# (AltaresLocal / CatrinasLocal). Las credenciales se generan al azar la primera vez y quedan en
# .local/credenciales-locales.txt (carpeta ignorada por git). Es idempotente: si ya esta todo arriba, solo lo verifica.
#
# Requisitos verificados: Windows, .NET 10 SDK y SQL Server LocalDB (viene con Visual Studio o con
# "SQL Server Express LocalDB"). Para otro SQL Server con autenticacion de Windows usar -Servidor.
# Solo para pruebas locales: NO es la forma de desplegar (ver docs/04 y docs/05).
param(
    [switch]$Detener,
    [switch]$Reiniciar,
    [switch]$SinDatos,
    [string]$Servidor = "(localdb)\mssqllocaldb"
)
$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$local = Join-Path $raiz ".local"
$logs = Join-Path $local "logs"
$procesosIdsArchivo = Join-Path $local "pids.json"
$configArchivo = Join-Path $local "config.json"
$credencialesArchivo = Join-Path $local "credenciales-locales.txt"

$apps = @(
    @{ Nombre = "Altares";  Puerto = 7185; Base = "AltaresLocal";  Clave = "AltarContext";    Proyecto = "src\Altares.Web\Altares.Web.csproj";   Id = "altares" },
    @{ Nombre = "Catrinas"; Puerto = 7186; Base = "CatrinasLocal"; Clave = "CatrinasContext"; Proyecto = "src\Catrinas.Web\Catrinas.Web.csproj"; Id = "catrinas" }
)

# El certificado de desarrollo de .NET no es de confianza por defecto: solo se acepta para localhost.
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
if ($PSVersionTable.PSVersion.Major -lt 6) {
    Add-Type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class ConfiarEnLocalhost : ICertificatePolicy {
    public bool CheckValidationResult(ServicePoint sp, X509Certificate c, WebRequest r, int problema) { return true; }
}
"@
    [System.Net.ServicePointManager]::CertificatePolicy = New-Object ConfiarEnLocalhost
}

function Escribir($mensaje) { Write-Host "[local] $mensaje" }

function Invocar-Sql([string]$sql, [string]$base = "master") {
    $conexion = New-Object System.Data.SqlClient.SqlConnection "Server=$Servidor;Database=$base;Trusted_Connection=True;Connect Timeout=30"
    try {
        $conexion.Open()
        $comando = $conexion.CreateCommand()
        $comando.CommandText = $sql
        $comando.CommandTimeout = 60
        return $comando.ExecuteScalar()
    } finally { $conexion.Dispose() }
}

function Detener-Apps {
    if (-not (Test-Path $procesosIdsArchivo)) { return }
    $guardados = Get-Content $procesosIdsArchivo -Raw | ConvertFrom-Json
    foreach ($propiedad in $guardados.PSObject.Properties) {
        $procesoId = [int]$propiedad.Value
        if (Get-Process -Id $procesoId -ErrorAction SilentlyContinue) {
            # /T incluye al proceso hijo que "dotnet run" crea para la aplicacion.
            & taskkill /PID $procesoId /T /F | Out-Null
            Escribir "$($propiedad.Name) detenida."
        }
    }
    Remove-Item $procesosIdsArchivo -Force
}

function Probar-Sitio([int]$puerto) {
    try {
        if ($PSVersionTable.PSVersion.Major -ge 6) { $r = Invoke-WebRequest -Uri "https://localhost:$puerto/" -TimeoutSec 10 -SkipCertificateCheck }
        else { $r = Invoke-WebRequest -Uri "https://localhost:$puerto/" -UseBasicParsing -TimeoutSec 10 }
        return ($r.StatusCode -eq 200)
    } catch { return $false }
}

function Nueva-Contrasena([int]$longitud) {
    $alfabeto = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789".ToCharArray()
    $bytes = New-Object byte[] $longitud
    $rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider
    $rng.GetBytes($bytes)
    return -join ($bytes | ForEach-Object { $alfabeto[$_ % $alfabeto.Length] })
}

New-Item -ItemType Directory -Force -Path $local, $logs | Out-Null

if ($Detener) { Detener-Apps; return }

# --- Requisitos -------------------------------------------------------------------------------------------------
if (-not ((& dotnet --list-sdks) -match "^10\.")) {
    throw "Falta el SDK de .NET 10 (https://dotnet.microsoft.com/download/dotnet/10.0). SDKs instalados: $((& dotnet --list-sdks) -join '; ')"
}
try { Invocar-Sql "SELECT 1" | Out-Null }
catch { throw "No se pudo conectar a SQL Server en '$Servidor'. En Windows instala SQL Server LocalDB (o Visual Studio con desarrollo web) y comprueba con: sqllocaldb info. Detalle: $($_.Exception.Message)" }

# --- Reinicio o limpieza previa ---------------------------------------------------------------------------------------
Detener-Apps
if ($Reiniciar) {
    foreach ($app in $apps) {
        Invocar-Sql "IF DB_ID('$($app.Base)') IS NOT NULL BEGIN ALTER DATABASE [$($app.Base)] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$($app.Base)]; END" | Out-Null
        Remove-Item (Join-Path $local "datos-$($app.Id).ok") -Force -ErrorAction SilentlyContinue
    }
    Remove-Item $configArchivo, $credencialesArchivo -Force -ErrorAction SilentlyContinue
    Escribir "Bases locales borradas."
}

# --- Credenciales (una sola vez; nunca se versionan) -------------------------------------------------------------------
if (-not (Test-Path $configArchivo)) {
    foreach ($app in $apps) {
        if ((Invocar-Sql "SELECT DB_ID('$($app.Base)')") -isnot [DBNull]) {
            throw "Existe la base $($app.Base) pero no .local/config.json (se generaron otras credenciales). Ejecuta con -Reiniciar para empezar de cero."
        }
    }
    $config = [ordered]@{ AdminUsuario = "admin-local"; AdminPassword = (Nueva-Contrasena 16); CuentasPassword = (Nueva-Contrasena 14) }
    $config | ConvertTo-Json | Set-Content $configArchivo -Encoding UTF8
}
$config = Get-Content $configArchivo -Raw | ConvertFrom-Json

# --- Compilar ------------------------------------------------------------------------------------------------------------
foreach ($app in $apps) {
    Escribir "Compilando $($app.Nombre)..."
    & dotnet build (Join-Path $raiz $app.Proyecto) -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "Fallo la compilacion de $($app.Nombre)" }
}
Escribir "Compilando la herramienta de datos..."
& dotnet build (Join-Path $raiz "tools\PruebaCarga\PruebaCarga.csproj") -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Fallo la compilacion de tools/PruebaCarga" }

# --- Arrancar -------------------------------------------------------------------------------------------------------------
# Variables de entorno de cada app: se escriben en .local/run-<app>.cmd (abajo). Marcadores de Google: sin
# ClientId/ClientSecret la app no arranca, pero "Continuar con Google" no funciona en local.

$procesosIds = [ordered]@{}
foreach ($app in $apps) {
    if (Probar-Sitio $app.Puerto) { throw "El puerto $($app.Puerto) ya esta en uso por otro programa (no es una instancia de este script)." }
    # Cada app se lanza DESACOPLADA (WMI) desde un .cmd generado en .local/ (ignorado por git, con las variables de
    # entorno de esa app). Asi no hereda las tuberias de quien ejecuta el script (un agente o terminal no se queda
    # esperando) y las contrasenas no aparecen en la linea de comandos de ningun proceso.
    $cmdArchivo = Join-Path $local "run-$($app.Id).cmd"
    $lineas = @(
        "@echo off",
        "set ASPNETCORE_ENVIRONMENT=Development",
        "set SEED_ADMIN_USUARIO=$($config.AdminUsuario)",
        "set SEED_ADMIN_PASSWORD=$($config.AdminPassword)",
        "set GoogleAuth__ClientId=no-real-local.apps.googleusercontent.com",
        "set GoogleAuth__ClientSecret=no-real-local",
        "set Logging__LogLevel__Microsoft.AspNetCore=Warning",
        "set Logging__LogLevel__Microsoft.EntityFrameworkCore=Warning",
        "set ConnectionStrings__$($app.Clave)=Server=$Servidor;Database=$($app.Base);Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true",
        "cd /d `"$raiz`"",
        "dotnet run --project `"$(Join-Path $raiz $app.Proyecto)`" -c Release --no-build --no-launch-profile --urls https://localhost:$($app.Puerto)"
    )
    Set-Content $cmdArchivo $lineas -Encoding ASCII
    $lanzado = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{
        CommandLine = "cmd.exe /c `"`"$cmdArchivo`" > `"$(Join-Path $logs "$($app.Id).log")`" 2> `"$(Join-Path $logs "$($app.Id).err.log")`"`""
        CurrentDirectory = $raiz
    }
    if ($lanzado.ReturnValue -ne 0) { throw "No se pudo lanzar $($app.Nombre) (codigo WMI $($lanzado.ReturnValue))" }
    $procesosIds[$app.Nombre] = [int]$lanzado.ProcessId
    Escribir "$($app.Nombre) arrancando (pid $($lanzado.ProcessId))..."
}
$procesosIds | ConvertTo-Json | Set-Content $procesosIdsArchivo -Encoding UTF8

foreach ($app in $apps) {
    $limite = (Get-Date).AddSeconds(240)
    while (-not (Probar-Sitio $app.Puerto)) {
        if ((Get-Date) -gt $limite -or -not (Get-Process -Id $procesosIds[$app.Nombre] -ErrorAction SilentlyContinue)) {
            $cola = (Get-Content (Join-Path $logs "$($app.Id).err.log") -Tail 20 -ErrorAction SilentlyContinue) -join "`n"
            throw "$($app.Nombre) no respondio en https://localhost:$($app.Puerto). Revisa .local/logs/$($app.Id).log y .err.log.`n$cola"
        }
        Start-Sleep -Seconds 3
    }
    Escribir "$($app.Nombre) lista en https://localhost:$($app.Puerto)"
}

# --- Datos ficticios --------------------------------------------------------------------------------------------------------
if (-not $SinDatos) {
    $herramienta = Join-Path $raiz "tools\PruebaCarga\bin\Release\net10.0\PruebaCarga.exe"
    $env:PRUEBA_ADMIN_USUARIO = $config.AdminUsuario
    $env:PRUEBA_ADMIN_PASSWORD = $config.AdminPassword
    $env:PRUEBA_CARGA_PASSWORD = $config.CuentasPassword
    foreach ($app in $apps) {
        $marca = Join-Path $local "datos-$($app.Id).ok"
        if (Test-Path $marca) { Escribir "$($app.Nombre): datos ficticios ya cargados."; continue }
        Escribir "$($app.Nombre): cargando cuentas y equipos ficticios..."
        & $herramienta --url "https://localhost:$($app.Puerto)" --app $app.Id --escenario preparar --equipos 5 --jueces 3 --reservados-pdf 1 --certificado-local | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "No se pudieron cargar los datos de $($app.Nombre) (codigo $LASTEXITCODE). Prueba con -Reiniciar." }
        Set-Content $marca (Get-Date -Format "s")
    }
}

# --- Resumen -----------------------------------------------------------------------------------------------------------------
$resumen = @"
CREDENCIALES SOLO PARA PRUEBAS LOCALES (datos ficticios). No subir a git ni compartir. Generadas por tools/Local/levantar-local.ps1.

Altares  : https://localhost:7185      Catrinas : https://localhost:7186

Administrador (ambas apps)  usuario: $($config.AdminUsuario)      contrasena: $($config.AdminPassword)
Jueces / jurado (ambas)     usuarios: carga-juez-1, carga-juez-2, carga-juez-3     contrasena: $($config.CuentasPassword)
Participantes (ambas)       carga.a1@uabc.edu.mx ... carga.a5@uabc.edu.mx (responsables), carga.b1..b5 (integrantes),
                            carga.m1..m5 (maestros, solo Altares)                  contrasena: $($config.CuentasPassword)

Detalle de cuentas y pruebas guiadas: docs/10-manual-pruebas-locales.md
"@
Set-Content $credencialesArchivo $resumen -Encoding UTF8
Write-Host ""
Write-Host "Todo arriba. Altares: https://localhost:7185 | Catrinas: https://localhost:7186"
Write-Host "Credenciales: $credencialesArchivo"
Write-Host "Detener: powershell -ExecutionPolicy Bypass -File tools/Local/levantar-local.ps1 -Detener"
