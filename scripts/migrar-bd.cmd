@echo off
REM Migrar base de datos (doble clic). Acepta los mismos parametros que migrar-bd.ps1
title InstaVende - Migrar base de datos
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0migrar-bd.ps1" %*
echo.
pause
