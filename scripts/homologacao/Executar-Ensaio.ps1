<#
.SYNOPSIS
    Ensaios de homologacao com o aplicativo real (Etapa 8), por UI Automation.

.DESCRIPTION
    Instala o pacote publicado numa pasta temporaria, gera a massa minima numa
    pasta descartavel e executa os ensaios E1..E6 com o aplicativo de verdade,
    sem enviar teclas: toda interacao usa padroes do UI Automation.

    E1  RNF03  tempo de abertura ate a janela principal (media de N aberturas)
    E2  CT19   segunda instancia mostra o aviso e nao abre um segundo banco
    E3  CT20   termino forcado do processo: comanda e total sobrevivem
    E4  CT09   erro da maquininha: "Voltar para a comanda" nao gera venda
    E5         restaurar backup valido e recusa do backup corrompido
    E6  CT13   nenhuma conexao TCP/UDP remota do processo (prova de apoio)

    Cada ensaio imprime PASSOU/FALHOU com evidencia; o codigo de saida e
    diferente de zero se algum ensaio falhar.

    SEGURANCA (ver tambem Ajuda.ps1):
    * aborta se ja houver VarthexComanda em execucao (pode ser o app do usuario):
      Assert-SemAppEmExecucao roda antes de TODA abertura, exceto a segunda instancia
      do E2 (proposital: precisa haver uma primeira, propria, em execucao) e o reinicio
      automatico do E5 (feito pelo proprio app; validado pelo log 'Iniciando' na pasta
      de dados descartavel antes de qualquer interacao);
    * so encerra PIDs que este script iniciou;
    * o app so roda com VARTHEX_COMANDA_DADOS numa pasta descartavel dentro de
      -Saida, conferida antes de cada abertura;
    * nunca le nem escreve em %LOCALAPPDATA%\VarthexComanda;
    * nenhuma tecla e enviada e o foco nunca e roubado.

.PARAMETER Saida
    Raiz descartavel do ensaio (instalacao temporaria, massa e dados). Tudo que
    o script cria fica aqui dentro.

.PARAMETER Ensaios
    Quais ensaios executar (padrao: todos). Ex.: -Ensaios E1,E3

.PARAMETER VezesAbertura
    Quantas aberturas o E1 cronometra (padrao: 5).

.PARAMETER Transcript
    Arquivo do transcript (padrao: docs\homologacao\evidencias\ensaio-<hoje>.txt).
    O nome de usuario do Windows e mascarado antes de gravar.

.PARAMETER ManterAmbiente
    Nao remove a instalacao temporaria e as pastas de dados ao final.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\homologacao\Executar-Ensaio.ps1 -Saida C:\Temp\ensaio
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Saida,
    [string[]]$Ensaios = @('E1', 'E2', 'E3', 'E4', 'E5', 'E6'),
    [int]$VezesAbertura = 5,
    [string]$Transcript,
    [switch]$ManterAmbiente
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. "$PSScriptRoot\Ajuda.ps1"

# -File passa "-Ensaios E1,E2" como uma unica string; aceitamos as duas formas.
$Ensaios = @($Ensaios | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })

$raizRepo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $Transcript) {
    $Transcript = Join-Path $raizRepo ("docs\homologacao\evidencias\ensaio-{0}.txt" -f (Get-Date -Format 'yyyy-MM-dd'))
}

$script:Linhas = New-Object System.Collections.Generic.List[string]
$script:Resultados = New-Object System.Collections.Generic.List[object]

function Registrar([string]$texto = '') {
    foreach ($linha in ($texto -split "`r?`n")) {
        [void]$script:Linhas.Add($linha)
        Write-Host $linha
    }
}

function Titulo([string]$texto) {
    Registrar ''
    Registrar ('=' * 72)
    Registrar $texto
    Registrar ('=' * 72)
}

function Concluir([string]$ensaio, [string]$descricao, [bool]$passou, [string]$evidencia, [string]$situacao) {
    if (-not $situacao) { $situacao = if ($passou) { 'PASSOU' } else { 'FALHOU' } }
    Registrar ''
    Registrar ("{0}: {1} -- {2}" -f $ensaio, $situacao, $evidencia)
    [void]$script:Resultados.Add([pscustomobject]@{
            Ensaio    = $ensaio
            Descricao = $descricao
            Situacao  = $situacao
            Evidencia = $evidencia
        })
}

# ---------------------------------------------------------------- utilidades da tela

function Get-Slots($janela) {
    $slots = @()
    foreach ($b in @(Find-Elementos -Raiz $janela -Tipo 'Button')) {
        $t = @(Get-TextosDe $b)
        if ($t.Count -ge 4 -and $t[0] -eq 'COMANDA') {
            $slots += [pscustomobject]@{ Numero = $t[2]; Situacao = $t[3]; Botao = $b }
        }
    }
    return $slots
}

function Wait-Slots($janela, [int]$Segundos = 20) {
    $limite = (Get-Date).AddSeconds($Segundos)
    while ((Get-Date) -lt $limite) {
        $slots = @(Get-Slots $janela)
        if ($slots.Count -gt 0) { return $slots }
        Start-Sleep -Milliseconds 200
    }
    throw 'A grade de comandas nao apareceu.'
}

function Open-ComandaLivre($janela) {
    $slots = @(Wait-Slots $janela)
    $livre = @($slots | Where-Object { $_.Situacao -eq 'LIVRE' })[0]
    if (-not $livre) { throw 'Nenhuma comanda livre na grade.' }
    Invoke-Elemento $livre.Botao
    Start-Sleep -Milliseconds 700
    return $livre.Numero
}

function Add-Produtos($janela, [string[]]$Nomes) {
    foreach ($nome in $Nomes) {
        $card = Find-BotaoPorTexto -Raiz $janela -Texto $nome -Exato
        if (-not $card) { throw "Card do produto '$nome' nao encontrado." }
        Invoke-Elemento $card
        Start-Sleep -Milliseconds 400
    }
}

function Get-TotalDaComanda($janela) {
    return (Get-TextoApos -Raiz $janela -Rotulo 'TOTAL DA COMANDA')
}

function Get-VendasDeHoje($janela) {
    <# Conta as vendas do dia na tela de Historico (a massa so tem vendas ate ontem). #>
    Invoke-Elemento (Find-Elemento -Raiz $janela -AutomationId 'BotaoHistorico')
    Start-Sleep -Milliseconds 1200
    $quantidade = Get-TextoApos -Raiz $janela -Rotulo 'Vendas: '
    Invoke-Elemento (Find-Elemento -Raiz $janela -AutomationId 'BotaoAtendimento')
    Start-Sleep -Milliseconds 800
    return $quantidade
}

function Close-App($processo, [int]$Segundos = 20) {
    <# Fecha pela janela (WindowPattern); se nao sair, encerra o PID proprio. #>
    try {
        $janela = Wait-JanelaPrincipal -Processo $processo -Segundos 5
        Close-Janela $janela
    }
    catch { }
    if (-not (Wait-SaidaDoProcesso $processo.Id $Segundos)) {
        [void](Stop-AppProprio -ProcessId $processo.Id -Forcado)
        return $false
    }
    return $true
}

function New-PastaDeDados([string]$nome) {
    <#
        Cada ensaio roda o gerador na sua propria pasta descartavel (leva ~8 s): assim
        os caminhos gravados no registro de backups sao os da pasta do ensaio, e um
        ensaio nunca herda o estado deixado por outro.
    #>
    $destino = Assert-PastaDescartavel (Join-Path $Saida $nome) $Saida
    if (Test-Path -LiteralPath $destino) { Remove-Item -LiteralPath $destino -Recurse -Force }
    $cronometro = [Diagnostics.Stopwatch]::StartNew()
    $saidaGerador = & $script:Dotnet run --project $script:ProjetoMassa -c Release -- --saida $destino 2>&1
    $cronometro.Stop()
    if ($LASTEXITCODE -ne 0) { throw "Gerador da massa minima falhou: $($saidaGerador -join ' ')" }
    Registrar ("Massa minima gerada para este ensaio em {0:N1} s: {1}" -f $cronometro.Elapsed.TotalSeconds, $destino)
    return [pscustomobject]@{ Pasta = $destino; Saida = @($saidaGerador) }
}

# ================================================================ ensaios

function Ensaio-E1 {
    Titulo "E1 (RNF03) - tempo de abertura ate a janela principal, media de $VezesAbertura aberturas"
    $dados = (New-PastaDeDados 'dados-E1').Pasta
    Registrar 'Cronometro: do Process.Start ate MainWindowHandle visivel com o titulo "Varthex Comanda".'
    $tempos = @()
    for ($i = 1; $i -le $VezesAbertura; $i++) {
        Assert-SemAppEmExecucao
        $cronometro = [Diagnostics.Stopwatch]::StartNew()
        $processo = Start-App -Exe $script:Exe -PastaDados $dados -RaizPermitida $Saida
        [void](Wait-JanelaPrincipal -Processo $processo)
        $cronometro.Stop()
        $ms = $cronometro.ElapsedMilliseconds
        $tempos += $ms
        Registrar ("  abertura {0}: {1} ms (PID {2})" -f $i, $ms, $processo.Id)
        [void](Close-App $processo)
        Start-Sleep -Milliseconds 700
    }
    $media = [math]::Round(($tempos | Measure-Object -Average).Average, 0)
    $maior = ($tempos | Measure-Object -Maximum).Maximum
    Registrar ("  media: {0} ms | maior: {1} ms | meta RNF03: <= 5000 ms" -f $media, $maior)
    $passou = $media -le 5000
    Concluir 'E1' 'RNF03 - abertura <= 5 s' $passou ("media {0} ms em {1} aberturas (maior {2} ms)" -f $media, $VezesAbertura, $maior) $null
}

function Ensaio-E2 {
    Titulo 'E2 (CT19) - segunda instancia mostra o aviso e nao abre um segundo banco'
    $dados = (New-PastaDeDados 'dados-E2').Pasta
    Assert-SemAppEmExecucao
    $primeiro = Start-App -Exe $script:Exe -PastaDados $dados -RaizPermitida $Saida
    [void](Wait-JanelaPrincipal -Processo $primeiro)
    Registrar "  1a instancia: PID $($primeiro.Id) com a janela principal aberta"

    $segundo = Start-App -Exe $script:Exe -PastaDados $dados -RaizPermitida $Saida
    Registrar "  2a instancia: PID $($segundo.Id) iniciada com as mesmas variaveis"
    $aviso = Wait-Janela -ProcessId $segundo.Id -Titulo 'Varthex Comanda' -ContendoTexto 'aberto neste computador' -Segundos 15
    $textoAviso = ''
    if ($aviso) { $textoAviso = (@(Get-TextosDe $aviso) | Where-Object { $_ -and $_.Length -gt 10 }) -join ' ' }
    Registrar "  aviso da 2a instancia: '$textoAviso'"

    $mensagemEsperada = 'O Varthex Comanda já está aberto neste computador.'
    $textoConfere = $textoAviso -like "*$mensagemEsperada*"

    $janelasPrincipais = @()
    foreach ($j in @(Get-JanelasDoProcesso $primeiro.Id)) {
        if ($j.Current.Name -eq 'Varthex Comanda' -and $j.Current.ClassName -eq 'Window') { $janelasPrincipais += $j }
    }
    Registrar "  janelas principais vivas na 1a instancia: $($janelasPrincipais.Count)"

    $msAteSair = -1
    if ($aviso) {
        $ok = Find-Elemento -Raiz $aviso -Nome 'OK' -Tipo 'Button' -Segundos 5
        Registrar '  o aviso e modal (ShowDialog): a 2a instancia so encerra depois do OK'
        $cronometro = [Diagnostics.Stopwatch]::StartNew()
        Invoke-Elemento $ok
        $saiu = Wait-SaidaDoProcesso $segundo.Id 10
        $cronometro.Stop()
        $msAteSair = $cronometro.ElapsedMilliseconds
        Registrar ("  apos o OK (InvokePattern): encerrou={0} em {1} ms" -f $saiu, $msAteSair)
    }
    else {
        $saiu = $false
    }

    $inicios = @(Get-LinhasDoLog $dados 'Iniciando Varthex Comanda')
    $bancos = @(Get-LinhasDoLog $dados 'Banco pronto')
    Registrar "  log: 'Iniciando Varthex Comanda' = $($inicios.Count) | 'Banco pronto' = $($bancos.Count)"
    foreach ($l in $inicios) { Registrar "    $l" }

    [void](Close-App $primeiro)

    $passou = $aviso -and $textoConfere -and $saiu -and $msAteSair -le 10000 -and
        $janelasPrincipais.Count -eq 1 -and $inicios.Count -eq 1 -and $bancos.Count -eq 1
    Concluir 'E2' 'CT19 - instancia unica' $passou (
        "aviso com a mensagem esperada; 1 janela principal; 2a instancia encerrou em $msAteSair ms apos o OK; log com 1 abertura de banco") $null
}

function Ensaio-E3 {
    Titulo 'E3 (CT20/RNF20) - termino forcado do processo: comanda e total sobrevivem'
    $dados = (New-PastaDeDados 'dados-E3').Pasta
    Assert-SemAppEmExecucao
    $processo = Start-App -Exe $script:Exe -PastaDados $dados -RaizPermitida $Saida
    $janela = Wait-JanelaPrincipal -Processo $processo
    Start-Sleep -Milliseconds 1200

    $abertasAntes = @(Get-Slots $janela | Where-Object { $_.Situacao -ne 'LIVRE' }).Count
    $vendasAntes = Get-VendasDeHoje $janela
    $numero = Open-ComandaLivre $janela
    Add-Produtos $janela @('X-Burger', 'Refrigerante Lata')
    $totalAntes = Get-TotalDaComanda $janela
    Registrar "  comanda $numero aberta com 2 itens; total na tela: $totalAntes"
    Registrar "  comandas abertas antes: $abertasAntes | vendas de hoje antes: $vendasAntes"

    Registrar "  Stop-Process -Force no PID $($processo.Id) (termino forcado, sem OnExit)"
    [void](Stop-AppProprio -ProcessId $processo.Id -Forcado)
    if (-not (Wait-SaidaDoProcesso $processo.Id 15)) { throw "O PID proprio $($processo.Id) nao encerrou apos o termino forcado." }
    Start-Sleep -Milliseconds 800

    # o processo proprio saiu: qualquer VarthexComanda que apareca agora nao e nosso -> aborta
    Assert-SemAppEmExecucao
    $processo2 = Start-App -Exe $script:Exe -PastaDados $dados -RaizPermitida $Saida
    $janela2 = Wait-JanelaPrincipal -Processo $processo2
    Start-Sleep -Milliseconds 1200
    $slots = @(Wait-Slots $janela2)
    $slot = @($slots | Where-Object { $_.Numero -eq $numero })[0]
    $totalDepois = if ($slot) { $slot.Situacao } else { '(slot nao encontrado)' }
    $vendasDepois = Get-VendasDeHoje $janela2
    $recuperadas = @(Get-LinhasDoLog $dados 'Comandas abertas recuperadas')
    $ultima = if ($recuperadas.Count -gt 0) { $recuperadas[-1] } else { '(sem linha no log)' }
    $esperado = $abertasAntes + 1
    Registrar "  apos reabrir: comanda $numero mostra '$totalDepois' | vendas de hoje: $vendasDepois"
    Registrar "  log: $ultima  (esperado: $esperado)"
    [void](Close-App $processo2)

    $passou = ($totalDepois -eq $totalAntes) -and ($vendasDepois -eq $vendasAntes) -and
        ($ultima -match ("Comandas abertas recuperadas: {0}$" -f $esperado))
    Concluir 'E3' 'CT20 - nada se perde no termino forcado' $passou (
        "comanda $numero continua aberta com $totalDepois; log 'Comandas abertas recuperadas: $esperado'; vendas de hoje seguem em $vendasDepois") $null
}

function Ensaio-E4 {
    Titulo 'E4 (CT09) - erro da maquininha: "Voltar para a comanda" nao gera venda'
    $dados = (New-PastaDeDados 'dados-E4').Pasta
    Assert-SemAppEmExecucao
    $processo = Start-App -Exe $script:Exe -PastaDados $dados -RaizPermitida $Saida
    $janela = Wait-JanelaPrincipal -Processo $processo
    Start-Sleep -Milliseconds 1200

    $vendasAntes = Get-VendasDeHoje $janela
    Registrar "  metodo da verificacao: a massa so tem vendas ate ontem, entao o contador"
    Registrar "  'Vendas:' da tela de Historico (data = hoje) isola o que este ensaio criar."
    $numero = Open-ComandaLivre $janela
    Add-Produtos $janela @('X-Burger', 'Refrigerante Lata')
    $totalAntes = Get-TotalDaComanda $janela
    Registrar "  comanda $numero com 2 itens; total: $totalAntes | vendas de hoje antes: $vendasAntes"

    Invoke-Elemento (Find-BotaoPorTexto -Raiz $janela -Texto 'Finalizar comanda')
    $encerramento = Wait-Janela -ProcessId $processo.Id -Titulo 'Encerrar comanda' -Segundos 15
    if (-not $encerramento) { throw 'A janela "Encerrar comanda" nao apareceu.' }
    $caixa = Find-Elemento -Raiz $encerramento -AutomationId 'CaixaCobrancaAprovada' -Segundos 5
    $estado = Get-EstadoDeMarcacao $caixa
    $totalAPagar = Get-TextoApos -Raiz $encerramento -Rotulo 'Total a pagar: '
    Registrar "  janela 'Encerrar comanda': total a pagar $totalAPagar; caixa 'cobranca aprovada' = $estado (nao marcamos)"

    Invoke-Elemento (Find-Elemento -Raiz $encerramento -Nome 'Voltar para a comanda' -Tipo 'Button' -Segundos 5)
    Start-Sleep -Milliseconds 1200
    $aindaAberta = Wait-Janela -ProcessId $processo.Id -Titulo 'Encerrar comanda' -Segundos 3
    $totalDepois = Get-TotalDaComanda $janela
    $vendasDepois = Get-VendasDeHoje $janela
    # volta da edicao para a grade para conferir o slot da comanda
    Invoke-Elemento (Find-Elemento -Raiz $janela -Nome 'Voltar' -Tipo 'Button' -Segundos 5)
    Start-Sleep -Milliseconds 900
    $slots = @(Wait-Slots $janela)
    $slot = @($slots | Where-Object { $_.Numero -eq $numero })[0]
    $situacaoSlot = if ($slot) { $slot.Situacao } else { '(slot nao encontrado)' }
    Registrar "  apos 'Voltar para a comanda': janela de encerramento fechada = $($null -eq $aindaAberta)"
    Registrar "  total da comanda: $totalDepois | slot $numero na grade: $situacaoSlot | vendas de hoje: $vendasDepois"
    [void](Close-App $processo)

    $passou = ($null -eq $aindaAberta) -and ($estado -eq 'Off') -and ($totalDepois -eq $totalAntes) -and
        ($situacaoSlot -eq $totalAntes) -and ($vendasDepois -eq $vendasAntes)
    Concluir 'E4' 'CT09 - cobranca nao aprovada nao encerra a comanda' $passou (
        "comanda $numero continua aberta com $totalDepois e as vendas de hoje seguem em $vendasDepois (nenhuma venda nova)") $null
}

function Ensaio-E5 {
    Titulo 'E5 - restaurar o backup valido e recusar o backup corrompido'
    $geracao = New-PastaDeDados 'dados-E5'
    $dados = $geracao.Pasta
    $nomeValido = $null
    $nomeCorrompido = $null
    foreach ($linha in $geracao.Saida) {
        if ($linha -match 'valido:\s+(\S+\.db)') { $nomeValido = $Matches[1] }
        if ($linha -match 'corrompido:\s+(\S+\.db)') { $nomeCorrompido = $Matches[1] }
    }
    Registrar "  backup valido:     $nomeValido"
    Registrar "  backup corrompido: $nomeCorrompido"
    $bancoAtivo = Join-Path $dados 'data\varthex-comanda.db'

    # --- parte A: mudar o estado e restaurar o backup valido pela tela de Backup
    Assert-SemAppEmExecucao
    $processo = Start-App -Exe $script:Exe -PastaDados $dados -RaizPermitida $Saida
    $janela = Wait-JanelaPrincipal -Processo $processo
    Start-Sleep -Milliseconds 1200
    $abertasNaMassa = @(Get-Slots $janela | Where-Object { $_.Situacao -ne 'LIVRE' }).Count
    $numero = Open-ComandaLivre $janela
    Add-Produtos $janela @('X-Burger')
    $totalNovo = Get-TotalDaComanda $janela
    Registrar "  estado alterado: comanda $numero aberta com $totalNovo (a massa tinha $abertasNaMassa comandas abertas)"

    Invoke-Elemento (Find-Elemento -Raiz $janela -AutomationId 'BotaoBackup')
    Start-Sleep -Milliseconds 1500
    $linha = $null
    foreach ($item in @(Find-Elementos -Raiz $janela -Tipo 'DataItem')) {
        $t = @(Get-TextosDe $item)
        if ($t.Count -gt 0 -and $t[0] -eq $nomeValido) { $linha = $item; break }
    }
    if (-not $linha) { throw "O backup valido '$nomeValido' nao aparece na lista da tela de Backup." }
    Select-Elemento $linha
    Start-Sleep -Milliseconds 500
    Registrar "  linha selecionada na lista: $((@(Get-TextosDe $linha))[0..2] -join ' | ')"

    Invoke-Elemento (Find-Elemento -Raiz $janela -Nome 'Restaurar backup selecionado' -Tipo 'Button')
    $confirmacao = Wait-Janela -ProcessId $processo.Id -Titulo 'Restaurar backup' -Segundos 15
    if (-not $confirmacao) { throw 'A confirmacao "Restaurar backup" nao apareceu.' }
    Registrar "  confirmacao: $((@(Get-TextosDe $confirmacao) | Where-Object { $_.Length -gt 20 }) -join ' ')"
    $iniciosAntesDoReinicio = @(Get-LinhasDoLog $dados 'Iniciando Varthex Comanda').Count
    Invoke-Elemento (Find-Elemento -Raiz $confirmacao -Nome 'Sim' -Tipo 'Button' -Segundos 5)

    $avisoOk = Wait-Janela -ProcessId $processo.Id -Titulo 'Varthex Comanda' -ContendoTexto 'Backup restaurado' -Segundos 30
    if (-not $avisoOk) { throw 'O aviso "Backup restaurado com sucesso" nao apareceu.' }
    Registrar "  aviso: $((@(Get-TextosDe $avisoOk) | Where-Object { $_.Length -gt 20 }) -join ' ')"
    Invoke-Elemento (Find-Elemento -Raiz $avisoOk -Nome 'OK' -Tipo 'Button' -Segundos 5)
    [void](Wait-SaidaDoProcesso $processo.Id 20)

    # o proprio app se reinicia (App.ReiniciarAplicativo); registramos o processo novo
    $reiniciado = $null
    $limite = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $limite -and -not $reiniciado) {
        $novos = @(Register-ProcessosDoExe)
        if ($novos.Count -gt 0) { $reiniciado = $novos[0] }
        Start-Sleep -Milliseconds 300
    }
    if (-not $reiniciado) { throw 'O aplicativo nao reiniciou sozinho apos a restauracao.' }
    Registrar "  o app reiniciou sozinho: PID $($reiniciado.Id)"
    # antes de interagir: o processo reiniciado tem de estar nos dados DESCARTAVEIS (e nao nos reais da loja).
    # Prova: uma linha NOVA 'Iniciando Varthex Comanda' apareceu em <dados>\logs depois do reinicio.
    $iniciou = $false
    $limiteLog = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $limiteLog -and -not $iniciou) {
        $iniciou = @(Get-LinhasDoLog $dados 'Iniciando Varthex Comanda').Count -gt $iniciosAntesDoReinicio
        if (-not $iniciou) { Start-Sleep -Milliseconds 300 }
    }
    if (-not $iniciou) {
        throw 'O app reiniciado nao registrou "Iniciando" no log da pasta descartavel: nao ha prova de que usa os dados do ensaio. Abortado sem interagir.'
    }
    Registrar "  o reinicio esta nos dados descartaveis: 'Iniciando Varthex Comanda' passou de $iniciosAntesDoReinicio para $(@(Get-LinhasDoLog $dados 'Iniciando Varthex Comanda').Count) no log de <dados>\logs"
    $janela2 = Wait-JanelaPrincipal -Processo $reiniciado -Segundos 40
    Start-Sleep -Milliseconds 1200
    $slots = @(Wait-Slots $janela2)
    $abertasDepois = @($slots | Where-Object { $_.Situacao -ne 'LIVRE' }).Count
    $slotRestaurado = @($slots | Where-Object { $_.Numero -eq $numero })[0]
    $situacaoSlot = if ($slotRestaurado) { $slotRestaurado.Situacao } else { '(slot nao encontrado)' }
    Registrar "  apos a restauracao: $abertasDepois comandas abertas (massa: $abertasNaMassa); comanda $numero = $situacaoSlot"
    [void](Close-App $reiniciado)
    $parteA = ($abertasDepois -eq $abertasNaMassa) -and ($situacaoSlot -eq 'LIVRE')

    # --- parte B: banco ativo corrompido -> modo de restauracao -> recusa do backup corrompido
    Registrar ''
    Registrar '  Parte B: o backup corrompido nao e oferecido na lista normal (so backups registrados).'
    Registrar '  Para exercitar a recusa pela interface sem caixa de dialogo de arquivo, o banco ATIVO'
    Registrar '  da pasta descartavel e corrompido de proposito: o app abre no modo de restauracao,'
    Registrar '  que lista os arquivos da pasta backups\ (inclusive o corrompido).'
    $lixo = New-Object byte[] 8192
    (New-Object System.Random 7).NextBytes($lixo)
    [IO.File]::WriteAllBytes($bancoAtivo, $lixo)
    $hashAntes = Get-HashDoArquivo $bancoAtivo
    Registrar "  banco ativo corrompido de proposito; sha256 = $hashAntes"

    Assert-SemAppEmExecucao
    $processo3 = Start-App -Exe $script:Exe -PastaDados $dados -RaizPermitida $Saida
    $janela3 = Wait-JanelaPrincipal -Processo $processo3 -Segundos 40
    Start-Sleep -Milliseconds 1500
    $faixa = @(Get-TextosDe $janela3) | Where-Object { $_ -match 'corrompido' }
    Registrar "  faixa na tela: $($faixa -join ' ')"

    $linhaCorrompida = $null
    foreach ($item in @(Find-Elementos -Raiz $janela3 -Tipo 'DataItem')) {
        $t = @(Get-TextosDe $item)
        if ($t.Count -gt 0 -and $t[0] -eq $nomeCorrompido) { $linhaCorrompida = $item; break }
    }
    if (-not $linhaCorrompida) { throw "O backup corrompido '$nomeCorrompido' nao aparece na lista do modo de restauracao." }
    Select-Elemento $linhaCorrompida
    Start-Sleep -Milliseconds 400
    Invoke-Elemento (Find-Elemento -Raiz $janela3 -Nome 'Restaurar backup selecionado' -Tipo 'Button')
    $confirmacao2 = Wait-Janela -ProcessId $processo3.Id -Titulo 'Restaurar backup' -Segundos 15
    if (-not $confirmacao2) { throw 'A confirmacao "Restaurar backup" (corrompido) nao apareceu.' }
    Invoke-Elemento (Find-Elemento -Raiz $confirmacao2 -Nome 'Sim' -Tipo 'Button' -Segundos 5)
    Start-Sleep -Seconds 3

    $recusa = @(@(Get-TextosDe $janela3) | Where-Object {
            $_ -like '*não é um banco*' -or $_ -like '*Arquivo não encontrado*' -or $_ -like '*está corrompido (falhou*'
        })
    $hashDepois = Get-HashDoArquivo $bancoAtivo
    $aindaVivo = $null -ne (Get-Process -Id $processo3.Id -ErrorAction SilentlyContinue)
    Registrar "  mensagem de recusa na tela: $($recusa -join ' // ')"
    Registrar "  sha256 do banco ativo depois: $hashDepois (inalterado = $($hashAntes -eq $hashDepois))"
    Registrar "  o app continuou aberto no modo de restauracao: $aindaVivo"
    [void](Close-App $processo3)

    $parteB = ($hashAntes -eq $hashDepois) -and $aindaVivo -and ($recusa.Count -gt 0)
    $passou = $parteA -and $parteB
    Concluir 'E5' 'Restauracao de backup (valido e corrompido)' $passou (
        "valido restaurado pela tela (o app reiniciou e voltou a $abertasDepois comandas abertas); corrompido recusado com o banco ativo intacto (sha256 igual)") $null
}

function Ensaio-E6 {
    Titulo 'E6 (CT13, apoio) - o processo nao abre conexoes de rede'
    $dados = (New-PastaDeDados 'dados-E6').Pasta
    Registrar 'ATENCAO: esta e uma evidencia de APOIO. Nao substitui o ensaio com a rede'
    Registrar 'fisica desligada no computador da loja (CT13 fica como teste manual).'
    Assert-SemAppEmExecucao
    $processo = Start-App -Exe $script:Exe -PastaDados $dados -RaizPermitida $Saida
    $janela = Wait-JanelaPrincipal -Processo $processo
    Start-Sleep -Milliseconds 1200

    # exercita as telas para o caso de alguma delas tentar rede
    foreach ($tela in 'BotaoProdutos', 'BotaoHistorico', 'BotaoBackup', 'BotaoConfiguracoes', 'BotaoAtendimento') {
        Invoke-Elemento (Find-Elemento -Raiz $janela -AutomationId $tela)
        Start-Sleep -Milliseconds 800
    }
    $numero = Open-ComandaLivre $janela
    Add-Produtos $janela @('Café Expresso')
    Registrar "  sessao exercitada (5 telas + comanda $numero com 1 item)"

    $locais = @('0.0.0.0', '::', '127.0.0.1', '::1')
    $tcp = @(Get-NetTCPConnection -OwningProcess $processo.Id -ErrorAction SilentlyContinue)
    $udp = @(Get-NetUDPEndpoint -OwningProcess $processo.Id -ErrorAction SilentlyContinue)
    $remotas = @($tcp | Where-Object { $locais -notcontains $_.RemoteAddress })
    Registrar "  Get-NetTCPConnection -OwningProcess $($processo.Id): $($tcp.Count) conexao(oes); remotas: $($remotas.Count)"
    foreach ($c in $tcp) { Registrar "    $($c.LocalAddress):$($c.LocalPort) -> $($c.RemoteAddress):$($c.RemotePort) [$($c.State)]" }
    Registrar "  Get-NetUDPEndpoint -OwningProcess $($processo.Id): $($udp.Count) ponto(s)"
    foreach ($u in $udp) { Registrar "    $($u.LocalAddress):$($u.LocalPort)" }
    $linhasNetstat = @(& netstat -ano | Select-String ("\s{0}$" -f $processo.Id))
    Registrar "  netstat -ano com o PID $($processo.Id): $($linhasNetstat.Count) linha(s)"
    foreach ($l in $linhasNetstat) { Registrar "    $($l.ToString().Trim())" }

    [void](Close-App $processo)
    $passou = ($remotas.Count -eq 0) -and ($udp.Count -eq 0)
    Concluir 'E6' 'CT13 (apoio) - sem conexoes remotas' $passou (
        "nenhuma conexao TCP remota e nenhum ponto UDP do PID durante a sessao (evidencia de apoio; nao substitui o teste sem rede fisica)") $null
}

# ================================================================ preparo e execucao

$codigoDeSaida = 0
try {
    Titulo 'Ensaios de homologacao do Varthex Comanda (app real, UI Automation)'
    Registrar ("Data/hora ........ {0}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz'))
    Registrar ("Maquina .......... {0} ({1})" -f $env:COMPUTERNAME, [Environment]::OSVersion.VersionString)
    Registrar ("PowerShell ....... {0}" -f $PSVersionTable.PSVersion)
    Registrar ("Ensaios .......... {0}" -f ($Ensaios -join ', '))

    $Saida = Assert-PastaDescartavel $Saida $null
    if (-not (Test-Path -LiteralPath $Saida)) { [void](New-Item -ItemType Directory -Path $Saida -Force) }
    Registrar ("Raiz descartavel . {0}" -f $Saida)

    if ($env:VARTHEX_COMANDA_DADOS) {
        throw 'VARTHEX_COMANDA_DADOS esta definida neste processo. O script so define a variavel no processo filho; limpe-a antes de continuar.'
    }
    Assert-SemAppEmExecucao
    Registrar 'Trava: nenhum VarthexComanda em execucao antes de comecar.'

    $script:Dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue)
    if ($script:Dotnet) { $script:Dotnet = $script:Dotnet.Source }
    else { $script:Dotnet = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe' }
    $script:ProjetoMassa = Join-Path $raizRepo 'backend\tools\VarthexComanda.MassaMinima\VarthexComanda.MassaMinima.csproj'

    # 1) pacote publicado
    $pastaArtefatos = Join-Path $raizRepo 'artifacts'
    $zip = @(Get-ChildItem -LiteralPath $pastaArtefatos -Filter 'VarthexComanda-*-win-x64.zip' -File -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTimeUtc | Select-Object -Last 1)
    if ($zip.Count -eq 0) {
        Registrar 'Pacote nao encontrado em artifacts\; publicando com scripts\publicar.ps1 ...'
        & (Join-Path $raizRepo 'scripts\publicar.ps1') | Out-Null
        $zip = @(Get-ChildItem -LiteralPath $pastaArtefatos -Filter 'VarthexComanda-*-win-x64.zip' -File |
                Sort-Object LastWriteTimeUtc | Select-Object -Last 1)
    }
    $arquivoZip = $zip[0].FullName
    Registrar ("Pacote ........... {0} ({1:N1} MB)" -f $zip[0].Name, ($zip[0].Length / 1MB))

    # 2) instalacao temporaria
    $pacote = Assert-PastaDescartavel (Join-Path $Saida 'pacote') $Saida
    $instalacao = Assert-PastaDescartavel (Join-Path $Saida 'app\VarthexComanda') $Saida
    if (Test-Path -LiteralPath $pacote) { Remove-Item -LiteralPath $pacote -Recurse -Force }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($arquivoZip, $pacote)
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $pacote 'Instalar.ps1') -Destino $instalacao -SemAtalhos | ForEach-Object { Registrar "  $_" }
    if ($LASTEXITCODE -ne 0) { throw "Instalar.ps1 falhou (codigo $LASTEXITCODE)." }
    $script:Exe = Join-Path $instalacao 'VarthexComanda.exe'
    if (-not (Test-Path -LiteralPath $script:Exe)) { throw "Executavel nao encontrado apos instalar: $($script:Exe)" }
    Set-ExeAutorizado $script:Exe
    $versao = (Get-Item -LiteralPath $script:Exe).VersionInfo.ProductVersion
    Registrar ("Versao ........... {0}" -f $versao)
    Registrar ("Instalacao ....... {0}" -f $instalacao)

    # 3) massa minima (base copiada para os ensaios que nao dependem dos caminhos do registro)
    $script:MassaBase = Assert-PastaDescartavel (Join-Path $Saida 'massa-base') $Saida
    if (Test-Path -LiteralPath $script:MassaBase) { Remove-Item -LiteralPath $script:MassaBase -Recurse -Force }
    $cronometro = [Diagnostics.Stopwatch]::StartNew()
    $saidaMassa = & $script:Dotnet run --project $script:ProjetoMassa -c Release -- --saida $script:MassaBase 2>&1
    $cronometro.Stop()
    if ($LASTEXITCODE -ne 0) { throw "Gerador da massa minima falhou: $($saidaMassa -join ' ')" }
    Registrar ("Massa de referencia gerada em {0:N1} s (cada ensaio gera a sua propria pasta):" -f ($cronometro.Elapsed.TotalSeconds))
    foreach ($l in $saidaMassa) { Registrar "  $l" }

    foreach ($nome in $Ensaios) {
        switch ($nome.ToUpperInvariant()) {
            'E1' { Ensaio-E1 }
            'E2' { Ensaio-E2 }
            'E3' { Ensaio-E3 }
            'E4' { Ensaio-E4 }
            'E5' { Ensaio-E5 }
            'E6' { Ensaio-E6 }
            default { throw "Ensaio desconhecido: $nome" }
        }
    }
}
catch {
    Registrar ''
    Registrar "ERRO: $($_.Exception.Message)"
    Registrar ($_.ScriptStackTrace)
    $codigoDeSaida = 2
}
finally {
    Stop-TodosOsProprios
    try { Assert-SemAppEmExecucao }
    catch { Registrar "AVISO na limpeza: $($_.Exception.Message)" }

    Titulo 'Resumo'
    Registrar ("{0,-6} {1,-52} {2}" -f 'Ensaio', 'O que foi verificado', 'Resultado')
    Registrar ('-' * 72)
    foreach ($r in $script:Resultados) {
        Registrar ("{0,-6} {1,-52} {2}" -f $r.Ensaio, $r.Descricao, $r.Situacao)
    }
    Registrar ('-' * 72)
    foreach ($r in $script:Resultados) {
        Registrar ("{0}: {1}" -f $r.Ensaio, $r.Evidencia)
    }
    $falhas = @($script:Resultados | Where-Object { $_.Situacao -eq 'FALHOU' })
    if ($falhas.Count -gt 0 -and $codigoDeSaida -eq 0) { $codigoDeSaida = 1 }

    Registrar ''
    Registrar 'Nao automatizado neste script (motivo honesto, sem fingir aprovacao):'
    Registrar '  * E5 pela caixa "Selecionar arquivo...": o dialogo de arquivos do Windows expoe os'
    Registrar '    controles (nome do arquivo, botao Abrir) SEM padroes de UI Automation -'
    Registrar '    GetSupportedPatterns() volta vazio -, e preencher/clicar exigiria teclas ou'
    Registrar '    mensagens Win32, proibidas aqui. A recusa do backup corrompido foi verificada'
    Registrar '    pelo modo de restauracao, que lista os arquivos da pasta backups\.'
    Registrar '  * CT13 sem rede fisica, CT21 em Windows limpo e o teste com a maquininha real'
    Registrar '    continuam manuais (ver docs/homologacao).'

    if (-not $ManterAmbiente) {
        Registrar ''
        Registrar 'Limpeza: removendo a instalacao temporaria e as pastas de dados descartaveis.'
        foreach ($nome in @('pacote', 'app', 'massa-base', 'dados-E1', 'dados-E2', 'dados-E3', 'dados-E4', 'dados-E5', 'dados-E6')) {
            $caminho = Join-Path $Saida $nome
            try {
                $seguro = Assert-PastaDescartavel $caminho $Saida
                if (Test-Path -LiteralPath $seguro) { Remove-Item -LiteralPath $seguro -Recurse -Force -ErrorAction Stop }
            }
            catch { Registrar "  nao removido ($nome): $($_.Exception.Message)" }
        }
    }

    Registrar ''
    Registrar ("Codigo de saida: {0}" -f $codigoDeSaida)

    $pastaTranscript = Split-Path -Parent $Transcript
    if ($pastaTranscript -and -not (Test-Path -LiteralPath $pastaTranscript)) {
        [void](New-Item -ItemType Directory -Path $pastaTranscript -Force)
    }
    $conteudo = Hide-NomeDeUsuario (($script:Linhas -join "`r`n") + "`r`n")
    [IO.File]::WriteAllText($Transcript, $conteudo, (New-Object System.Text.UTF8Encoding $false))
    Write-Host ''
    Write-Host "Transcript gravado em: $Transcript"
}

exit $codigoDeSaida
