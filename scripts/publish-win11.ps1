param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root "dist\Lumina"

if (Test-Path $out) {
    Remove-Item -Recurse -Force $out
}

dotnet publish (Join-Path $root "src\Lumina.App\Lumina.App.csproj") `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -o $out

Write-Host "Paquet pret pour le PC Windows 11 dedie :"
Write-Host $out
Write-Host "Copiez tout le dossier sur la machine de show et lancez Lumina.exe"
