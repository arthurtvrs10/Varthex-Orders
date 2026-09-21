<#
.SYNOPSIS
    Funcoes de apoio aos ensaios de homologacao (UI Automation, sem teclas).

.DESCRIPTION
    Dot-source este arquivo a partir do script de ensaio:

        . "$PSScriptRoot\Ajuda.ps1"

    Regras de seguranca embutidas (nao remova):
    * NUNCA envia teclas (SendKeys/keybd_event/SendInput) nem chama
      SetForegroundWindow. Toda interacao passa por padroes do UI Automation
      (Invoke, Value, Toggle, SelectionItem, Window).
    * O aplicativo so e iniciado com VARTHEX_COMANDA_DADOS apontando para uma
      pasta descartavel; a variavel e definida SO no processo filho
      (ProcessStartInfo), nunca neste processo.
    * Nunca le nem escreve em %LOCALAPPDATA%\VarthexComanda (dados reais) ou
      em %LOCALAPPDATA%\Programs\VarthexComanda (instalacao real).
    * So encerra processos que este script iniciou (lista $script:PidsProprios)
      e cujo executavel e o da instalacao temporaria.
#>

Set-StrictMode -Version Latest

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing

if (-not ('VarthexJanelaNativa' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class VarthexJanelaNativa {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint f);
}
'@
}

# Estado compartilhado com o script que fez o dot-source.
$script:PidsProprios = New-Object System.Collections.Generic.List[int]
$script:ExeAutorizado = $null

# ---------------------------------------------------------------- seguranca

function Assert-SemAppEmExecucao {
    <#  Aborta se ja existir um VarthexComanda rodando: pode ser o aplicativo real do usuario. #>
    $existentes = @(Get-Process -Name 'VarthexComanda*' -ErrorAction SilentlyContinue)
    if ($existentes.Count -gt 0) {
        $ids = ($existentes | ForEach-Object { $_.Id }) -join ', '
        throw "Ja existe VarthexComanda em execucao (PID $ids). Feche o aplicativo antes do ensaio; este script nunca encerra processo que nao iniciou."
    }
}

function Resolver-Caminho([string]$caminho) {
    if ([string]::IsNullOrWhiteSpace($caminho)) { throw 'Caminho vazio.' }
    $expandido = [Environment]::ExpandEnvironmentVariables($caminho.Trim().Trim('"'))
    return $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($expandido).TrimEnd('\')
}

function Test-DentroOuIgual([string]$caminho, [string]$baseDir) {
    $baseDir = $baseDir.TrimEnd('\')
    return $caminho.Equals($baseDir, [StringComparison]::OrdinalIgnoreCase) -or
        $caminho.StartsWith($baseDir + '\', [StringComparison]::OrdinalIgnoreCase)
}

function Assert-PastaDescartavel([string]$caminho, [string]$raizPermitida) {
    <#  Garante que so mexemos em pastas descartaveis dentro da raiz do ensaio. #>
    $completo = Resolver-Caminho $caminho
    $proibidas = @(
        (Join-Path $env:LOCALAPPDATA 'VarthexComanda'),
        (Join-Path $env:LOCALAPPDATA 'Programs\VarthexComanda')
    )
    foreach ($p in $proibidas) {
        $pn = (Resolver-Caminho $p)
        if ((Test-DentroOuIgual $completo $pn) -or (Test-DentroOuIgual $pn $completo)) {
            throw "Recusado: '$completo' toca a pasta real do aplicativo ('$pn')."
        }
    }
    if ($raizPermitida) {
        $raiz = Resolver-Caminho $raizPermitida
        if (-not (Test-DentroOuIgual $completo $raiz)) {
            throw "Recusado: '$completo' esta fora da raiz descartavel do ensaio ('$raiz')."
        }
    }
    return $completo
}

function Set-ExeAutorizado([string]$exe) {
    <#  Unico executavel que este script pode iniciar/encerrar (a instalacao temporaria). #>
    $script:ExeAutorizado = (Resolver-Caminho $exe)
}

# ---------------------------------------------------------------- processos

function Get-PidsProprios { return @($script:PidsProprios) }

function Register-PidProprio([int]$processId) {
    if (-not $script:PidsProprios.Contains($processId)) { [void]$script:PidsProprios.Add($processId) }
}

function Get-CaminhoDoProcesso($processo) {
    try { return $processo.Path } catch { return $null }
}

function Register-ProcessosDoExe {
    <#
        Registra processos do executavel autorizado que este script nao iniciou diretamente
        (caso do reinicio automatico apos restaurar um backup: o proprio app se re-executa).
        Seguro porque Assert-SemAppEmExecucao garantiu que nada estava rodando no inicio e
        porque so casa com o exe da instalacao temporaria.
    #>
    $novos = @()
    foreach ($p in @(Get-Process -Name 'VarthexComanda*' -ErrorAction SilentlyContinue)) {
        $caminho = Get-CaminhoDoProcesso $p
        if ($caminho -and $caminho.Equals($script:ExeAutorizado, [StringComparison]::OrdinalIgnoreCase)) {
            if (-not $script:PidsProprios.Contains($p.Id)) {
                Register-PidProprio $p.Id
                $novos += $p
            }
        }
    }
    return @($novos)
}

function Start-App {
    <#  Inicia o app instalado com VARTHEX_COMANDA_DADOS na pasta descartavel (so no filho). #>
    param(
        [Parameter(Mandatory = $true)][string]$Exe,
        [Parameter(Mandatory = $true)][string]$PastaDados,
        [Parameter(Mandatory = $true)][string]$RaizPermitida
    )
    $exeCompleto = Resolver-Caminho $Exe
    if ($exeCompleto -ne $script:ExeAutorizado) {
        throw "Recusado iniciar '$exeCompleto': nao e o executavel autorizado ('$($script:ExeAutorizado)')."
    }
    $dados = Assert-PastaDescartavel $PastaDados $RaizPermitida
    if (-not (Test-Path -LiteralPath $dados)) {
        throw "Pasta de dados inexistente: $dados"
    }

    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $exeCompleto
    $info.WorkingDirectory = [IO.Path]::GetDirectoryName($exeCompleto)
    $info.UseShellExecute = $false
    [void]$info.EnvironmentVariables.Remove('VARTHEX_COMANDA_DADOS')
    [void]$info.EnvironmentVariables.Add('VARTHEX_COMANDA_DADOS', $dados)

    $processo = [System.Diagnostics.Process]::Start($info)
    Register-PidProprio $processo.Id
    return $processo
}

function Stop-AppProprio {
    <#  Encerra pelo PID, so se for um processo iniciado por este script. #>
    param([Parameter(Mandatory = $true)][int]$ProcessId, [switch]$Forcado)
    if (-not $script:PidsProprios.Contains($ProcessId)) {
        throw "Recusado encerrar o PID $ProcessId : nao foi iniciado por este script."
    }
    $p = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if (-not $p) { return $false }
    Stop-Process -Id $ProcessId -Force:$Forcado -ErrorAction SilentlyContinue
    [void]$p.WaitForExit(10000)
    return $true
}

function Stop-TodosOsProprios {
    foreach ($processId in @($script:PidsProprios)) {
        try { [void](Stop-AppProprio -ProcessId $processId -Forcado) } catch { }
    }
}

function Wait-SaidaDoProcesso([int]$ProcessId, [int]$Segundos = 15) {
    $limite = (Get-Date).AddSeconds($Segundos)
    while ((Get-Date) -lt $limite) {
        $p = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
        if (-not $p) { return $true }
        Start-Sleep -Milliseconds 150
    }
    return $false
}

# ---------------------------------------------------------------- janelas / UIA

function Get-RaizDeJanela([IntPtr]$hwnd) {
    return [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
}

function Wait-JanelaPrincipal {
    <#
        Espera a janela principal aparecer e ficar visivel; devolve o tempo medido.
        O cronometro do chamador deve comecar antes do Start-App (RNF03).
    #>
    param([Parameter(Mandatory = $true)]$Processo, [int]$Segundos = 60, [string]$Titulo = 'Varthex Comanda')
    $limite = (Get-Date).AddSeconds($Segundos)
    while ((Get-Date) -lt $limite) {
        if ($Processo.HasExited) { throw "O processo $($Processo.Id) encerrou antes de mostrar a janela." }
        $Processo.Refresh()
        $h = $Processo.MainWindowHandle
        if ($h -ne [IntPtr]::Zero) {
            try {
                $el = Get-RaizDeJanela $h
                if ($el -and -not $el.Current.IsOffscreen -and $el.Current.Name -eq $Titulo) {
                    return $el
                }
            }
            catch { }
        }
        Start-Sleep -Milliseconds 25
    }
    throw "A janela '$Titulo' do PID $($Processo.Id) nao apareceu em $Segundos s."
}

function ConvertTo-Lista($Colecao) {
    <#
        AutomationElementCollection nao e desdobrada por @(...) (vira um unico item);
        aqui o foreach enumera de verdade e devolve um array de AutomationElement.
    #>
    $lista = New-Object System.Collections.Generic.List[object]
    if ($null -ne $Colecao) {
        foreach ($item in $Colecao) { [void]$lista.Add($item) }
    }
    return $lista.ToArray()
}

function Get-JanelasDoProcesso([int]$ProcessId) {
    <#
        Janelas de topo do processo MAIS as janelas que elas possuem: um dialogo modal
        do WPF (Owner = janela principal) nao aparece como filho da area de trabalho,
        e sim como descendente da janela dona.
    #>
    $condProcesso = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ProcessIdProperty, $ProcessId)
    $condJanela = New-Object System.Windows.Automation.AndCondition(
        $condProcesso,
        (New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
            [System.Windows.Automation.ControlType]::Window)))

    $raiz = [System.Windows.Automation.AutomationElement]::RootElement
    $lista = New-Object System.Collections.Generic.List[object]
    foreach ($topo in (ConvertTo-Lista $raiz.FindAll([System.Windows.Automation.TreeScope]::Children, $condProcesso))) {
        # as possuidas vem primeiro: um dialogo modal deve casar antes da janela dona,
        # que tambem "contem" os textos do dialogo por ser a dona dele na arvore
        try {
            foreach ($possuida in (ConvertTo-Lista $topo.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condJanela))) {
                [void]$lista.Add($possuida)
            }
        }
        catch { }
        [void]$lista.Add($topo)
    }
    return $lista.ToArray()
}

function Wait-Janela {
    <#  Espera uma janela de topo do processo com o titulo dado (e, opcionalmente, um texto dentro). #>
    param(
        [Parameter(Mandatory = $true)][int]$ProcessId,
        [Parameter(Mandatory = $true)][string]$Titulo,
        [int]$Segundos = 20,
        [string]$ContendoTexto,
        [IntPtr]$Exceto = [IntPtr]::Zero
    )
    $limite = (Get-Date).AddSeconds($Segundos)
    while ((Get-Date) -lt $limite) {
        foreach ($j in @(Get-JanelasDoProcesso $ProcessId)) {
            try {
                if ($j.Current.Name -ne $Titulo) { continue }
                if ($Exceto -ne [IntPtr]::Zero -and [IntPtr]$j.Current.NativeWindowHandle -eq $Exceto) { continue }
                if ($ContendoTexto) {
                    $textos = @(Get-TextosDe $j)
                    if (-not ($textos | Where-Object { $_ -and $_.Contains($ContendoTexto) })) { continue }
                }
                return $j
            }
            catch { }
        }
        Start-Sleep -Milliseconds 100
    }
    return $null
}

function Find-Elementos {
    <#  Descendentes por tipo de controle (Button, Text, Edit, CheckBox, ListItem, ...). #>
    param([Parameter(Mandatory = $true)]$Raiz, [Parameter(Mandatory = $true)][string]$Tipo)
    $ct = [System.Windows.Automation.ControlType]::$Tipo
    $cond = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::ControlTypeProperty, $ct)
    return (ConvertTo-Lista $Raiz.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond))
}

function Find-Elemento {
    <#  Primeiro descendente que casa com AutomationId e/ou Name (e tipo), com espera. #>
    param(
        [Parameter(Mandatory = $true)]$Raiz,
        [string]$AutomationId,
        [string]$Nome,
        [string]$Tipo,
        [int]$Segundos = 10
    )
    $limite = (Get-Date).AddSeconds($Segundos)
    while ($true) {
        $condicoes = New-Object System.Collections.Generic.List[System.Windows.Automation.Condition]
        if ($AutomationId) {
            $condicoes.Add((New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $AutomationId)))
        }
        if ($Nome) {
            $condicoes.Add((New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::NameProperty, $Nome)))
        }
        if ($Tipo) {
            $condicoes.Add((New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::ControlTypeProperty,
                [System.Windows.Automation.ControlType]::$Tipo)))
        }
        if ($condicoes.Count -eq 0) { throw 'Find-Elemento exige AutomationId, Nome ou Tipo.' }
        $cond = if ($condicoes.Count -eq 1) { $condicoes[0] }
                else { New-Object System.Windows.Automation.AndCondition($condicoes.ToArray()) }
        $achado = $Raiz.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
        if ($achado) { return $achado }
        if ((Get-Date) -ge $limite) { return $null }
        Start-Sleep -Milliseconds 100
    }
}

function Get-TextosDe($Elemento) {
    <#  Nomes dos descendentes de texto, em ordem da arvore (cards/slots tem Grid como conteudo). #>
    return @(Find-Elementos -Raiz $Elemento -Tipo 'Text' | ForEach-Object { $_.Current.Name })
}

function Get-TextoApos {
    <#  Texto que vem logo depois de um rotulo (ex.: "TOTAL DA COMANDA" -> "R$ 24,00"). #>
    param([Parameter(Mandatory = $true)]$Raiz, [Parameter(Mandatory = $true)][string]$Rotulo)
    $textos = @(Get-TextosDe $Raiz)
    for ($i = 0; $i -lt $textos.Count - 1; $i++) {
        if ($textos[$i] -eq $Rotulo) { return $textos[$i + 1] }
    }
    return $null
}

function Find-BotaoPorTexto {
    <#  Botao cujo conteudo e um Grid/StackPanel: casa pelos textos descendentes. #>
    param(
        [Parameter(Mandatory = $true)]$Raiz,
        [Parameter(Mandatory = $true)][string]$Texto,
        [switch]$Exato
    )
    foreach ($b in @(Find-Elementos -Raiz $Raiz -Tipo 'Button')) {
        if ($b.Current.Name -eq $Texto) { return $b }
        foreach ($t in @(Get-TextosDe $b)) {
            if ($Exato) { if ($t -eq $Texto) { return $b } }
            elseif ($t -and $t.Contains($Texto)) { return $b }
        }
    }
    return $null
}

# ---------------------------------------------------------------- padroes (nunca teclas)

function Invoke-Elemento($Elemento) {
    if (-not $Elemento) { throw 'Invoke-Elemento: elemento nulo.' }
    $padrao = $Elemento.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $padrao.Invoke()
}

function Set-ValorDoElemento($Elemento, [string]$Valor) {
    if (-not $Elemento) { throw 'Set-ValorDoElemento: elemento nulo.' }
    $padrao = $Elemento.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern)
    $padrao.SetValue($Valor)
}

function Select-Elemento($Elemento) {
    if (-not $Elemento) { throw 'Select-Elemento: elemento nulo.' }
    $padrao = $Elemento.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
    $padrao.Select()
}

function Get-EstadoDeMarcacao($Elemento) {
    $padrao = $Elemento.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    return $padrao.Current.ToggleState
}

function Close-Janela($Elemento) {
    <#  Fecha pela janela (WindowPattern), equivalente ao X: nenhuma tecla e enviada. #>
    if (-not $Elemento) { throw 'Close-Janela: elemento nulo.' }
    $padrao = $Elemento.GetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern)
    $padrao.Close()
}

# ---------------------------------------------------------------- evidencias

function Save-Captura {
    <#  Captura por PrintWindow (nao exige foco nem traz a janela para a frente). #>
    param([Parameter(Mandatory = $true)][IntPtr]$Hwnd, [Parameter(Mandatory = $true)][string]$Caminho)
    $r = New-Object VarthexJanelaNativa+RECT
    [void][VarthexJanelaNativa]::GetWindowRect($Hwnd, [ref]$r)
    $largura = $r.R - $r.L
    $altura = $r.B - $r.T
    if ($largura -le 0 -or $altura -le 0) { return $null }
    $bmp = New-Object System.Drawing.Bitmap $largura, $altura
    try {
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        try {
            $hdc = $g.GetHdc()
            [void][VarthexJanelaNativa]::PrintWindow($Hwnd, $hdc, 2)
            $g.ReleaseHdc($hdc)
        }
        finally { $g.Dispose() }
        $bmp.Save($Caminho, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $bmp.Dispose() }
    return $Caminho
}

function Get-ArquivoDeLog([string]$PastaDados) {
    $pasta = Join-Path $PastaDados 'logs'
    if (-not (Test-Path -LiteralPath $pasta)) { return $null }
    return (Get-ChildItem -LiteralPath $pasta -Filter 'varthex-comanda-*.log' -File |
        Sort-Object LastWriteTimeUtc | Select-Object -Last 1)
}

function Get-LinhasDoLog([string]$PastaDados, [string]$Padrao) {
    $arquivo = Get-ArquivoDeLog $PastaDados
    if (-not $arquivo) { return @() }
    $conteudo = Get-Content -LiteralPath $arquivo.FullName -Encoding UTF8 -ErrorAction SilentlyContinue
    if (-not $Padrao) { return @($conteudo) }
    return @($conteudo | Where-Object { $_ -match $Padrao })
}

function Get-HashDoArquivo([string]$Caminho) {
    <#
        SHA-256 tolerante a arquivo aberto por outro processo (FileShare.ReadWrite):
        o banco ativo fica aberto pelo aplicativo enquanto ele roda.
    #>
    if (-not (Test-Path -LiteralPath $Caminho)) { return $null }
    $fluxo = [IO.File]::Open($Caminho, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
    try {
        $algoritmo = [Security.Cryptography.SHA256]::Create()
        try { return ([BitConverter]::ToString($algoritmo.ComputeHash($fluxo)) -replace '-', '').ToLowerInvariant() }
        finally { $algoritmo.Dispose() }
    }
    finally { $fluxo.Dispose() }
}

function Hide-NomeDeUsuario([string]$Texto) {
    <#  O transcript e commitado: nunca pode conter o nome de usuario do Windows. #>
    if ($null -eq $Texto) { return $Texto }
    $saida = $Texto
    foreach ($par in @(
        @{ De = $env:USERPROFILE; Para = '%USERPROFILE%' },
        @{ De = $env:USERNAME;    Para = '%USERNAME%' })) {
        if ($par.De) {
            $saida = [regex]::Replace($saida, [regex]::Escape($par.De), $par.Para, 'IgnoreCase')
        }
    }
    return $saida
}
