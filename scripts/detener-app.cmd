@echo off
REM Detener InstaVende (doble clic). Acepta los mismos parametros que detener-app.ps1
title InstaVende - Detener InstaVende
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0detener-app.ps1" %*
echo.
pause
