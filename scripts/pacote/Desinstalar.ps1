<#
.SYNOPSIS
    Remove o Varthex Comanda (binarios e atalhos). NUNCA remove os dados.

.DESCRIPTION
    Apaga a pasta de instalacao INTEIRA (-Destino, padrao
    %LOCALAPPDATA%\Programs\VarthexComanda; nao guarde arquivos seus ali), a
    copia <Destino>.anterior e os atalhos que apontam para essa instalacao.
    A pasta de dados (%LOCALAPPDATA%\VarthexComanda) e preservada.
    Aceita -WhatIf (simula sem alterar nada) e -Confirm.

.PARAMETER Destino
    Pasta onde o aplicativo foi instalado. Aceita %VARIAVEIS% e caminhos
    relativos (resolvidos a partir da pasta atual do PowerShell). Nao aceita
    caminhos de rede.
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$Destino = (Join-Path $env:LOCALAPPDATA 'Programs\VarthexComanda')
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

# Remove o atalho somente se ele apontar para o exe desta instalacao.
function RemoverAtalhoSeForDaqui([string]$pastaDoAtalho, [string]$exe) {
    $arquivo = Join-Path $pastaDoAtalho $NomeAtalho
    if (-not (Test-Path -LiteralPath $arquivo)) { return $false }
    $shell = New-Object -ComObject WScript.Shell
    try {
        $alvo = $shell.CreateShortcut($arquivo).TargetPath
    }
    finally {
        [void][Runtime.InteropServices.Marshal]::ReleaseComObject($shell)
    }
    if ($alvo -and $alvo.Equals($exe, [StringComparison]::OrdinalIgnoreCase)) {
        if ($PSCmdlet.ShouldProcess($arquivo, 'Remover atalho')) {
            Remove-Item -LiteralPath $arquivo -Force
        }
        return $true
    }
    return $false
}

# ---------------------------------------------------------------- execucao

$destinoCompleto = Canonico $Destino
Assert-CaminhoSeguro $destinoCompleto

$exeDestino = Join-Path $destinoCompleto $NomeExe
$anterior = "$destinoCompleto.anterior"
$temInstalacao = Test-Path -LiteralPath $exeDestino
$temAnterior = $false
if ($temInstalacao -or (Test-Path -LiteralPath $anterior)) {
    Assert-CaminhoSeguro $anterior 'Copia anterior'
}
$temAnterior = Test-Path -LiteralPath (Join-Path $anterior $NomeExe)

if (-not $temInstalacao -and -not $temAnterior) {
    throw "Nenhuma instalacao do Varthex Comanda encontrada em '$destinoCompleto'. Nada foi removido."
}

if (ExeEmExecucao $exeDestino) {
    throw 'Feche o Varthex Comanda e tente de novo.'
}

# Evita que a pasta a remover seja o diretorio atual deste processo.
Set-Location -LiteralPath ([IO.Path]::GetTempPath())

$removidos = 0
foreach ($pasta in @([Environment]::GetFolderPath('DesktopDirectory'), [Environment]::GetFolderPath('Programs'))) {
    if (RemoverAtalhoSeForDaqui $pasta $exeDestino) { $removidos++ }
}

# Quando iniciado pelo Desinstalar.cmd que mora dentro do destino, o proprio .cmd
# ainda esta em execucao: ele nao pode ser apagado agora (o cmd.exe perderia o
# arquivo no meio da execucao). Fica de fora e o .cmd se remove ao terminar.
$manter = $null
if ($env:VARTHEX_DESINSTALAR_CMD) {
    try {
        $candidato = Canonico $env:VARTHEX_DESINSTALAR_CMD
        if (DentroOuIgual $candidato $destinoCompleto) { $manter = $candidato }
    }
    catch { $manter = $null }
}

if ($temInstalacao -and $PSCmdlet.ShouldProcess($destinoCompleto, 'Remover a pasta do programa')) {
    if ($manter) {
        foreach ($item in @(Get-ChildItem -LiteralPath $destinoCompleto -Force)) {
            if (-not $item.FullName.Equals($manter, [StringComparison]::OrdinalIgnoreCase)) {
                Remove-Item -LiteralPath $item.FullName -Recurse -Force -WhatIf:$false -Confirm:$false
            }
        }
    }
    else {
        Remove-Item -LiteralPath $destinoCompleto -Recurse -Force -WhatIf:$false -Confirm:$false
    }
}
if ($temAnterior -and $PSCmdlet.ShouldProcess($anterior, 'Remover a copia anterior')) {
    Remove-Item -LiteralPath $anterior -Recurse -Force -WhatIf:$false -Confirm:$false
}

if ($WhatIfPreference) {
    Write-Host "Simulacao (-WhatIf): nada foi removido. Seria removido: $destinoCompleto"
}
else {
    Write-Host "Varthex Comanda removido de: $destinoCompleto"
    Write-Host "Atalhos removidos: $removidos"
    Write-Host "Dados do aplicativo (preservados, nao foram removidos): $(PastaDeDados)"
}
