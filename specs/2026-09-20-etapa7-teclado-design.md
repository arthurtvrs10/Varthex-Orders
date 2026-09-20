# Etapa 7 · Fatia 2 — Navegação por teclado e foco visível (design)

Cobre RNF18 ("Operações essenciais funcionam por teclado e possuem foco visível"),
CT17 ("buscar produto, adicionar item, iniciar encerramento pelo teclado") e o
quadro "Atalhos mínimos" de `docs/docs/07-*` (Ctrl+F, `+`, `-`, F4, Esc, Enter).

## Estado atual (verificado)

- Únicos atalhos: F4 (Ver total, `AtendimentoView`) e `ConfirmacaoView` ("Não" é
  Default+Cancel). Nenhum `FocusVisualStyle`, `IsDefault`, `AccessKey`, `TabIndex`.
- Grade de comandas: não existe campo para digitar o número (o
  `AtendimentoViewModel` já tem `NovoNumero` + `AbrirCommand`, sem UI).
- Dentro da comanda: `TextoBuscaCatalogo` existe no VM, sem caixa de busca na
  tela; ao abrir, o foco vai para `PainelComanda` (só para o F4 funcionar), então
  o menu só é alcançado voltando com Shift+Tab; Voltar/Cancelar sem atalho.
- `EncerramentoView` sem `IsDefault`/`IsCancel` e sem foco inicial.
- O foco padrão do WPF é um retângulo pontilhado fino, pouco visível em botões
  laranja/verde.

## Decisões (rulings)

1. **Foco visível único:** um `FocusVisualStyle` compartilhado em `App.xaml`
   (contorno sólido azul `#1F5FBF` de 3 px com respiro de 2 px, sem pontilhado)
   aplicado por *setter* nos estilos implícitos de Button, TextBox, ComboBox,
   CheckBox e DatePicker. **Não** criar estilo para `ListViewItem`/`ListView`
   (um `BasedOn` já derrubou o app na inicialização; a seleção da lista já é
   visível). Os botões com `Style` local (slots, categorias) ganham o mesmo
   setter.
2. **Atalhos** (janela principal): `Ctrl+1..5` abrem Atendimento, Produtos,
   Histórico, Backup, Configurações (os botões de navegação também ganham tecla
   de acesso `_`? **não** — conflita com o texto; usar `ToolTip` "Ctrl+N" e
   `AutomationProperties.AccessKey`).
3. **Atendimento – grade:** `TextBox` "Nº da comanda" (ligado a `NovoNumero`),
   `Enter` executa `AbrirCommand` (abre livre ou seleciona a aberta);
   `Ctrl+N` foca esse campo; foco inicial nele quando a grade aparece.
4. **Atendimento – dentro da comanda:** caixa de busca ligada a
   `TextoBuscaCatalogo` no topo do menu; `Ctrl+F` foca; **Enter** na busca
   adiciona o primeiro produto listado (sem resultado: mensagem "Nenhum produto
   encontrado.") e mantém o foco na busca com o texto selecionado para o
   próximo item; `Esc` com texto na busca limpa; `Esc` sem texto = Voltar;
   foco inicial na busca ao abrir a comanda (F4 continua funcionando porque a
   busca está dentro do `UserControl`).
5. **Quantidade por teclado:** `↑/↓` no painel de itens seleciona a linha
   (item selecionado destacado); `+`/`-` (e `Add`/`Subtract` do teclado
   numérico) aumentam/diminuem o item selecionado; `Delete` remove. Como o
   painel é um `ItemsControl` de botões, implementar com `SelectedItemId` no VM
   (novo, testável) e `Key` bindings no `UserControl`; teclas de texto (`+`/`-`)
   **não** disparam quando o foco está numa `TextBox`.
6. **Diálogos:** `EncerramentoView`: `Esc` = "Voltar para a comanda"
   (`IsCancel`), foco inicial no CheckBox, "Confirmar e encerrar" é `IsDefault`
   (Enter) e continua desabilitado até marcar a cobrança (já é comando com
   `CanExecute`? — verificar; se não for, tornar). `ConfirmacaoView`: mantém
   Enter/Esc = Não; "Sim" ganha tecla de acesso `_Sim` (Alt+S).
7. **Formulários:** `IsDefault` no botão principal de Produtos (Salvar/Cadastrar),
   Configurações (Salvar); `Ctrl+F` foca a busca em Produtos e Histórico.
8. **Tab order:** ordem lógica esquerda→direita/cima→baixo; conteúdo decorativo
   fora da ordem (`IsTabStop=False`) apenas onde hoje atrapalha (ex.: cards do
   menu **continuam** tab stops; categorias também).

## Verificação

- Testes de ViewModel (`SelectedItemId`, busca+Enter, número+Enter).
- Roteiro de navegação **por UI Automation** com dados isolados
  (`VARTHEX_COMANDA_DADOS`), executado por mim na fatia 4 (CT17): abrir comanda
  digitando o número, buscar, Enter, `+`, F4, Esc — só teclado.
