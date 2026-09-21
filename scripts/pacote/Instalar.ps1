<#
.SYNOPSIS
    Instala (ou atualiza) o Varthex Comanda para o usuario atual, sem administrador.

.DESCRIPTION
    Copia os arquivos deste pacote para -Destino (padrao:
    %LOCALAPPDATA%\Programs\VarthexComanda). Os DADOS do aplicativo ficam em
    %LOCALAPPDATA%\VarthexComanda e nunca sao tocados por este script.
    Em atualizacao, a instalacao anterior fica em <Destino>.anterior (uma so
    copia, substituida a cada atualizacao) para permitir o retorno.
    Aceita -WhatIf (simula sem alterar nada) e -Confirm.

.PARAMETER Destino
    Pasta de instalacao dos binarios. Aceita %VARIAVEIS% e caminhos relativos
    (resolvidos a partir da pasta atual do PowerShell). Nao aceita caminhos de rede.

.PARAMETER SemAtalhos
    Nao cria atalhos na Area de Trabalho e no Menu Iniciar.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
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

if (-not ('VarthexNativo' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class VarthexNativo {
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint GetLongPathName(string curto, StringBuilder longo, uint tamanho);
}
'@
}

# ---------------------------------------------------------------- caminhos

function Normalizar([string]$caminho) {
    $raiz = [IO.Path]::GetPathRoot($caminho)
    if ($caminho.Length -gt $raiz.Length) { return $caminho.TrimEnd('\') }
    return $caminho
}

# Expande %VAR%, resolve caminhos relativos a partir da pasta atual do PowerShell
# e recusa caminhos de rede (UNC / \\?\).
function ResolverCaminho([string]$caminho) {
    if ([string]::IsNullOrWhiteSpace($caminho)) { throw 'Caminho vazio.' }
    $expandido = [Environment]::ExpandEnvironmentVariables($caminho.Trim().Trim('"'))
    $resolvido = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($expandido)
    if ($expandido.StartsWith('\\') -or $resolvido.StartsWith('\\')) {
        throw "Destino recusado: '$caminho' e um caminho de rede (UNC). Use uma pasta em um disco local."
    }
    return (Normalizar $resolvido)
}

function ParaLongo([string]$existente) {
    $sb = New-Object System.Text.StringBuilder 32768
    $n = [VarthexNativo]::GetLongPathName($existente, $sb, [uint32]$sb.Capacity)
    if ($n -gt 0 -and $n -lt $sb.Capacity) { return $sb.ToString() }
    return $existente
}

# Caminho absoluto canonico: resolve relativos, expande %VAR% e nomes curtos 8.3
# (do trecho que ja existe) para comparar com as pastas protegidas.
function Canonico([string]$caminho) {
    $atual = ResolverCaminho $caminho
    $resto = New-Object System.Collections.Generic.List[string]
    while (-not (Test-Path -LiteralPath $atual)) {
        $pai = [IO.Path]::GetDirectoryName($atual)
        if ([string]::IsNullOrEmpty($pai)) { break }
        $resto.Insert(0, [IO.Path]::GetFileName($atual))
        $atual = $pai
    }
    if (Test-Path -LiteralPath $atual) {
        $atual = ParaLongo ((Get-Item -LiteralPath $atual -Force).FullName)
    }
    foreach ($segmento in $resto) { $atual = Join-Path $atual $segmento }
    return (Normalizar $atual)
}

function DentroOuIgual([string]$caminho, [string]$baseDir) {
    $baseDir = $baseDir.TrimEnd('\')
    return $caminho.Equals($baseDir, [StringComparison]::OrdinalIgnoreCase) -or
        $caminho.StartsWith($baseDir + '\', [StringComparison]::OrdinalIgnoreCase)
}

function PastaDeDados {
    if ($env:VARTHEX_COMANDA_DADOS) { return (Canonico $env:VARTHEX_COMANDA_DADOS) }
    return (Canonico (Join-Path $env:LOCALAPPDATA 'VarthexComanda'))
}

# Recusa caminhos perigosos. Lanca excecao com a razao; nao altera nada.
function Assert-CaminhoSeguro([string]$completo, [string]$rotulo = 'Destino') {
    $raiz = [IO.Path]::GetPathRoot($completo)
    if ($completo.TrimEnd('\').Equals($raiz.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) {
        throw "$rotulo recusado: '$completo' e a raiz de um disco."
    }
    $rede = $false
    try { $rede = ((New-Object IO.DriveInfo $raiz).DriveType -eq [IO.DriveType]::Network) } catch { $rede = $false }
    if ($rede) {
        throw "$rotulo recusado: '$completo' esta em uma unidade de rede. Use uma pasta em um disco local."
    }
    $segmentos = @($completo.Substring($raiz.Length).Split('\', [StringSplitOptions]::RemoveEmptyEntries))
    if (($segmentos.Count + 1) -lt 4) {
        throw "$rotulo recusado: '$completo' e curto demais (use uma subpasta dedicada, por exemplo ...\Programs\VarthexComanda)."
    }
    $dados = PastaDeDados
    if (DentroOuIgual $completo $dados) {
        throw "$rotulo recusado: '$completo' esta dentro da pasta de dados '$dados'."
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
        $pn = $null
        try { $pn = Canonico $p } catch { continue }
        if (DentroOuIgual $pn $completo) {
            throw "$rotulo recusado: '$completo' contem ou e a pasta protegida '$pn'."
        }
    }
}

# ---------------------------------------------------------------- utilidades

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
    $arquivo = Join-Path $pastaDoAtalho $NomeAtalho
    if (-not $PSCmdlet.ShouldProcess($arquivo, 'Criar atalho')) { return }
    $shell = New-Object -ComObject WScript.Shell
    try {
        $atalho = $shell.CreateShortcut($arquivo)
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

$destinoCompleto = Canonico $Destino
$pacoteCompleto = Canonico $Pacote
Assert-CaminhoSeguro $destinoCompleto

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

$atualizando = $false
if (Test-Path -LiteralPath $destinoCompleto) {
    $temItens = @(Get-ChildItem -LiteralPath $destinoCompleto -Force).Count -gt 0
    if ($temItens) {
        if (-not (Test-Path -LiteralPath $exeDestino)) {
            throw "Destino recusado: '$destinoCompleto' nao esta vazio e nao parece uma instalacao do Varthex Comanda. Escolha uma pasta vazia."
        }
        $atualizando = $true
    }
}

if ($atualizando) {
    # A copia .anterior sera substituida: so pode ser apagada se for mesmo uma
    # copia do Varthex Comanda. Qualquer outra coisa com esse nome e preservada.
    Assert-CaminhoSeguro $anterior 'Copia anterior'
    if ((Test-Path -LiteralPath $anterior) -and -not (Test-Path -LiteralPath (Join-Path $anterior $NomeExe))) {
        throw "Recusado: '$anterior' existe e nao parece uma copia do Varthex Comanda. Renomeie ou remova essa pasta antes de atualizar."
    }
    $versaoAntiga = VersaoInstaladaEm $destinoCompleto
    Write-Host "Atualizando de $versaoAntiga para $versaoNova"
}
else {
    Write-Host "Instalando a versao $versaoNova"
}

# Evita que uma pasta a mover/remover seja o diretorio atual deste processo.
Set-Location -LiteralPath ([IO.Path]::GetTempPath())

if ($atualizando) {
    if (Test-Path -LiteralPath $anterior) {
        Remove-Item -LiteralPath $anterior -Recurse -Force
    }
    Move-Item -LiteralPath $destinoCompleto -Destination $anterior
}

try {
    if ($PSCmdlet.ShouldProcess($destinoCompleto, 'Copiar os arquivos do pacote e gravar versao-instalada.txt')) {
        if (-not (Test-Path -LiteralPath $destinoCompleto)) {
            [void](New-Item -ItemType Directory -Path $destinoCompleto -WhatIf:$false -Confirm:$false)
        }
        Copy-Item -Path (Join-Path $pacoteCompleto '*') -Destination $destinoCompleto -Recurse -Force -WhatIf:$false -Confirm:$false
        $linhas = @(
            "Versao: $versaoNova",
            ('Instalada em: ' + (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz'))
        )
        Set-Content -LiteralPath (Join-Path $destinoCompleto 'versao-instalada.txt') -Value $linhas -Encoding UTF8 -WhatIf:$false -Confirm:$false
    }
}
catch {
    $falha = $_
    Write-Host 'Falha ao copiar os arquivos. Desfazendo a instalacao.'
    try {
        if (Test-Path -LiteralPath $destinoCompleto) {
            Remove-Item -LiteralPath $destinoCompleto -Recurse -Force
        }
        if ($atualizando -and (Test-Path -LiteralPath $anterior)) {
            Move-Item -LiteralPath $anterior -Destination $destinoCompleto
            Write-Host "A instalacao anterior foi restaurada em '$destinoCompleto'."
        }
    }
    catch {
        Write-Host "Nao foi possivel desfazer automaticamente: $($_.Exception.Message)" -ForegroundColor Red
        if ($atualizando) {
            Write-Host "Sua instalacao anterior esta em '$anterior'. Para voltar, renomeie essa pasta para '$destinoCompleto' (veja o LEIAME.txt)." -ForegroundColor Red
        }
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
if ($WhatIfPreference) {
    Write-Host "Simulacao (-WhatIf): nada foi alterado. Destino seria: $destinoCompleto"
}
else {
    Write-Host "Varthex Comanda $versaoNova instalado em: $destinoCompleto"
    Write-Host "Dados do aplicativo (preservados, nao foram alterados): $(PastaDeDados)"
    if ($atualizando) {
        Write-Host "A versao anterior foi guardada em: $anterior (para retornar, veja o LEIAME.txt)"
    }
}
