@echo off
rem O proprio .cmd pode estar dentro da pasta que sera removida. O Desinstalar.ps1
rem o preserva ate aqui; na ultima linha o truque (goto) 2>nul encerra o contexto
rem deste arquivo sem erros, e so entao ele se apaga, apaga a pasta ja vazia e
rem devolve o codigo de saida. (Sem SETLOCAL: ele restauraria o diretorio atual.)
set "VARTHEX_DESINSTALAR_CMD=%~f0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Desinstalar.ps1" %*
set "CODIGO=%ERRORLEVEL%"
if "%~1"=="" pause
cd /d "%TEMP%"
(goto) 2>nul & set "VARTHEX_DESINSTALAR_CMD=" & set "CODIGO=" & (if "%CODIGO%"=="0" if not exist "%~dp0VarthexComanda.exe" if not exist "%~dp0Desinstalar.ps1" del "%~f0" & rd "%~dp0") & exit /b %CODIGO%
