@echo off
rem Lanceur Windows classique. Aucun PowerShell.
cd /d "%~dp0"
if not exist "Lumina.exe" (
  echo Lumina.exe introuvable dans ce dossier.
  pause
  exit /b 1
)
start "" "Lumina.exe"
