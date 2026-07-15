@echo off
REM ===================================================================
REM  rebuild.bat - lattice-fraktal
REM  Beendet den laufenden Bake-Server (entsperrt die .exe),
REM  baut den Kern neu und startet den Server wieder.
REM  Einfach doppelklicken oder im Terminal aufrufen.
REM ===================================================================

echo === Laufenden Server beenden (falls vorhanden) ===
taskkill /IM lattice-fraktal.exe /F >nul 2>&1

cd /d "%~dp0src\LatticeFraktal"

echo.
echo === Build ===
dotnet build
if errorlevel 1 (
  echo.
  echo *** BUILD FEHLGESCHLAGEN - Server wird NICHT gestartet. ***
  echo Bitte die Fehler oben beheben und rebuild.bat erneut ausfuehren.
  pause
  exit /b 1
)

echo.
echo === Server starten ===
echo Vorschau im Browser: http://localhost:5151/
echo (Strg+C beendet den Server.)
echo.
dotnet run --no-build -- serve
