@echo off
setlocal EnableExtensions
cd /d "%~dp0.."

set "OUT=%CD%\dist\Lumina"
if exist "%OUT%" rmdir /s /q "%OUT%"
mkdir "%OUT%" 2>nul

dotnet publish "src\Lumina.App\Lumina.App.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:DebugType=None ^
  -o "%OUT%"
if errorlevel 1 exit /b 1

copy /y "%~dp0Lancer-Lumina.cmd" "%OUT%\Lancer Lumina.cmd" >nul
copy /y "%~dp0LIRE-MOI-PC-SHOW.txt" "%OUT%\LIRE-MOI.txt" >nul

echo.
echo Paquet pret : %OUT%
echo Sur le PC de show : copiez tout le dossier, puis double-cliquez Lumina.exe
echo Aucun PowerShell, aucun SDK .NET a installer.
exit /b 0
