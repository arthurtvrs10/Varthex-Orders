@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Instalar.ps1" %*
set CODIGO=%ERRORLEVEL%
if "%~1"=="" pause
exit /b %CODIGO%
