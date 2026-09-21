<#
.SYNOPSIS
    Publica o Varthex Comanda (win-x64, autocontido) e monta o pacote .zip.

.DESCRIPTION
    1. Le a versao do <Version> do VarthexComanda.Desktop.csproj.
    2. dotnet publish Release win-x64 --self-contained (multi-arquivo, sem
       PublishSingleFile e sem trimming) em <Saida>\publish\win-x64.
    3. Monta <Saida>\VarthexComanda-<versao>-win-x64.zip com os arquivos
       publicados + Instalar.cmd, Instalar.ps1, Desinstalar.ps1 e LEIAME.txt
       na raiz do zip.

.PARAMETER Saida
    Pasta de saida (padrao: <raiz do repositorio>\artifacts).
#>
[CmdletBinding()]
param(
    [string]$Saida
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
if (-not $Saida) { $Saida = Join-Path $raiz 'artifacts' }
$Saida = [IO.Path]::GetFullPath($Saida)

$csproj = Join-Path $raiz 'backend\src\VarthexComanda.Desktop\VarthexComanda.Desktop.csproj'
$pastaPacote = Join-Path $PSScriptRoot 'pacote'
if (-not (Test-Path -LiteralPath $csproj)) { throw "Projeto nao encontrado: $csproj" }

# Versao vem do csproj (fonte unica).
$no = Select-Xml -Path $csproj -XPath '//Version' | Select-Object -First 1
if (-not $no) { throw "Nao achei <Version> em $csproj" }
$versao = $no.Node.InnerText.Trim()
if ($versao -notmatch '^\d+\.\d+\.\d+([\-+.][0-9A-Za-z.\-]+)?$') { throw "Versao invalida no csproj: '$versao'" }

# dotnet: PATH ou instalacao padrao.
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnet) { $dotnet = $dotnet.Source }
else {
    $padrao = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if (Test-Path -LiteralPath $padrao) { $dotnet = $padrao } else { throw 'dotnet nao encontrado no PATH.' }
}

$pastaPublicacao = Join-Path $Saida 'publish\win-x64'
$arquivoZip = Join-Path $Saida "VarthexComanda-$versao-win-x64.zip"

Write-Host "Varthex Comanda $versao - publicando em $pastaPublicacao"

# Limpa somente a pasta de publicacao gerada por este script.
if (Test-Path -LiteralPath $pastaPublicacao) {
    if (-not $pastaPublicacao.EndsWith('publish\win-x64', [StringComparison]::OrdinalIgnoreCase)) {
        throw "Recusado limpar '$pastaPublicacao'."
    }
    Remove-Item -LiteralPath $pastaPublicacao -Recurse -Force
}

& $dotnet publish $csproj -c Release -r win-x64 --self-contained true -o $pastaPublicacao -p:PublishSingleFile=false -p:PublishTrimmed=false
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou (codigo $LASTEXITCODE)." }

if (-not (Test-Path -LiteralPath (Join-Path $pastaPublicacao 'VarthexComanda.exe'))) {
    throw 'VarthexComanda.exe nao foi gerado.'
}

# Monta o zip.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

if (Test-Path -LiteralPath $arquivoZip) { Remove-Item -LiteralPath $arquivoZip -Force }
[void](New-Item -ItemType Directory -Force -Path $Saida)

function Add-ArquivosAoZip($zip, [string]$pasta) {
    $baseLen = ([IO.Path]::GetFullPath($pasta)).TrimEnd('\').Length + 1
    foreach ($arq in Get-ChildItem -LiteralPath $pasta -Recurse -File -Force) {
        $nome = $arq.FullName.Substring($baseLen).Replace('\', '/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $arq.FullName, $nome, [IO.Compression.CompressionLevel]::Optimal)
    }
}

$zip = [IO.Compression.ZipFile]::Open($arquivoZip, [IO.Compression.ZipArchiveMode]::Create)
try {
    Add-ArquivosAoZip $zip $pastaPublicacao
    foreach ($nomeArq in 'Instalar.cmd', 'Instalar.ps1', 'Desinstalar.ps1', 'LEIAME.txt') {
        $origem = Join-Path $pastaPacote $nomeArq
        if (-not (Test-Path -LiteralPath $origem)) { throw "Arquivo do pacote ausente: $origem" }
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $origem, $nomeArq, [IO.Compression.CompressionLevel]::Optimal)
    }
}
finally {
    $zip.Dispose()
}

$tamanho = (Get-Item -LiteralPath $arquivoZip).Length
Write-Host ''
Write-Host "Pacote gerado: $arquivoZip"
Write-Host ('Tamanho: {0:N1} MB' -f ($tamanho / 1MB))
