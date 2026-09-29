<#
.SYNOPSIS
Gera o instalador Windows x64 com runtime incluso (RNF09, CT21).
#>
[CmdletBinding()]
param([string]$Compilador, [string]$Saida)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$raiz = Split-Path -Parent $PSScriptRoot
if (-not $Saida) { $Saida = Join-Path $raiz 'artifacts' }
$Saida = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Saida)
if (-not $Compilador) {
    $candidatos = @(
        (Join-Path $raiz 'artifacts\tools\InnoSetup\ISCC.exe'),
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    $Compilador = $candidatos | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}
if (-not $Compilador -or -not (Test-Path -LiteralPath $Compilador)) {
    throw 'Instale Inno Setup 6 (https://jrsoftware.org/isdl.php) ou informe -Compilador <caminho de ISCC.exe>.'
}
& (Join-Path $PSScriptRoot 'publicar.ps1') -Saida $Saida
$projeto = Join-Path $raiz 'backend\src\VarthexComanda.Desktop\VarthexComanda.Desktop.csproj'
$versao = (Select-Xml -Path $projeto -XPath '//Version' | Select-Object -First 1).Node.InnerText.Trim()
& $Compilador "/DAppVersion=$versao" "/DPublishPath=$Saida\publish\win-x64" "/DOutputPath=$Saida" (Join-Path $PSScriptRoot 'instalador.iss')
if ($LASTEXITCODE -ne 0) { throw "Compilacao do instalador falhou: $LASTEXITCODE" }
$instalador = Join-Path $Saida "VarthexComanda-$versao-Setup-win-x64.exe"
if (-not (Test-Path -LiteralPath $instalador)) { throw 'Instalador nao foi gerado.' }
$hash = (Get-FileHash -LiteralPath $instalador -Algorithm SHA256).Hash
"$hash  $([IO.Path]::GetFileName($instalador))" | Set-Content -LiteralPath "$instalador.sha256" -Encoding ascii
Write-Host "Instalador gerado: $instalador"
