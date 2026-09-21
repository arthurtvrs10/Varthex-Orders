<#
.SYNOPSIS
    Instala (ou atualiza) o Varthex Comanda para o usuario atual, sem administrador.

.DESCRIPTION
    Copia os arquivos deste pacote para -Destino (padrao:
    %LOCALAPPDATA%\Programs\VarthexComanda). Os DADOS do aplicativo ficam em
    %LOCALAPPDATA%\VarthexComanda e nunca sao tocados por este script.
    Em atualizacao, a instalacao anterior fica em <Destino>.anterior (uma so
    copia, substituida a cada atualizacao) para permitir o retorno.

.PARAMETER Destino
    Pasta de instalacao dos binarios.

.PARAMETER SemAtalhos
    Nao cria atalhos na Area de Trabalho e no Menu Iniciar.
#>
[CmdletBinding()]
param(
    [string]$Destino = (Join-Path $env:LOCALAPPDATA 'Programs\VarthexComanda'),
    [switch]$SemAtalhos
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Mostra so a mensagem (sem pilha de erro) e sai com codigo 1.
trap {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

$NomeExe = 'VarthexComanda.exe'
$NomeAtalho = 'Varthex Comanda.lnk'
$Pacote = $PSScriptRoot

function Normalizar([string]$caminho) {
    $completo = [IO.Path]::GetFullPath($caminho)
    $raiz = [IO.Path]::GetPathRoot($completo)
    if ($completo.Length -gt $raiz.Length) { $completo = $completo.TrimEnd('\') }
    return $completo
}

function DentroOuIgual([string]$caminho, [string]$baseDir) {
    $baseDir = $baseDir.TrimEnd('\')
    return $caminho.Equals($baseDir, [StringComparison]::OrdinalIgnoreCase) -or
        $caminho.StartsWith($baseDir + '\', [StringComparison]::OrdinalIgnoreCase)
}

function PastaDeDados {
    if ($env:VARTHEX_COMANDA_DADOS) { return (Normalizar $env:VARTHEX_COMANDA_DADOS) }
    return (Normalizar (Join-Path $env:LOCALAPPDATA 'VarthexComanda'))
}

# Recusa destinos perigosos. Lanca excecao com a razao; nao altera nada.
function Assert-DestinoSeguro([string]$destinoCompleto) {
    $raiz = [IO.Path]::GetPathRoot($destinoCompleto)
    if ($destinoCompleto.Equals($raiz.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase) -or
        $destinoCompleto.Equals($raiz, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Destino recusado: '$destinoCompleto' e a raiz de um disco."
    }
    $segmentos = @($destinoCompleto.Substring($raiz.Length).Split('\', [StringSplitOptions]::RemoveEmptyEntries))
    if (($segmentos.Count + 1) -lt 4) {
        throw "Destino recusado: '$destinoCompleto' e curto demais (use uma subpasta dedicada, por exemplo ...\Programs\VarthexComanda)."
    }
    $dados = PastaDeDados
    if (DentroOuIgual $destinoCompleto $dados) {
        throw "Destino recusado: '$destinoCompleto' esta dentro da pasta de dados '$dados'."
    }
    $protegidas = @(
        $env:USERPROFILE,
        $env:LOCALAPPDATA,
        $env:APPDATA,
        $dados,
        [Environment]::GetFolderPath('UserProfile'),
        [Environment]::GetFolderPath('DesktopDirectory'),
        [Environment]::GetFolderPath('Programs'),
        [Environment]::GetFolderPath('MyDocuments'),
        $env:ProgramFiles,
        ${env:ProgramFiles(x86)},
        $env:windir,
        $env:TEMP
    )
    foreach ($p in $protegidas) {
        if ([string]::IsNullOrWhiteSpace($p)) { continue }
        $pn = Normalizar $p
        if (DentroOuIgual $pn $destinoCompleto) {
            throw "Destino recusado: '$destinoCompleto' contem ou e a pasta protegida '$pn'."
        }
    }
}

function ExeEmExecucao([string]$exe) {
    foreach ($proc in @(Get-Process -ErrorAction SilentlyContinue)) {
        $caminhoProc = $null
        try { $caminhoProc = $proc.Path } catch { $caminhoProc = $null }
        if ($caminhoProc -and $caminhoProc.Equals($exe, [StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

function VersaoDoExe([string]$exe) {
    $vi = (Get-Item -LiteralPath $exe).VersionInfo
    if ($vi.ProductVersion) { return $vi.ProductVersion }
    return $vi.FileVersion
}

function VersaoInstaladaEm([string]$pasta) {
    $arquivo = Join-Path $pasta 'versao-instalada.txt'
    if (Test-Path -LiteralPath $arquivo) {
        foreach ($linha in Get-Content -LiteralPath $arquivo) {
            if ($linha -match '^Versao:\s*(.+)$') { return $Matches[1].Trim() }
        }
    }
    $exe = Join-Path $pasta $NomeExe
    if (Test-Path -LiteralPath $exe) { return (VersaoDoExe $exe) }
    return 'desconhecida'
}

function CriarAtalho([string]$pastaDoAtalho, [string]$exe, [string]$pastaTrabalho) {
    $shell = New-Object -ComObject WScript.Shell
    try {
        $atalho = $shell.CreateShortcut((Join-Path $pastaDoAtalho $NomeAtalho))
        $atalho.TargetPath = $exe
        $atalho.WorkingDirectory = $pastaTrabalho
        $atalho.IconLocation = "$exe,0"
        $atalho.Description = 'Varthex Comanda'
        $atalho.Save()
    }
    finally {
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($shell)
    }
}

# ---------------------------------------------------------------- execucao

$destinoCompleto = Normalizar $Destino
$pacoteCompleto = Normalizar $Pacote
Assert-DestinoSeguro $destinoCompleto

if ((DentroOuIgual $destinoCompleto $pacoteCompleto) -or (DentroOuIgual $pacoteCompleto $destinoCompleto)) {
    throw "Destino recusado: '$destinoCompleto' e a pasta do pacote ('$pacoteCompleto') estao uma dentro da outra."
}

$exePacote = Join-Path $pacoteCompleto $NomeExe
if (-not (Test-Path -LiteralPath $exePacote)) {
    throw "Pacote incompleto: '$NomeExe' nao encontrado em '$pacoteCompleto'. Extraia o .zip inteiro antes de instalar."
}

$exeDestino = Join-Path $destinoCompleto $NomeExe
$anterior = "$destinoCompleto.anterior"
$versaoNova = VersaoDoExe $exePacote

if (ExeEmExecucao $exeDestino) {
    throw 'Feche o Varthex Comanda e tente de novo.'
}

$existeDestino = Test-Path -LiteralPath $destinoCompleto
$atualizando = $false
if ($existeDestino) {
    $temItens = @(Get-ChildItem -LiteralPath $destinoCompleto -Force).Count -gt 0
    if ($temItens) {
        if (-not (Test-Path -LiteralPath $exeDestino)) {
            throw "Destino recusado: '$destinoCompleto' nao esta vazio e nao parece uma instalacao do Varthex Comanda. Escolha uma pasta vazia."
        }
        $atualizando = $true
    }
}

if ($atualizando) {
    $versaoAntiga = VersaoInstaladaEm $destinoCompleto
    Write-Host "Atualizando de $versaoAntiga para $versaoNova"
    if (Test-Path -LiteralPath $anterior) {
        Remove-Item -LiteralPath $anterior -Recurse -Force
    }
    Move-Item -LiteralPath $destinoCompleto -Destination $anterior
}
else {
    Write-Host "Instalando a versao $versaoNova"
}

try {
    if (-not (Test-Path -LiteralPath $destinoCompleto)) {
        [void](New-Item -ItemType Directory -Path $destinoCompleto)
    }
    Copy-Item -Path (Join-Path $pacoteCompleto '*') -Destination $destinoCompleto -Recurse -Force
    $linhas = @(
        "Versao: $versaoNova",
        ('Instalada em: ' + (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz'))
    )
    Set-Content -LiteralPath (Join-Path $destinoCompleto 'versao-instalada.txt') -Value $linhas -Encoding UTF8
}
catch {
    $falha = $_
    Write-Host 'Falha ao copiar os arquivos. Desfazendo a instalacao.'
    if (Test-Path -LiteralPath $destinoCompleto) {
        Remove-Item -LiteralPath $destinoCompleto -Recurse -Force -ErrorAction SilentlyContinue
    }
    if ($atualizando -and (Test-Path -LiteralPath $anterior)) {
        Move-Item -LiteralPath $anterior -Destination $destinoCompleto
        Write-Host 'A instalacao anterior foi restaurada.'
    }
    throw $falha
}

if (-not $SemAtalhos) {
    CriarAtalho ([Environment]::GetFolderPath('DesktopDirectory')) $exeDestino $destinoCompleto
    CriarAtalho ([Environment]::GetFolderPath('Programs')) $exeDestino $destinoCompleto
    Write-Host 'Atalhos criados na Area de Trabalho e no Menu Iniciar.'
}
else {
    Write-Host 'Atalhos nao criados (-SemAtalhos).'
}

Write-Host ''
Write-Host "Varthex Comanda $versaoNova instalado em: $destinoCompleto"
Write-Host "Dados do aplicativo (preservados, nao foram alterados): $(PastaDeDados)"
if ($atualizando) {
    Write-Host "A versao anterior foi guardada em: $anterior (para retornar, veja o LEIAME.txt)"
}
