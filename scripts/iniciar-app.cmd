@echo off
REM Compilar y levantar InstaVende (doble clic). Acepta los mismos parametros que iniciar-app.ps1
title InstaVende - Compilar y levantar InstaVende
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0iniciar-app.ps1" %*
echo.
pause
