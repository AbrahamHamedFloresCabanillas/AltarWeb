# Genera los paquetes de despliegue de Altares y Catrinas (F4). Uso:
#   powershell -ExecutionPolicy Bypass -File tools/Publicar/publicar.ps1 [-Salida <carpeta>] [-Runtime win-x86|win-x64]
#
# - win-x86 por defecto: el app pool de MonsterASP corre en 32 bits (verificado en 2026-09; si el panel del
#   sitio indica 64 bits, usar -Runtime win-x64). Con la arquitectura equivocada IIS responde 500.30.
# - Framework-dependent: el hosting ya trae el runtime de ASP.NET Core 10.
# - Ningun appsettings*.json viaja en el paquete (lo excluye cada .csproj): la configuracion y los secretos
#   van en las variables de entorno del sitio (docs/04-despliegue-altares.md, docs/05-despliegue-catrinas.md).
# - La carpeta de salida queda FUERA del repositorio por defecto.
param(
    [string]$Salida = (Join-Path (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))) "publicacion"),
    [ValidateSet("win-x86", "win-x64")][string]$Runtime = "win-x86"
)
$ErrorActionPreference = "Stop"
$raiz = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$commit = (git -C $raiz rev-parse --short HEAD)
$sucio = (git -C $raiz status --porcelain)
if ($sucio) { Write-Warning "Hay cambios sin commit: el paquete no correspondera exactamente a $commit." }

foreach ($app in @("Altares", "Catrinas")) {
    $destino = Join-Path $Salida "$app-$commit-$Runtime"
    if (Test-Path $destino) { Remove-Item -Recurse -Force $destino }
    dotnet publish (Join-Path $raiz "src/$app.Web/$app.Web.csproj") -c Release -r $Runtime --self-contained false -o $destino
    if ($LASTEXITCODE -ne 0) { throw "Fallo el publish de $app" }

    # Verificaciones del paquete: sin configuracion local, con wwwroot y web.config apuntando al ejecutable correcto.
    $configs = Get-ChildItem $destino -Filter "appsettings*.json" -Recurse
    if ($configs) { throw "El paquete de $app contiene $($configs.Name -join ', '): no debe subirse." }
    if (-not (Test-Path (Join-Path $destino "wwwroot"))) { throw "El paquete de $app no tiene wwwroot." }
    $webConfig = Get-Content (Join-Path $destino "web.config") -Raw
    if ($webConfig -notmatch "$app\.Web\.exe") { throw "web.config de $app no apunta a $app.Web.exe" }
    $runtimeConfig = Get-Content (Join-Path $destino "$app.Web.runtimeconfig.json") -Raw
    if ($runtimeConfig -notmatch '"System.GC.Server":\s*false') { throw "$app no quedo con GC de estacion de trabajo." }

    $zip = "$destino.zip"
    if (Test-Path $zip) { Remove-Item -Force $zip }
    Compress-Archive -Path (Join-Path $destino "*") -DestinationPath $zip
    Write-Host "OK $app -> $zip"
}
