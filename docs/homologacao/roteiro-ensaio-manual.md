# Roteiro do ensaio manual no computador da loja

Para uma pessoa executar, com o computador da loja, o que os testes automatizados e os ensaios do desenvolvimento **não conseguem provar**. Ao terminar, copie os resultados para [resultados-ct.md](resultados-ct.md) e [checklist-etapa8.md](checklist-etapa8.md).

> **Regra de ouro:** durante este roteiro o aplicativo só pode abrir com **dados descartáveis** (partes 1 a 10). Nunca aponte a massa de teste para a pasta real `%LOCALAPPDATA%\VarthexComanda`. Se a loja já usa o aplicativo com dados de verdade, faça antes um backup e **não misture**: use uma conta do Windows separada ou um computador de teste.

## Como preencher

Cada passo tem caixas para marcar e três campos:

- **Resultado obtido:** o que você viu de fato, com as palavras da tela. Se algo diferente do esperado aconteceu, escreva.
- **Situação:** marque uma, `Aprovado`, `Reprovado` ou `Bloqueado` (não deu para executar).
- **Evidência:** foto da tela, arquivo do log, anotação com data e hora. Não fotografe cartão, senha nem tela da maquininha com dados de cartão.

Dados da execução:

- Executado por: ____________________  Data: ___/___/______  Versão do aplicativo (rodapé de Configurações): ________
- Computador: preencha primeiro a [matriz de compatibilidade](matriz-compatibilidade.md).

---

## Parte 0. Antes de começar

- [ ] Ter o pacote `VarthexComanda-1.0.0-win-x64.zip` no computador (copiado por pendrive; sem internet, se possível).
- [ ] Ter a **massa mínima já gerada** (parte 1) numa pasta com **o mesmo caminho** que será usado neste computador (o registro de backups guarda caminhos absolutos; se a pasta mudar de lugar, a tela de Backup aponta para a pasta antiga).
- [ ] Ter à mão: uma maquininha real, dois pendrives, o Gerenciador de Tarefas (`Ctrl+Shift+Esc`), um teclado físico.
- [ ] Avisar quem trabalha na loja que o aplicativo estará em teste.

---

## Parte 1. Instalar e preparar dados descartáveis (massa mínima)

A massa mínima é gerada na **máquina de desenvolvimento** (precisa do SDK do .NET; **não instale o SDK no computador da loja**, para não invalidar o CT21). Ela traz 5 categorias (1 inativa), 30 produtos (1 inativo), 20 números de comanda, 5 comandas abertas de perfis diferentes (1 item, 2 itens, muitos itens, itens repetidos, vazia), comandas abandonadas, 90 dias de histórico e três arquivos em `backups\` (válido, antigo e corrompido).

**1.1 Gerar (na máquina de desenvolvimento).** Escolha a pasta final que existirá no computador da loja, por exemplo `C:\HomologacaoVarthex\dados`, e rode na raiz do repositório:

```powershell
dotnet run --project backend\tools\VarthexComanda.MassaMinima -- --saida C:\HomologacaoVarthex\dados
```

Anote as contagens que a ferramenta imprime (devem ser 5 categorias, 1 inativa; 30 produtos, 1 inativo; 20 números; 5 comandas abertas; 90 dias). Gere o mais perto possível do dia do ensaio (o histórico é relativo à data de geração). Copie a pasta inteira para o mesmo caminho no computador da loja.

- [ ] Massa gerada e copiada para o mesmo caminho no computador da loja.

**1.2 Instalar o pacote (no computador da loja).** Extraia o `.zip` **inteiro** numa pasta e dê dois cliques em `Instalar.cmd`. Ele instala em `%LOCALAPPDATA%\Programs\VarthexComanda` e cria atalhos. Para instalar sem atalhos, num Prompt de Comando dentro da pasta extraída: `.\Instalar.cmd -SemAtalhos`.

- [ ] Instalação concluída sem erro.

**1.3 Abrir o aplicativo com a massa.** Sem mexer nos dados reais, abra o aplicativo **de um Prompt de Comando ou de um arquivo `.cmd`** que defina a variável `VARTHEX_COMANDA_DADOS`. A variável vale só para essa janela; o atalho normal do Windows continua abrindo os dados reais.

No Prompt de Comando:

```bat
set VARTHEX_COMANDA_DADOS=C:\HomologacaoVarthex\dados
"%LOCALAPPDATA%\Programs\VarthexComanda\VarthexComanda.exe"
```

Para não digitar isso toda vez, crie na Área de Trabalho um arquivo de texto chamado `Varthex-TESTE.cmd` (atenção à extensão `.cmd`, não `.txt`) com estas duas linhas, e **use somente esse arquivo neste roteiro**:

```bat
set VARTHEX_COMANDA_DADOS=C:\HomologacaoVarthex\dados
start "" "%LOCALAPPDATA%\Programs\VarthexComanda\VarthexComanda.exe"
```

Conferir: a grade mostra 20 comandas, com as comandas 3, 7, 12, 15 e 18 ocupadas; o Histórico mostra vendas dos últimos 90 dias.

- [ ] Aplicativo abriu com a massa (20 números; 5 comandas ocupadas; histórico de 90 dias).

Resultado obtido: ______________________________________________
Situação: [ ] Aprovado  [ ] Reprovado  [ ] Bloqueado
Evidência: ______________________________________________

---

## Parte 2. Tempo de abertura, tempo de resposta e legibilidade (RNF03, RNF02, RNF07)

**2.1 RNF03 (abrir em até 5 segundos, média de cinco aberturas).** Feche o aplicativo. Com um cronômetro (celular), dê dois cliques no `Varthex-TESTE.cmd` e pare quando a janela principal estiver pronta. Repita cinco vezes.

| Abertura | 1 | 2 | 3 | 4 | 5 | Média |
| --- | --- | --- | --- | --- | --- | --- |
| Segundos | | | | | | |

Meta: média até 5 s. Referência (máquina de desenvolvimento, medida por ensaio): 1,7 s.

Resultado obtido: ______________________________________________
Situação: [ ] Aprovado  [ ] Reprovado  [ ] Bloqueado
Evidência: ______________________________________________

**2.2 RNF02 (ação comum em até 500 ms).** Repita 20 ações comuns (adicionar produto, abrir comanda, trocar de tela). Se alguma parecer lenta (mais de meio segundo), anote quantas de 20.

- [ ] Registrado quantas das 20 ações pareceram lentas (o critério pede 95 % dentro do limite, isto é, no máximo 1 lenta).

Resultado obtido: ______________________________________________
Situação: [ ] Aprovado  [ ] Reprovado  [ ] Bloqueado
Evidência: ______________________________________________

**2.3 RNF07 (texto e controles legíveis, contraste e tamanho).** Percorra as 5 telas na distância normal de uso no balcão.

- [ ] Textos legíveis sem esforço; botões grandes o bastante para acertar com o dedo ou o mouse.

Resultado obtido: ______________________________________________
Situação: [ ] Aprovado  [ ] Reprovado  [ ] Bloqueado
Evidência: ______________________________________________

---

## Parte 3. Operar sem rede (CT13)

Critério: cadastro, comanda, total, encerramento e histórico funcionam sem rede. **Este é o teste que faltava**; o teste estrutural do código e o ensaio E6 são só apoio.

**3.1 Desligar a rede.**

- [ ] Desligar o Wi-Fi e/ou desconectar o cabo de rede do computador (não basta fechar o navegador).
- [ ] Confirmar que **não há internet**: abrir o navegador e ver que uma página não carrega; na barra do Windows o ícone de rede mostra "sem conexão".

**3.2 Operar com o aplicativo (`Varthex-TESTE.cmd`), sem rede:**

- [ ] Cadastrar uma categoria nova e um produto novo (Produtos).
- [ ] Abrir uma comanda livre e adicionar o produto novo e mais dois produtos.
- [ ] Ver o total e conferir a soma com a calculadora.
- [ ] Encerrar (na tela de Encerramento, marcar "A cobrança foi aprovada fora do sistema?" e "Confirmar e encerrar").
- [ ] Encontrar a venda no Histórico e conferir itens, total e horário.
- [ ] Criar um backup (Backup > "Criar backup agora").
- [ ] Nenhuma mensagem de erro de rede apareceu em nenhum momento.

**3.3 Religar a rede** e anotar.

- [ ] Rede religada.

Resultado obtido: ______________________________________________
Situação: [ ] Aprovado  [ ] Reprovado  [ ] Bloqueado
Evidência: ______________________________________________

---

## Parte 4. Navegar só com o teclado (CT17)

Critério: é possível localizar, adicionar e iniciar o encerramento. Use **teclado físico**, sem tocar o mouse. Este passo cobre o que os testes automatizados não provam (teclas reais no aplicativo real), incluindo os últimos ajustes de foco, que só tiveram verificação ao vivo parcial.

| # | Ação | Esperado | OK |
| --- | --- | --- | --- |
| 1 | `Ctrl+1`, `Ctrl+2`, `Ctrl+3`, `Ctrl+4`, `Ctrl+5` | Troca para Atendimento, Produtos, Histórico, Backup e Configurações | [ ] |
| 2 | Em Atendimento, `Ctrl+N` | Foco no campo "Nº da comanda"; contorno azul visível | [ ] |
| 3 | Digitar um número livre (ex.: 4) e `Enter` | A comanda 4 abre e o campo é limpo | [ ] |
| 4 | Digitar de novo o número de uma comanda **já aberta** (ex.: 7) e `Enter` | A comanda 7 é **selecionada** (não aparece erro) | [ ] |
| 5 | `Ctrl+F` | Foco em "Buscar produto"; contorno azul visível | [ ] |
| 6 | Digitar parte do nome de um produto e `Enter` | O primeiro resultado é adicionado; o texto da busca permanece | [ ] |
| 7 | Busca vazia e `Enter` | Nada é adicionado | [ ] |
| 8 | Busca sem resultado e `Enter` | Aparece mensagem; nada é adicionado | [ ] |
| 9 | `Esc` com texto na busca | Limpa o texto sem sair; um novo `Esc` volta | [ ] |
| 10 | Com foco fora dos campos de texto, `↑` e `↓` | Selecionam o item anterior/próximo da comanda | [ ] |
| 11 | `+` e `-` | Aumenta/diminui a quantidade do item selecionado | [ ] |
| 12 | `-` num item com quantidade 1 | Pede confirmação antes de remover | [ ] |
| 13 | `Delete` no item selecionado | Remove o item (com confirmação) | [ ] |
| 14 | `F4` com itens na comanda | Abre "Encerrar comanda" | [ ] |
| 15 | Em Encerramento, `Enter` **sem** marcar "A cobrança foi aprovada fora do sistema?" | Não encerra | [ ] |
| 16 | Marcar a caixa com a barra de espaço e `Enter` | Encerra e volta ao Atendimento (use uma comanda de teste) | [ ] |
| 17 | Em outra comanda, `F4` e depois `Esc` | Volta para a comanda sem encerrar | [ ] |
| 18 | `Tab` / `Shift+Tab` em cada tela | O foco passa por todos os controles numa ordem lógica e é sempre visível | [ ] |
| 19 | Numa confirmação (por exemplo, ao remover), pressionar `s` e `n` | **Comportamento conhecido:** a tecla `s`, sem `Alt`, aciona "Sim" na confirmação. Registre se isso é aceitável para a loja | [ ] |

Resultado obtido: ______________________________________________
Situação: [ ] Aprovado  [ ] Reprovado  [ ] Bloqueado
Evidência: ______________________________________________

---

## Parte 5. Segundo executável (CT19)

Critério: com o aplicativo aberto, uma nova execução mostra aviso e não abre outra janela nem outra conexão com a base.

- [ ] Com o aplicativo aberto (`Varthex-TESTE.cmd`), abrir **o mesmo programa** outra vez (dois cliques no `Varthex-TESTE.cmd`). **Não use o atalho do Menu Iniciar**: ele abre sem `VARTHEX_COMANDA_DADOS` e apontaria para os dados reais da loja. Se quiser conferir que o atalho também mostra o aviso, faça isso só num computador de teste, com a primeira instância comprovadamente aberta.
- [ ] Aparece o aviso: **"O Varthex Comanda já está aberto neste computador."**
- [ ] Depois de clicar em OK, continua existindo **uma única janela** do Varthex Comanda (barra de tarefas) e o Gerenciador de Tarefas (aba Detalhes) mostra um único `VarthexComanda.exe`.
- [ ] O aplicativo original continua funcionando normalmente.

Resultado obtido: ______________________________________________
Situação: [ ] Aprovado  [ ] Reprovado  [ ] Bloqueado
Evidência: ______________________________________________

---

## Parte 6. Término forçado pelo Gerenciador de Tarefas (CT20)

Critério: após término forçado, a reabertura mostra os mesmos itens e totais, sem criar venda.

- [ ] Abrir a comanda 1 e lançar 2 ou 3 itens; anotar o total mostrado: R$ __________.
- [ ] Anotar quantas vendas o Histórico mostra **para hoje**: __________ (com a massa mínima é 0).
- [ ] `Ctrl+Shift+Esc` > aba **Detalhes** > `VarthexComanda.exe` > **Finalizar tarefa** (término forçado, sem fechar o aplicativo normalmente).
- [ ] Reabrir pelo `Varthex-TESTE.cmd`.
- [ ] A comanda 1 continua aberta, com **os mesmos itens e o mesmo total**; nenhum item duplicado ou perdido.
- [ ] O Histórico continua mostrando o mesmo número de vendas de hoje (nenhuma venda foi criada).
- [ ] (Opcional) No arquivo de log do dia, em `C:\HomologacaoVarthex\dados\logs`, a linha "Comandas abertas recuperadas: N".

Resultado obtido: ______________________________________________
Situação: [ ] Aprovado  [ ] Reprovado  [ ] Bloqueado
Evidência: ______________________________________________

---

## Parte 7. Reiniciar o computador com uma comanda aberta

Complementa o CT20 e o RNF05: o que a loja realmente vai enfrentar numa queda de energia ou reinício.

- [ ] Com o `Varthex-TESTE.cmd`, abrir uma comanda com 2 ou 3 itens; anotar número e total: comanda ______, R$ __________.
- [ ] **Sem fechar o aplicativo**, reiniciar o Windows (Iniciar > Reiniciar; se aparecer aviso de aplicativo aberto, escolha reiniciar mesmo assim).
- [ ] Depois de reiniciar, abrir o `Varthex-TESTE.cmd`.
- [ ] A comanda continua aberta com os mesmos itens e o mesmo total; nenhuma venda foi criada.
- [ ] (Opcional, **só numa máquina virtual ou computador de teste** — nunca no computador de produção da loja) Repetir desligando pelo botão de energia, para simular queda de energia.

Resultado obtido: ______________________________________________
Situação: [ ] Aprovado  [ ] Reprovado  [ ] Bloqueado
Evidência: ______________________________________________

---

## Parte 8. Escalas de exibição 100 %, 150 % e 200 %

Para cada escala: `Configurações do Windows > Sistema > Tela > Escala`. Se o Windows pedir para sair e entrar novamente, faça isso e reabra o `Varthex-TESTE.cmd`. Anote a resolução efetiva. Em cada escala, percorra as 5 telas e abra "Encerrar comanda".

| Escala | Nada cortado ou sobreposto? | Textos legíveis? | Botões alcançáveis? | Situação |
| --- | --- | --- | --- | --- |
| 100 % | [ ] | [ ] | [ ] | [ ] Aprovado  [ ] Reprovado  [ ] Bloqueado |
| 150 % | [ ] | [ ] | [ ] | [ ] Aprovado  [ ] Reprovado  [ ] Bloqueado |
| 200 % | [ ] | [ ] | [ ] | [ ] Aprovado  [ ] Reprovado  [ ] Bloqueado |

- [ ] Escala original do computador restaurada ao final.

Resultado obtido: ______________________________________________
Evidência (fotos de cada escala): ______________________________________________

---

## Parte 9. Erro da maquininha (CT09 com a maquininha real)

Critério: se a cobrança externa não for concluída, voltar mantém a comanda aberta e não cria venda. O ensaio automatizado E4 já provou a parte do aplicativo; falta o gesto real com a máquina.

- [ ] Abrir uma comanda com itens e chegar a "Encerrar comanda". Conferir "Total a pagar" e a frase "Digite este valor na maquininha."
- [ ] Digitar o valor na **maquininha real** e **provocar a falha** (recusa, cancelamento, cartão negado ou desistência do cliente). **Não marque** "A cobrança foi aprovada fora do sistema?".
- [ ] Clicar em **"Voltar para a comanda"** (ou `Esc`). A comanda continua **aberta** com os mesmos itens e o mesmo total; o Histórico **não** mostra venda nova.
- [ ] Repetir o pagamento, agora **aprovado** na maquininha; marcar a caixa e clicar em **"Confirmar e encerrar"**. A venda aparece no Histórico com os itens, o total e o horário certos; a comanda fica livre.
- [ ] O aplicativo não pediu nem mostrou dado de cartão, forma de pagamento ou troco em nenhum momento.
- [ ] O texto "A cobrança foi aprovada fora do sistema?" foi compreendido pela pessoa do caixa (isto ajuda a responder a QV04).

Resultado obtido: ______________________________________________
Situação: [ ] Aprovado  [ ] Reprovado  [ ] Bloqueado
Evidência: ______________________________________________

---

## Parte 10. Backup, restauração e troca de pendrive

> A tela de **Backup lista apenas os backups registrados** (os que o próprio aplicativo criou neste banco). O backup **antigo** e o **corrompido** da massa mínima existem como arquivos em `backups\` mas **não aparecem na lista**; para tentar restaurá-los use **"Selecionar arquivo..."** e escolha o arquivo na pasta `C:\HomologacaoVarthex\dados\backups`. Essa via (o diálogo de arquivos do Windows) **não foi automatizada** e é justamente o que esta parte confere.

**10.1 Backup e restauração pela lista**

- [ ] Backup > "Criar backup agora". O novo backup aparece na lista com status válido.
- [ ] Mudar o estado: abrir uma comanda livre e lançar itens (anote: comanda ____, R$ ______).
- [ ] Selecionar o backup **válido** da massa (ou o recém-criado, anterior à mudança) e clicar em "Restaurar backup selecionado". A confirmação diz que os dados atuais serão substituídos e que uma cópia de segurança será criada.
- [ ] Confirmar. Aparece "Backup restaurado com sucesso. O Varthex Comanda vai reiniciar agora." e o aplicativo reinicia.
- [ ] Depois de reiniciar, o estado é o do backup (a comanda aberta depois do backup voltou a livre).
- [ ] Existe uma cópia de segurança da base anterior (pasta de dados, `backups\`).

**10.2 Restauração pela caixa "Selecionar arquivo..."**

- [ ] "Selecionar arquivo..." e escolher o backup **válido**: restaura e reinicia, como acima.
- [ ] "Selecionar arquivo..." e escolher o backup **corrompido** (`varthex-comanda-…-…db` de 8 KB; o arquivo `.sha256` dele não confere): o aplicativo **recusa** com mensagem compreensível, e a base atual **não** é alterada (confira que as comandas e o histórico seguem iguais).
- [ ] "Selecionar arquivo..." e escolher o backup **antigo** (esquema da primeira migração): anote o que acontece. Esperado: o aplicativo restaura (ou recusa) com mensagem clara, e ao abrir aplica as migrações mantendo os totais. **Registre exatamente o que viu**; este comportamento não foi ensaiado com o aplicativo real.

**10.3 Troca de pendrive (pasta externa)**

- [ ] Ligar o pendrive A. Backup > "Escolher pasta externa e criar backup..." e escolher uma pasta no pendrive A. O backup é criado e validado.
- [ ] Remover o pendrive A e tentar criar outro backup na mesma pasta externa. Aparece mensagem compreensível (por exemplo, "Não foi possível acessar a pasta de backup"), sem travar o aplicativo.
- [ ] Ligar o pendrive B (letra de unidade possivelmente diferente). Escolher uma pasta nele e criar o backup. Funciona.
- [ ] Em Configurações, confirmar qual é a "pasta de backup externa" padrão e que o backup automático do fim do dia foi para o pendrive certo (anote o que o aplicativo faz quando o pendrive não está conectado na hora do backup automático).
- [ ] Restaurar a partir de um backup do pendrive B com "Selecionar arquivo...".

Resultado obtido: ______________________________________________
Situação: [ ] Aprovado  [ ] Reprovado  [ ] Bloqueado
Evidência: ______________________________________________

---

## Parte 11. Instalação limpa, sem .NET (CT21)

Critério: o pacote autocontido inicia em instalação limpa, sem exigir SDK ou runtime separado. Precisa de **um Windows onde nenhum .NET esteja instalado**: uma máquina virtual nova, ou um computador de teste. **Não use** a máquina de desenvolvimento (tem o SDK) nem um computador onde já rodou algo em .NET.

- [ ] Confirmar que não há .NET: num Prompt de Comando, `dotnet --list-runtimes` deve dizer que o comando não é reconhecido (ou listar nada). Em `Configurações > Aplicativos`, procurar "Microsoft .NET" e não encontrar.
- [ ] Copiar `VarthexComanda-1.0.0-win-x64.zip` para a máquina (pendrive), **extrair inteiro** numa pasta e dar dois cliques em `Instalar.cmd`.
- [ ] Anotar qualquer aviso do Windows (SmartScreen: "O Windows protegeu o computador" por o executável **não ter assinatura de código**) e do antivírus. Se o antivírus bloquear ou colocar arquivos em quarentena, registre qual.
- [ ] Abrir pelo atalho `Varthex Comanda`. A janela aparece; a versão no rodapé de Configurações é 1.0.0.
- [ ] No log (`%LOCALAPPDATA%\VarthexComanda\logs`), a linha "Iniciando Varthex Comanda 1.0.0".
- [ ] Executar o fluxo mínimo: cadastrar categoria e produto, abrir comanda, adicionar, encerrar, ver no Histórico, criar backup.
- [ ] (Recomendado) Desinstalar com `Desinstalar.cmd` e conferir que a pasta de dados `%LOCALAPPDATA%\VarthexComanda` **permaneceu**.

Resultado obtido: ______________________________________________
Situação: [ ] Aprovado  [ ] Reprovado  [ ] Bloqueado
Evidência: ______________________________________________

---

## Parte 12. Fechamento

- [ ] Registrar o resultado de cada parte em [resultados-ct.md](resultados-ct.md) (CT09, CT13, CT17, CT19, CT20, CT21, RNF02, RNF03, RNF07) e no [checklist](checklist-etapa8.md).
- [ ] Preencher a linha "Computador da loja" e as escalas em [matriz-compatibilidade.md](matriz-compatibilidade.md).
- [ ] Apagar a pasta de dados descartáveis (`C:\HomologacaoVarthex`) e o `Varthex-TESTE.cmd`.
- [ ] Conferir que os dados reais da loja (`%LOCALAPPDATA%\VarthexComanda`) **não foram tocados** durante o roteiro.
- [ ] Anotar problemas encontrados em [pendencias.md](pendencias.md).
- [ ] Treinar os operadores com o [guia do operador](guia-do-operador.md) e registrar quem foi treinado e a data.
