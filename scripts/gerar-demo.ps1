<# Gera a edição com catálogo fictício (RF03, RNF09, CT21). #>
[CmdletBinding()]
param([string]$Compilador)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
$versao = (Select-Xml -Path "$raiz/backend/src/VarthexComanda.Desktop/VarthexComanda.Desktop.csproj" -XPath '//Version').Node.InnerText.Trim()
$saida = Join-Path $raiz "artifacts/demo-$versao"
$publicacao = Join-Path $saida 'publish/win-x64'
if (-not $Compilador) { $Compilador = Join-Path $raiz 'artifacts/tools/InnoSetup/ISCC.exe' }
if (-not (Test-Path -LiteralPath $Compilador)) { throw 'Informe o caminho do Inno Setup em -Compilador.' }
& (Join-Path $PSScriptRoot 'publicar.ps1') -Saida $saida
if ($LASTEXITCODE -ne 0) { throw 'Publicação falhou.' }
& $Compilador "/DAppVersion=$versao" "/DPublishPath=$publicacao" "/DOutputPath=$saida" (Join-Path $PSScriptRoot 'instalador-demo.iss')
if ($LASTEXITCODE -ne 0) { throw 'Compilação do instalador demo falhou.' }
$instalador = Join-Path $saida "VarthexComanda-Demo-$versao-Setup-win-x64.exe"
$hash = (Get-FileHash -LiteralPath $instalador -Algorithm SHA256).Hash
"$hash  $([IO.Path]::GetFileName($instalador))" | Set-Content -LiteralPath "$instalador.sha256" -Encoding ascii
Write-Host "Instalador de demonstração: $instalador"
