<#
.SYNOPSIS
    Remove o Varthex Comanda (binarios e atalhos). NUNCA remove os dados.

.DESCRIPTION
    Apaga a pasta de instalacao (-Destino, padrao
    %LOCALAPPDATA%\Programs\VarthexComanda), a copia <Destino>.anterior e os
    atalhos que apontam para essa instalacao. A pasta de dados
    (%LOCALAPPDATA%\VarthexComanda) e preservada.

.PARAMETER Destino
    Pasta onde o aplicativo foi instalado.
#>
[CmdletBinding()]
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
        throw "Destino recusado: '$destinoCompleto' e curto demais (use a pasta dedicada, por exemplo ...\Programs\VarthexComanda)."
    }
    $dados = PastaDeDados
    if (DentroOuIgual $destinoCompleto $dados) {
        throw "Destino recusado: '$destinoCompleto' esta dentro da pasta de dados '$dados', que nunca e removida."
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
        Remove-Item -LiteralPath $arquivo -Force
        return $true
    }
    return $false
}

# ---------------------------------------------------------------- execucao

$destinoCompleto = Normalizar $Destino
Assert-DestinoSeguro $destinoCompleto

$exeDestino = Join-Path $destinoCompleto $NomeExe
$anterior = "$destinoCompleto.anterior"
$temInstalacao = Test-Path -LiteralPath $exeDestino
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

if ($temInstalacao) {
    Remove-Item -LiteralPath $destinoCompleto -Recurse -Force
}
if ($temAnterior) {
    Remove-Item -LiteralPath $anterior -Recurse -Force
}

Write-Host "Varthex Comanda removido de: $destinoCompleto"
Write-Host "Atalhos removidos: $removidos"
Write-Host "Dados do aplicativo (preservados, nao foram removidos): $(PastaDeDados)"
