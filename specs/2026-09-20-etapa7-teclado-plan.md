# Etapa 7 · Teclado e foco visível — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Operar o fluxo essencial (abrir comanda, buscar produto, adicionar,
ajustar quantidade, iniciar encerramento) só pelo teclado, com foco sempre
visível (RNF18 / CT17).

**Architecture:** estilo de foco compartilhado em `App.xaml`; `InputBindings`
na `MainWindow` e nas views; novos membros testáveis nos ViewModels
(`SelectedItemId`, busca+Enter); `IsDefault`/`IsCancel` nos diálogos.

**Tech Stack:** C#/.NET 10, WPF (XAML), CommunityToolkit.Mvvm, xUnit.

**Spec:** [specs/2026-09-20-etapa7-teclado-design.md](2026-09-20-etapa7-teclado-design.md)

## Global Constraints

- Prefixar todo `dotnet` com `export PATH="$PATH:/c/Program Files/dotnet" &&`.
  Solução: `backend/VarthexComanda.slnx`.
- **PROIBIDO abrir/rodar o app** e mexer em `%LOCALAPPDATA%\VarthexComanda`.
  Nenhum `dotnet run`. A verificação visual/UIA é feita depois, por mim, com
  dados isolados.
- Commits em português, **sem** `Co-Authored-By`.
- Cor do foco: `#1F5FBF`, espessura 3, sem `StrokeDashArray`.
- **Não** criar estilo para `ListViewItem`/`ListView` (um `BasedOn` derrubou o
  app na inicialização antes) e não usar `BasedOn` em estilos implícitos novos
  sem verificar que o app ainda compila e os estilos existentes de `App.xaml`
  (touch: `MinHeight 46` etc.) continuam valendo.
- ViewModels sem `System.Windows`/`Microsoft.Win32`.
- Mensagens exatas: `"Nenhum produto encontrado."`.
- Como não se pode rodar a UI, toda tarefa de XAML precisa **compilar** e, se
  possível, ter um teste que **carrega o XAML** (ver Task 5) — um erro de XAML só
  aparece em runtime.

---

## Task 1: Foco visível compartilhado

**Files:**
- Modify: `backend/src/VarthexComanda.Desktop/App.xaml`
- Modify: `backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoView.xaml` (estilos locais de slot e categoria)

- [ ] Em `App.xaml` adicionar `<Style x:Key="FocoVisivel">` (TargetType `Control`) contendo `Rectangle` de contorno `#1F5FBF`, `StrokeThickness="3"`, `Margin="-3"`, `SnapsToDevicePixels`, `IsHitTestVisible=False`; e `FocusVisualStyle="{StaticResource FocoVisivel}"` como *setter* nos estilos implícitos **existentes** de Button, TextBox, ComboBox, CheckBox e DatePicker.
- [ ] Os `Style` locais sem `BasedOn` dos botões de slot e de categoria (`AtendimentoView.xaml`) — que **substituem** o implícito — ganham o mesmo setter `FocusVisualStyle`.
- [ ] Compilar a solução (`dotnet build backend/VarthexComanda.slnx`) sem avisos novos e rodar `dotnet test` (nada deve mudar).
- [ ] Commit `feat: foco visivel de alto contraste em botoes e campos (RNF18)`.

---

## Task 2: Atalhos globais e foco inicial

**Files:**
- Modify: `backend/src/VarthexComanda.Desktop/MainWindow.xaml`, `MainWindow.xaml.cs`

- [ ] `Window.InputBindings`: `Ctrl+D1..D5` (e `Ctrl+NumPad1..5`) chamam os mesmos manipuladores dos botões (extrair métodos `IrParaAtendimento()`, etc., usados por `Click` e pelos atalhos; comandos simples, sem MVVM novo). Botões ganham `ToolTip="Ctrl+1"`…`"Ctrl+5"`.
- [ ] Ao trocar de aba, mover o foco para o primeiro controle útil da tela (via `Dispatcher.BeginInvoke(..., DispatcherPriority.Input)` + `MoveFocus`/`Focus()` num elemento com `x:Name`), sem lançar se o elemento não existir. No modo de restauração (fatia anterior, se já mesclada) os atalhos das abas desabilitadas **não** funcionam.
- [ ] Compilar; commit `feat: atalhos Ctrl+1..5 entre as telas (RNF18)`.

---

## Task 3: Atendimento — número da comanda, busca e quantidade por teclado

**Files:**
- Modify: `backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoViewModel.cs`, `AtendimentoView.xaml`, `AtendimentoView.xaml.cs`
- Test: `backend/tests/VarthexComanda.Desktop.Tests/Atendimento/AtendimentoViewModelTecladoTests.cs` (arquivo novo)

**Interfaces:**
- Produces (VM): `[ObservableProperty] int? ItemSelecionadoId`; comandos `SelecionarProximoItemCommand`, `SelecionarItemAnteriorCommand`, `AumentarSelecionadoCommand`, `DiminuirSelecionadoCommand`, `RemoverSelecionadoCommand` (reusam `AumentarQuantidade`/`Diminuir`/`Remover` existentes); `AdicionarPrimeiroDaBuscaCommand`.

- [ ] **Testes que falham** (VM, com os dobles já usados nos testes de `AtendimentoViewModel`): (a) `AdicionarPrimeiroDaBusca` adiciona o 1º de `ProdutosCatalogo` (respeita a ordem/filtro), limpa/mantém a busca conforme decisão do design (mantém o texto; a view seleciona), e com lista vazia define `Mensagem = "Nenhum produto encontrado."` sem adicionar; (b) `SelecionarProximoItem`/`Anterior` percorrem `Itens` sem passar dos limites e ajustam `ItemSelecionadoId`; ao adicionar um item novo ele passa a ser o selecionado; (c) `AumentarSelecionado`/`DiminuirSelecionado`/`RemoverSelecionado` operam no item selecionado e, sem seleção, não fazem nada e não lançam; após remover, a seleção vai para o vizinho ou `null`; (d) `Abrir` com `NovoNumero = "7"` funciona como hoje (teste de regressão do fluxo por número).
- [ ] **XAML/code-behind**: (1) grade: `TextBox` "Nº da comanda" (`Text="{Binding NovoNumero, UpdateSourceTrigger=PropertyChanged}"`, `InputBindings`: `Enter` → `AbrirCommand`), `Ctrl+N` (KeyBinding no `UserControl`, tratado no code-behind chamando `Focus()`) e foco inicial nele sempre que a grade fica visível; filtrar entrada não numérica no `PreviewTextInput`. (2) comanda: `TextBox` de busca no topo do menu (`Text="{Binding TextoBuscaCatalogo, UpdateSourceTrigger=PropertyChanged}"`, placeholder por `Label`/`TextBlock` sobreposto "Buscar produto (Ctrl+F)"), `Enter` → `AdicionarPrimeiroDaBuscaCommand` e depois `SelectAll()`; `Ctrl+F` foca; `Esc`: com texto limpa a busca, sem texto executa o comando **Voltar** existente; foco inicial na busca quando a comanda abre (trocar o foco atual para `PainelComanda`: o F4 continua a funcionar por estar dentro do `UserControl`). (3) itens: `Up`/`Down`/`+`/`-`/`Add`/`Subtract`/`Delete` como `KeyBinding` no `UserControl` **desativados quando o foco está numa TextBox** (checar `Keyboard.FocusedElement is TextBox` no code-behind e tratar o evento lá em vez de usar `KeyBinding` puro); linha selecionada com fundo destacado (`ItemSelecionadoId` == id, via trigger/`MultiBinding` ou `ComandaSlot`-like converter existente); `Voltar`/`Cancelar`/`Finalizar` recebem `ToolTip` com a tecla.
- [ ] Rodar a solução inteira — verde; compilar sem avisos novos.
- [ ] Commit `feat: atendimento operavel por teclado (numero, busca, quantidade) (RNF18/CT17)`.

---

## Task 4: Diálogos e formulários

**Files:**
- Modify: `Atendimento/EncerramentoView.xaml(.cs)`, `Atendimento/EncerramentoViewModel.cs` (só se `CanExecute` faltar), `ConfirmacaoView.xaml`, `Catalogo/ProdutosView.xaml(.cs)`, `Configuracao/ConfiguracaoView.xaml`, `Atendimento/HistoricoView.xaml(.cs)`, `Backup/BackupView.xaml`
- Test: `backend/tests/VarthexComanda.Desktop.Tests/Atendimento/EncerramentoViewModelTests.cs` (se `CanExecute` for adicionado)

- [ ] `EncerramentoView`: `IsCancel="True"` em "Voltar para a comanda"; `IsDefault="True"` em "Confirmar e encerrar", **que precisa estar desabilitado enquanto `CobrancaAprovada` for falso** (se hoje só mostra mensagem de erro ao clicar, mudar para `CanExecute` com `[NotifyCanExecuteChangedFor]` e teste; a mensagem existente para o caso falso continua para uso via comando direto); `FocusManager.FocusedElement` no CheckBox.
- [ ] `ConfirmacaoView`: `Content="_Sim"`/`"_Não"` mantendo `IsDefault`+`IsCancel` do "Não".
- [ ] Produtos: `IsDefault` no botão que grava (Cadastrar/Salvar — o que estiver visível), `Ctrl+F` foca a busca; Histórico: `Ctrl+F` foca a busca; Configurações: `IsDefault` em "Salvar"; Backup: nada obrigatório.
- [ ] Rodar a solução inteira; commit `feat: Enter/Esc/atalhos nos dialogos e formularios (RNF18)`.

---

## Task 5: Teste de carga dos XAML e ordem de tabulação

**Files:**
- Test: `backend/tests/VarthexComanda.Desktop.Tests/Xaml/CarregamentoDeXamlTests.cs` (novo) — só se o projeto de testes já roda em STA/aceita WPF; senão, reportar e usar apenas verificação por compilação.

- [ ] Um teste STA (`[StaFact]` se existir xunit.stafact; senão `Thread` STA manual) que instancia cada `UserControl`/`Window` de XAML **com ViewModel dublado** e chama `Measure/Arrange` sem exceção: Atendimento, Produtos, Historico, Backup, Configuracao, Encerramento (sem `ShowDialog`), Confirmacao. Serve como rede contra XAML inválido (estilo/recurso não encontrado). Se algo falhar por dependência de DI, use os construtores reais com dobles do projeto de testes.
- [ ] Listar no relatório a ordem de tabulação de Atendimento (grade e comanda) como está e qualquer `TabIndex` adicionado.
- [ ] Rodar a solução; commit `test: carrega todos os XAML em STA para pegar erro de recurso em tempo de teste`.
