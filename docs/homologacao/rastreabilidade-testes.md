# Rastreabilidade dos casos de teste (CT01–CT22)

> Gerado por `RastreabilidadeDosCasosTests`; não editar à mão.
> Regenerar: defina `VARTHEX_GERAR_RASTREABILIDADE` com o caminho deste arquivo e rode `dotnet test backend/tests/VarthexComanda.Desktop.Tests --filter RastreabilidadeDosCasosTests`.

Cada caso de `docs/docs/09-testes-aceitacao.md` aparece com os testes automatizados marcados com
`[Trait("Caso", "CTnn")]`. A coluna **Tipo** diz o que a cobertura automatizada prova:
`automatizado`, `estrutural` (prova por construção do código, não do comportamento em execução)
ou `manual` (sem teste automatizado). A **Nota** registra os limites; o que sobra (ensaio no app real,
no computador da loja) está descrito em `docs/homologacao/`.

| Caso | Cenário | Critério de aceitação | Tipo | Testes | Nota |
| --- | --- | --- | --- | ---: | --- |
| CT01 | Abrir comanda livre | Número fica ocupado e comanda fica `ABERTA` | automatizado | 3 | Caso de uso (repositório em memória), repositório EF sobre SQLite real e ViewModel. |
| CT02 | Impedir duplicidade | Segunda abertura do mesmo número é recusada | automatizado | 3 | A recusa está no caso de uso e no índice único parcial do SQLite; a tela, em vez de exibir erro, seleciona a comanda já aberta. |
| CT03 | Adicionar produto | Duas inclusões de R$ 18,00 resultam em quantidade 2 e subtotal R$ 36,00 | automatizado | 4 | Inclui teste com os valores literais (R$ 18,00) sobre SQLite real. |
| CT04 | Preservar preço | Alterar produto para R$ 22,00 não muda item lançado por R$ 18,00 | automatizado | 2 | Ponta a ponta com SQLite real: item, total e histórico da venda mantêm R$ 18,00. |
| CT05 | Corrigir quantidade | Reduzir 3 para 2 recalcula e persiste subtotal e total | automatizado | 3 | A persistência é conferida relendo por contexto novo. |
| CT06 | Recusar quantidade inválida | Valor negativo ou fracionário não é gravado | automatizado | 5 | Negativo/zero: caso de uso e restrição CHECK do banco. Fracionário: apenas por construção (a quantidade é `int` em toda a cadeia e a tela só tem + e −); não há campo de digitação para testar. |
| CT07 | Recusar comanda vazia | Nenhuma venda é criada | automatizado | 5 | Caso de uso, ViewModel e restrição do banco (venda de total zero é recusada). |
| CT08 | Exibir total | Itens de R$ 18,00, R$ 18,00 e R$ 6,00 mostram total R$ 42,00 | automatizado | 3 | Total calculado e persistido com os valores literais; a exibição em tela é conferida no ViewModel (sem teste visual de pixel). |
| CT09 | Falha na cobrança externa | Voltar mantém comanda aberta e não cria venda | automatizado | 5 | ViewModel de encerramento (em memória e sobre SQLite real). A maquininha de verdade é ensaio manual. |
| CT10 | Encerrar após cobrança manual | Cria venda e fecha comanda sem dados de pagamento | automatizado | 4 | "Sem dados de pagamento" é conferido no esquema real do banco (nenhuma coluna de pagamento). |
| CT11 | Rollback | Falha entre venda e fechamento desfaz tudo | automatizado | 2 | Injeção real de falha (interceptor de comandos do EF) no meio do `SaveChanges` do encerramento; nada é persistido. |
| CT12 | Histórico | Itens, total e horário correspondem ao encerramento | automatizado | 4 | Horário conferido ao tick; itens e total relidos pelo repositório do histórico. |
| CT13 | Operar offline | Cadastro, comanda, total, encerramento e histórico funcionam sem rede | estrutural | 1 | Só verificação estrutural: os quatro assemblies do produto não referenciam APIs de rede. Não substitui operar de fato sem rede (ensaio manual). |
| CT14 | Criar backup | Cópia abre, passa na integridade e possui checksum | automatizado | 3 | Serviço real de backup sobre SQLite real. |
| CT15 | Rejeitar backup corrompido | Base ativa permanece inalterada | automatizado | 7 | Backup real com bytes corrompidos: caso de uso recusa sem nenhuma escrita na base ativa (hash idêntico). |
| CT16 | Atualizar aplicação | Migrações completam e totais anteriores permanecem | automatizado | 2 | Banco criado só até a `InitialCreate`, com dados por SQL, migrado até a última com backup preventivo; totais, vendas e comanda aberta preservados. |
| CT17 | Navegar por teclado | É possível localizar, adicionar e iniciar encerramento | automatizado | 41 | Parcial: ViewModel e XAML (atalhos, foco, Enter/Esc). A prova com teclado físico real é ensaio manual. |
| CT18 | Resumo diário | Quatro vendas totalizando R$ 120,00 geram ticket médio R$ 30,00 | automatizado | 2 | ViewModel do histórico sobre SQLite real, com os valores literais. |
| CT19 | Impedir segunda instância | Com o aplicativo aberto, nova execução exibe aviso e não abre outra conexão com a base | automatizado | 1 | Parcial: teste unitário da trava (mutex). A prova com o segundo executável (aviso na tela e nenhuma segunda conexão) é ensaio. |
| CT20 | Recuperar comandas abertas | Após término forçado, a reabertura mostra os mesmos itens e totais sem criar venda | automatizado | 4 | Nível de repositório: reabre o SQLite real. O término forçado do processo do app é ensaio. |
| CT21 | Instalar no Windows | Pacote autocontido inicia em instalação limpa sem exigir SDK ou runtime separado | manual | 0 | Sem teste automatizado: exige um Windows limpo (procedimento em docs/homologacao). |
| CT22 | Controlar logs | Rotação remove arquivos além da retenção e respeita o limite de armazenamento configurado | automatizado | 2 | Rotação por tamanho e retenção de N arquivos com o configurador real de log. |

## Testes por caso

### CT01 — Abrir comanda livre (automatizado)

- `AbrirComandaTests.Executar_NumeroValido_AbreComanda` (Application.Tests)
- `AtendimentoViewModelTests.Abrir_NumeroValido_CriaComandaEMostraNaGrade` (Desktop.Tests)
- `EfComandaRepositoryTests.AbrirComanda_NumeroLivre_CriaComandaAberta` (Infrastructure.Tests)

### CT02 — Impedir duplicidade (automatizado)

- `AbrirComandaTests.Executar_NumeroJaAberto_Falha` (Application.Tests)
- `AtendimentoViewModelTests.Abrir_MesmoNumeroDuasVezes_SegundaSelecionaAComandaSemDuplicarNaGrade` (Desktop.Tests)
- `EfComandaRepositoryTests.AbrirComanda_MesmoNumeroDuasVezes_SegundaLancaExcecao` (Infrastructure.Tests)

### CT03 — Adicionar produto (automatizado)

- `AdicionarItemTests.Executar_DuasVezesMesmoProduto_IncrementaQuantidadeEmVezDeDuplicar` (Application.Tests)
- `AtendimentoViewModelTests.AdicionarProdutoDuasVezes_IncrementaQuantidadeEmVezDeDuplicar` (Desktop.Tests)
- `CriteriosDeAtendimentoTests.CT03_DuasInclusoesDeR18_ResultamEmQuantidade2ESubtotalR36` (Infrastructure.Tests)
- `EfComandaRepositoryTests.AdicionarItem_MesmoProdutoMesmoPreco_IncrementaQuantidadeEmVezDeDuplicar` (Infrastructure.Tests)

### CT04 — Preservar preço (automatizado)

- `CriteriosDeAtendimentoTests.CT04_AlterarPrecoDoProdutoParaR22_NaoMudaItemJaLancadoPorR18` (Infrastructure.Tests)
- `EfComandaRepositoryTests.AdicionarItem_MesmoProdutoPrecoDiferente_CriaLinhaNova` (Infrastructure.Tests)

### CT05 — Corrigir quantidade (automatizado)

- `AlterarQuantidadeTests.Executar_QuantidadeValida_RecalculaSubtotalETotal` (Application.Tests)
- `CriteriosDeAtendimentoTests.CT05_ReduzirDe3Para2_RecalculaEPersisteSubtotalETotal` (Infrastructure.Tests)
- `EfComandaRepositoryTests.AlterarQuantidade_ItemExistente_RecalculaSubtotalETotal` (Infrastructure.Tests)

### CT06 — Recusar quantidade inválida (automatizado)

- `AdicionarItemTests.Executar_QuantidadeZeroOuNegativa_Falha` (Application.Tests)
- `AlterarQuantidadeTests.Executar_QuantidadeZeroOuNegativa_Falha` (Application.Tests)
- `CriteriosDeAtendimentoTests.CT06_BancoRecusaQuantidadeNaoPositiva` (Infrastructure.Tests)
- `CriteriosDeAtendimentoTests.CT06_QuantidadeEInteiraEmToda_ACadeiaDeLancamento` (Infrastructure.Tests)
- `CriteriosDeAtendimentoTests.CT06_QuantidadeNegativaOuZero_NaoEGravada` (Infrastructure.Tests)

### CT07 — Recusar comanda vazia (automatizado)

- `AtendimentoViewModelTests.VerTotal_ComandaSemItens_ComandoDesabilitado` (Desktop.Tests)
- `CriteriosDeAtendimentoTests.CT07_EncerrarComandaVazia_NaoCriaVendaEComandaContinuaAberta` (Infrastructure.Tests)
- `CriteriosDeAtendimentoTests.CT07_RepositorioDireto_ComandaVazia_BancoRecusaEDesfazTudo` (Infrastructure.Tests)
- `EncerramentoViewModelTests.ConfirmarEncerrar_ComandaVazia_MostraMensagemENaoDisparaConcluido` (Desktop.Tests)
- `EncerrarComandaTests.Executar_ComandaVazia_Falha` (Application.Tests)

### CT08 — Exibir total (automatizado)

- `AtendimentoViewModelTests.AtualizarSlots_ComandaAbertaMostraTotalETempoFormatados` (Desktop.Tests)
- `CriteriosDeAtendimentoTests.CT08_ItensDe18_18E6_TotalizamR42` (Infrastructure.Tests)
- `EncerramentoViewModelTests.Carregar_ComandaComItens_PreencheNumeroTotalEItens` (Desktop.Tests)

### CT09 — Falha na cobrança externa (automatizado)

- `AtendimentoViewModelTests.VerTotal_DialogoCancela_ComandaPermaneceAberta` (Desktop.Tests)
- `CriteriosDeTelaComBancoRealTests.CT09_FalhaNaCobranca_VoltarMantemComandaAbertaENaoCriaVenda` (Desktop.Tests)
- `EncerramentoViewModelTests.ConfirmarEncerrar_ExecutadoDiretamenteSemCobrancaAprovada_NuncaCriaVenda` (Desktop.Tests)
- `EncerramentoViewModelTests.ConfirmarEncerrar_SemCobrancaAprovada_ComandoDesabilitado` (Desktop.Tests)
- `EncerramentoViewModelTests.Voltar_DisparaConcluidoFalseSemAlterarComanda` (Desktop.Tests)

### CT10 — Encerrar após cobrança manual (automatizado)

- `CriteriosDeAtendimentoTests.CT10_Encerrar_CriaVendaFechaComandaESemColunasDePagamento` (Infrastructure.Tests)
- `EfComandaRepositoryTests.EncerrarComanda_ComandaAberta_GravaVendaEFechaComanda` (Infrastructure.Tests)
- `EncerramentoViewModelTests.ConfirmarEncerrar_CobrancaAprovada_FechaComandaEDisparaConcluidoTrue` (Desktop.Tests)
- `EncerrarComandaTests.Executar_ComandaComItens_RetornaVendaEFechaComanda` (Application.Tests)

### CT11 — Rollback (automatizado)

- `FalhaNoEncerramentoTests.CT11_CasoDeUsoEncerrarComanda_FalhaDeBanco_NaoFechaComanda` (Infrastructure.Tests)
- `FalhaNoEncerramentoTests.CT11_FalhaDuranteOEncerramento_DesfazVendaEFechamento` (Infrastructure.Tests)

### CT12 — Histórico (automatizado)

- `CriteriosDeAtendimentoTests.CT12_Historico_MostraItensTotalEHorarioDoEncerramento` (Infrastructure.Tests)
- `EfVendaRepositoryTests.BuscarItensDaVenda_VendaExistente_RetornaItensDaComandaOriginal` (Infrastructure.Tests)
- `EfVendaRepositoryTests.ListarPorData_VendaDentroDoIntervalo_RetornaComNumeroDaComanda` (Infrastructure.Tests)
- `HistoricoViewModelTests.SelecionarVenda_PopulaItensDaVendaSelecionada` (Desktop.Tests)

### CT13 — Operar offline (estrutural)

- `OperacaoOfflinePorConstrucaoTests.CT13_AssembliesDoProduto_NaoReferenciamApisDeRede` (Desktop.Tests)

### CT14 — Criar backup (automatizado)

- `EfBackupServiceTests.CriarBackupGerenciado_BackupPassaNaVerificacaoDeIntegridade` (Infrastructure.Tests)
- `EfBackupServiceTests.CriarBackupGerenciado_CriaArquivoDbEChecksumECompanheiro` (Infrastructure.Tests)
- `EfBackupServiceTests.Validar_ArquivoValido_TodasAsChecagensPassam` (Infrastructure.Tests)

### CT15 — Rejeitar backup corrompido (automatizado)

- `BackupCorrompidoTests.CT15_RestaurarBackupCorrompido_PeloCasoDeUso_RecusaESemNenhumaEscritaNaBaseAtiva` (Infrastructure.Tests)
- `BackupCorrompidoTests.CT15_RestaurarParaDireto_ComBackupCorrompido_FalhaEOsDadosAtivosSeguemIntactos` (Infrastructure.Tests)
- `BackupViewModelModoRestauracaoTests.ModoRestauracao_ArquivoNaoValidado_MostraMotivoSemRestaurar` (Desktop.Tests)
- `EfBackupServiceTests.RestaurarPara_ArquivoCorrompidoComoOrigem_ContinuaFalhandoESemAlterarABaseAtiva` (Infrastructure.Tests)
- `EfBackupServiceTests.RestaurarPara_ArquivoInvalido_NaoAlteraABaseAtiva` (Infrastructure.Tests)
- `EfBackupServiceTests.Validar_ArquivoCorrompido_FalhaNaIntegridade` (Infrastructure.Tests)
- `RestaurarBackupTests.Executar_RelatorioReprovado_NaoChamaRestaurarParaERetornaFalha` (Application.Tests)

### CT16 — Atualizar aplicação (automatizado)

- `AtualizacaoDaAplicacaoTests.CT16_BancoNoEsquemaDaInitialCreate_AtualizaMantendoTotaisEVendas` (Infrastructure.Tests)
- `MigracaoDoBancoTests.BancoComAlgumasMigracoesAplicadasEOutrasPendentes_ExigeBackupPreventivo` (Infrastructure.Tests)

### CT17 — Navegar por teclado (automatizado)

- `AtendimentoTecladoXamlTests.AtendimentoView_ExpoeCampoDeNumeroEDeBusca` (Desktop.Tests)
- `AtendimentoTecladoXamlTests.AtendimentoView_ItemSelecionado_TemLinhaComFundoDestacado` (Desktop.Tests)
- `AtendimentoTecladoXamlTests.AtendimentoView_OrdemDeTab_BuscaCategoriasCardsItensVoltarCancelarFinalizar` (Desktop.Tests)
- `AtendimentoTecladoXamlTests.AtendimentoView_SemComandaSelecionada_InstanciaOsSlotsComFocoVisivel` (Desktop.Tests)
- `AtendimentoTecladoXamlTests.Enter_NaBuscaSemResultado_MostraMensagem` (Desktop.Tests)
- `AtendimentoTecladoXamlTests.Enter_NaBuscaVazia_NaoAdicionaNada` (Desktop.Tests)
- `AtendimentoTecladoXamlTests.Enter_NaBusca_AdicionaOPrimeiroEMantemOTexto` (Desktop.Tests)
- `AtendimentoTecladoXamlTests.Esc_ComTextoNaBusca_LimpaSemSair_ESemTextoVolta` (Desktop.Tests)
- `AtendimentoTecladoXamlTests.Esc_ForaDaBusca_VoltaMesmoComTextoNaBusca` (Desktop.Tests)
- `AtendimentoTecladoXamlTests.ItemSelecionadoParaCorConverter_DestacaSoALinhaSelecionada` (Desktop.Tests)
- `AtendimentoTecladoXamlTests.SomenteDigitos_AceitaApenasDigitosAscii` (Desktop.Tests)
- `AtendimentoTecladoXamlTests.Teclas_ComFocoNaBusca_NaoAgemSobreOsItens` (Desktop.Tests)
- `AtendimentoTecladoXamlTests.Teclas_ComFocoNoMenu_NaoAgemSobreOsItens` (Desktop.Tests)
- `AtendimentoTecladoXamlTests.Teclas_SetasESinais_OperamNoItemSelecionadoForaDeCampoDeTexto` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.Abrir_ComNovoNumero7_AbreComandaEZeraOCampo` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.Abrir_NumeroDeComandaJaAberta_SelecionaEmVezDeMostrarErro` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.Abrir_NumeroInvalidoOuForaDaFaixa_MantemAsMensagensAtuais` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.AcoesNoSelecionado_SemSelecao_NaoFazemNadaENaoLancam` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.AdicionarPrimeiroDaBusca_AdicionaOPrimeiroDaListaEMantemOTexto` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.AdicionarPrimeiroDaBusca_MesmoProdutoDuasVezes_IncrementaQuantidade` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.AdicionarPrimeiroDaBusca_RespeitaCategoriaEFiltro` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.AdicionarPrimeiroDaBusca_SemResultado_MostraMensagemENaoAdiciona` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.AdicionarPrimeiroDaBusca_SemTextoMasComCategoria_NaoAdicionaNada` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.AdicionarPrimeiroDaBusca_SemTexto_NaoAdicionaNada` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.AumentarSelecionado_AumentaSoOItemSelecionado` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.DiminuirSelecionado_ComQuantidadeMaiorQueUm_Diminui` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.DiminuirSelecionado_ComQuantidadeUm_ConfirmaERemove` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.FecharEdicao_LimpaSelecaoEBusca` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.ItemAdicionado_PassaAserOSelecionado` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.RemoverSelecionado_ConfirmadorRecusa_MantemItemESelecao` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.RemoverSelecionado_NoFim_SelecionaOAnterior` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.RemoverSelecionado_NoMeio_SelecionaOVizinho` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.RemoverSelecionado_UltimoItem_SelecaoFicaNula` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.SelecionarItemAnterior_SemSelecao_VaiParaOUltimo` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.SelecionarProximoEAnterior_PercorremSemPassarDosLimites` (Desktop.Tests)
- `AtendimentoViewModelTecladoTests.Selecionar_SemItens_NaoFazNadaENaoLanca` (Desktop.Tests)
- `DialogosTecladoXamlTests.ConfiguracaoView_SalvarEPadrao` (Desktop.Tests)
- `DialogosTecladoXamlTests.ConfirmacaoView_TeclasDeAcessoSimNao_NaoEPadraoEECancel` (Desktop.Tests)
- `DialogosTecladoXamlTests.EncerramentoView_EscVoltaEEnterConfirma_ConfirmarSoHabilitadoComCobrancaAprovada` (Desktop.Tests)
- `DialogosTecladoXamlTests.HistoricoView_TemCampoDeBuscaParaCtrlF` (Desktop.Tests)
- `DialogosTecladoXamlTests.ProdutosView_EnterGravaPeloBotaoVisivelECtrlFTemCampoDeBusca` (Desktop.Tests)

### CT18 — Resumo diário (automatizado)

- `CriteriosDeTelaComBancoRealTests.CT18_QuatroVendasQueSomamR120_GeramTicketMedioR30` (Desktop.Tests)
- `HistoricoViewModelTests.Resumo_DuasVendas_CalculaQuantidadeTotalETicketMedio` (Desktop.Tests)

### CT19 — Impedir segunda instância (automatizado)

- `SingleInstanceGuardTests.TryAcquire_SegundaGuardaComMesmoNome_FalhaAteAPrimeiraLiberar` (Infrastructure.Tests)

### CT20 — Recuperar comandas abertas (automatizado)

- `RecuperacaoDeAtendimentoTests.ComandaEncerrada_NaoReaparecemComoAberta` (Infrastructure.Tests)
- `RecuperacaoDeAtendimentoTests.ComandasAbertas_ReaparecemAposReinicio_ComMesmosItensETotais` (Infrastructure.Tests)
- `RecuperacaoDeAtendimentoTests.Reinicio_NaoCriaVendaNemAlteraStatus` (Infrastructure.Tests)
- `RecuperacaoDeAtendimentoTests.Reinicio_NaoDuplicaNemDescartaItemConfirmado` (Infrastructure.Tests)

### CT21 — Instalar no Windows (manual)

_Sem teste automatizado (manual)._

### CT22 — Controlar logs (automatizado)

- `LoggingConfiguratorTests.CreateLogger_RetemNoMaximoNArquivos` (Infrastructure.Tests)
- `LoggingConfiguratorTests.CreateLogger_RolaPorTamanho` (Infrastructure.Tests)
