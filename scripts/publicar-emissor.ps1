[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
dotnet publish (Join-Path $raiz 'backend/tools/VarthexComanda.Licencas/VarthexComanda.Licencas.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:DebugType=none -o (Join-Path $raiz 'artifacts/emissor')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao publicar emissor.' }
