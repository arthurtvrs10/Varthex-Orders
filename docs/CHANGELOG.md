# Histórico de alterações

## Android 1.0.1 - 2026-10-01 - Colar chave privada

- Permite colar o PEM completo em campo protegido, mantendo a importação por arquivo.
- Confere formato e correspondência com a chave pública antes de permitir emissão; a chave permanece apenas na memória da sessão.
- Atualiza os links de download do APK e a orientação de uso.
- Testes com chaves sintéticas cobrem PEM com CRLF, conteúdo incompleto e incompatível e licenças dos quatro planos.
- Validação visual em aparelho Android permanece pendente.

## Demonstração 1.2.3 - 2026-10-01 - Catálogo com imagens

- Edição separada com 12 produtos fictícios: pastéis, salgados, porção e bebidas.
- Imagens realistas geradas por IA sobre fundo branco, incluídas para uso offline.
- Carga inicial transacional e única; base, instalação e atalhos separados da edição normal.
- Mantém licença offline e preserva alterações e dados entre execuções e reinstalações.
- Validação: 593 testes aprovados, incluindo sete cenários novos da demonstração e leitura das 12 imagens.
- Rastreabilidade: RF01/RF03, RN04/RN05, RNF01/RNF09/RNF14, CT21; Windows limpo permanece pendente.

## Aplicativo 1.2.2 - 2026-10-01 - Instalador atualizado

- Instalador Windows recompilado com controles quadrados, cores originais e logo e crédito somente no rodapé.
- Download do Windows aponta para o arquivo versionado da release v1.2.2; links do Android usam endereço direto.
- Validação: 586 testes Release aprovados, validação documental e SHA-256 do instalador conferidos.
- Sem mudança de RF/RN de negócio. Rastreabilidade: RNF09, RNF14, RNF17 e CT21; homologação em Windows limpo continua pendente.


## 1.20 - 2026-09-29 - Nome na comanda (aplicativo 1.2.0)

- Campo opcional Nome do cliente e botão Salvar nome na comanda aberta.
- Nome na grade, no encerramento e no histórico; até 80 caracteres, editável enquanto aberta.
- Migração aditiva `AddNomeClienteComanda`; sessões antigas permanecem sem nome.
- RF29, RN28, CT24; RNF13 ajustado conforme solicitação explícita do responsável.
- Validação: 586 testes aprovados (4 Domain, 105 Application, 255 Infrastructure, 222 Desktop), incluindo migrações existentes e persistência do nome; instalador 1.2.0 gerado.


## 1.19 - 2026-09-29 - Licenciamento offline (aplicativo 1.1.0)

- Ativação por assinatura RSA, código de computador e prazo fixo: vitalícia ou 1–3 meses.
- Tela de ativação e renovação, verificação periódica e registro de último uso protegido por DPAPI.
- Emissor separado, chave privada fora do Git e dos pacotes de distribuição.
- RF28, RN24–RN27 e CT23 descritos em [Licenciamento offline](docs/20-licenciamento-offline.md).
- Validação: 578 testes aprovados (4 Domain, 99 Application, 254 Infrastructure, 221 Desktop), incluindo diálogo de ativação; pacote conferido sem emissor ou chave privada. Homologação em Windows limpo permanece pendente.


## Instalador EXE (2026-09-29)

A distribuição principal agora usa `artifacts\VarthexComanda-1.0.0-Setup-win-x64.exe`.
Dê dois cliques e siga o assistente em português. Inclui o .NET, cria atalhos e
permite desinstalar por Aplicativos Instalados do Windows, sem administrador.
Banco, fotos e backups ficam separados e são preservados.

Para gerar: `.\scripts\gerar-instalador.ps1`. Requer Inno Setup 6 instalado,
ou `-Compilador <caminho de ISCC.exe>`. Saída em `artifacts`, com SHA-256.
Atualizações usam o mesmo instalador; não criam backup dos binários anteriores.
O ZIP e os procedimentos com scripts abaixo continuam como alternativa.
Esta decisão substitui referências anteriores à ausência de instalador EXE.
Sem alteração de RF/RN de negócio; rastreabilidade: RNF09, RNF14, RNF17 e CT21.
559 testes Release passaram; CT21 em Windows limpo sem .NET permanece pendente.


## 1.17 - 2026-09-21 - Homologação (Etapa 8)

Entregue (tudo o que uma máquina de desenvolvimento prova sem terceiros; nenhum código de produto foi alterado):

- Rastreabilidade executável: `[Trait("Caso", "CTnn")]` nos testes e o teste `RastreabilidadeDosCasosTests`, que falha se um caso automatizável perder a cobertura e gera `docs/homologacao/rastreabilidade-testes.md` (`VARTHEX_GERAR_RASTREABILIDADE`). Lacunas cobertas: CT11 (falha injetada no encerramento), CT15 (backup corrompido), CT16 (atualização a partir da `InitialCreate`), CT03 a CT08, CT10, CT12 e CT18 com os valores literais do documento 09, CT09 com ViewModel e SQLite reais. CT13 tem prova só estrutural (os assemblies do produto não referenciam APIs de rede).
- Massa mínima: ferramenta `backend/tools/VarthexComanda.MassaMinima` (`--saida <pasta>`), reproduzível, que recusa a pasta real de dados; teste confere as contagens do documento 09.
- Ensaios com o aplicativo real (`scripts/homologacao/Executar-Ensaio.ps1`, UI Automation, dados descartáveis): E1 RNF03 (média de 1736 ms em 5 aberturas), E2 CT19, E3 CT20, E4 CT09, E5 restauração de backup, E6 sem conexões de rede (apoio). Todos aprovados; transcrição em `docs/homologacao/evidencias/ensaio-2026-09-21.txt`.
- Documentos em `docs/homologacao/`: README, checklist da Etapa 8, matriz de compatibilidade (RNF17), resultados por CT, roteiro do ensaio manual, guia do operador e pendências (QV01 a QV14 com a situação real e as pendências técnicas). Documentos 10 e 13 ganharam ponteiros; o CT17 entrou na linha do OBJ01 do documento 10.
- Suíte: 559 testes verdes (Domain 4, Application 85, Infrastructure 254, Desktop 216).

Pendente (a homologação humana **ainda não aconteceu**):

- CT13 com a rede física desligada e CT21 em Windows limpo, sem .NET (`Pendente – exige pessoa`); CT17 parcial (falta o teclado físico no aplicativo real).
- Computador da loja (QV07): matriz de compatibilidade, RNF02, RNF03 (o E1 mediu só a máquina de desenvolvimento), RNF07, escalas 100/150/200 %, reinício do PC com comanda aberta, maquininha real e troca de pendrive.
- A restauração pela caixa "Selecionar arquivo..." **não foi automatizada** (o diálogo de arquivos do Windows não expõe UI Automation); o backup corrompido foi recusado pelo modo de restauração (E5).
- Treinamento dos operadores (o guia existe) e as respostas do responsável a QV01 a QV14 (só a QV02 tem decisão em código).
- Limitações conhecidas: sem alerta de pouco espaço em disco (R15); `corrompido-*.db.bak` sem poda; reinício após restaurar consome um arquivo de log; `.fotos.zip` gerado no thread da interface; sem assinatura de código; sem instalador MSI; instalação e dados por conta do Windows (QV12); Enter no número seleciona a comanda já aberta; a tecla `s` responde "Sim" nas confirmações; a última rodada de ajustes de foco do teclado teve só verificação ao vivo parcial; CT06 fracionário coberto só por construção; CT19 com teste unitário só da trava (o comportamento completo é o E2).

## 1.16 - 2026-09-21 - Publicação autocontida (Etapa 7, fatia 4)

- O executável passou a se chamar `VarthexComanda.exe` (`AssemblyName = VarthexComanda`; namespaces inalterados).
- Versão `1.0.0` definida no `<Version>` do projeto Desktop; aparece no rodapé da tela Configurações e na linha "Iniciando Varthex Comanda 1.0.0" do log. Ícone do aplicativo (`.ico` multitamanho) na janela, no executável e nos atalhos.
- `scripts\publicar.ps1` publica em `win-x64`, autocontido e multiarquivo, e gera `artifacts\VarthexComanda-1.0.0-win-x64.zip` (cerca de 64 MB, cerca de 430 arquivos, runtime do .NET incluso).
- O pacote traz `Instalar.cmd`/`Instalar.ps1` (instalação por usuário em `%LOCALAPPDATA%\Programs\VarthexComanda`, sem administrador, com atalhos na Área de Trabalho e no Menu Iniciar, `-Destino` e `-SemAtalhos`), `Desinstalar.cmd`/`Desinstalar.ps1` e `LEIAME.txt`. Os scripts recusam destinos perigosos e recusam continuar com o aplicativo aberto; o instalador grava `versao-instalada.txt`.
- Os dados (`%LOCALAPPDATA%\VarthexComanda`) ficam separados e são preservados na atualização e na desinstalação. A atualização guarda a versão anterior em `VarthexComanda.anterior`.
- Documentação: README e documentos 12 e 19 passam a descrever a instalação pelo pacote; instalador de terceiros (MSI/Inno/WiX/MSIX) fica como melhoria futura.
- Verificado em máquina de desenvolvimento: uma cópia instalada abriu sem `dotnet` no `PATH` e com pasta de dados descartável; a janela apareceu e o log registrou a versão 1.0.0.
- Limitações conhecidas: **CT21 pendente** (teste num Windows limpo sem nenhum .NET, em outra máquina ou VM); antivírus, versão/arquitetura do Windows-alvo (QV07) e instalação com atalhos no computador do balcão ainda não validados; sem assinatura de código; sem atualização automática; retorno à versão anterior é manual.

## 1.15 - 2026-09-21 - Acabamento (Etapa 7, fatia 3)

- As janelas nativas de aviso (segunda instância, erro inesperado, falha ao preparar o banco, restauração concluída) foram trocadas por uma janela de aviso grande, no mesmo padrão de toque da confirmação.
- A coluna "Ativo" da lista de Produtos mostra "Sim/Não".
- Fotos entram no backup: um `.db.fotos.zip` ao lado do banco, criado só quando as fotos mudam (evita duplicar a biblioteca a cada cópia), com os 3 mais recentes por pasta (local e externa). A restauração devolve as fotos sem apagar nenhuma existente. Resolve a limitação declarada em 1.12.
- Na inicialização o log registra quantas fotos não pertencem a nenhum produto (só a contagem); nada é apagado automaticamente.
- Documentação: correspondência entre os contratos de `docs/15` e o código.
- Limitações conhecidas: o zip de fotos é gerado no thread da interface (só quando as fotos mudam); `corrompido-*.db.bak` continua sem poda.

## 1.14 - 2026-09-21 - Navegação por teclado e foco visível (Etapa 7, fatia 2)

- Foco visível de alto contraste (contorno azul de 3 px) em botões, campos, caixas de seleção e datas — RNF18.
- Atalhos: `Ctrl+1..5` trocam de tela (respeitando o modo de restauração), `Ctrl+N` foca o número da comanda, `Ctrl+F` foca a busca, `F4` finaliza, `Esc` volta.
- Atendimento: digitar o número da comanda e `Enter` abre a comanda livre ou seleciona a que já está aberta; caixa de busca de produto com `Enter` adicionando o primeiro resultado (ignorado com busca vazia); `↑/↓` selecionam o item, `+`/`-` ajustam a quantidade e `Delete` remove (só fora de campos de texto e do menu).
- Encerramento: `Esc` volta para a comanda; `Enter` só confirma depois de marcar que a cobrança foi aprovada.
- Formulários de Produtos e Configurações têm botão padrão (`Enter`); `Ctrl+F` busca em Produtos e Histórico.
- Testes que carregam todos os XAML em STA (pegam recurso ausente sem abrir o app).
- Limitações conhecidas: sem estilo de foco para listas e cabeçalhos de colunas; a cor do foco não acompanha o modo de alto contraste do Windows; nomes de acessibilidade (leitor de tela) só nos campos de número e busca; a tecla `s` (sem Alt) aciona "Sim" nas confirmações; a última rodada de ajustes de foco ainda não foi conferida em execução real.

## 1.13 - 2026-09-20 - Robustez operacional (Etapa 7, fatia 1)

- Logs com rotação diária e por tamanho (5 MB por arquivo, 30 arquivos retidos, teto de cerca de 150 MB) — RNF21.
- Logs sem dados sensíveis: perfil do usuário, pasta de backup externa e pasta de dados são mascarados — RF26.
- Falhas técnicas que antes eram engolidas pelas telas agora são registradas no log local (só operação e exceção) — RNF16.
- Recuperação de comandas abertas após reinício provada por testes (RF27, RN22, RNF20/CT20) e registrada no log de inicialização.
- Banco corrompido: o aplicativo abre em modo de restauração em vez de fechar; a restauração funciona mesmo com o banco ativo corrompido e guarda uma cópia bruta.
- Primeira instalação não gera mais backup preventivo nem aviso espúrio; a restauração limpa arquivos `-wal`/`-shm`/`-journal`.
- Testes de infraestrutura passam a rodar em série (o pool global do SQLite causava falhas intermitentes).
- Limitações conhecidas: reiniciar após restaurar consome um dos 30 arquivos de log do dia; `corrompido-*.db.bak` não é podado; não há alerta de pouco espaço em disco (R15).

## 1.12 - 2026-09-19 - Foto do produto

- Produtos passam a ter uma foto opcional: "Escolher foto..." e "Remover
  foto" no formulário da tela de Produtos (para um produto já salvo e
  selecionado), com preview.
- A foto aparece nos cards do menu da tela de Atendimento; produto sem
  foto (ou com arquivo ausente/corrompido) continua mostrando o
  placeholder cinza.
- A imagem é copiada para `%LOCALAPPDATA%\VarthexComanda\fotos` (formatos
  JPG/PNG/BMP, até 10 MB); o banco guarda só o nome do arquivo. Trocar ou
  remover apaga o arquivo antigo.
- Limitação conhecida: o backup ainda copia só o banco de dados — as fotos
  não vão junto (resolvido em 1.15).

## 1.11 - 2026-09-18 - Redesign da tela de Atendimento

- Grade de comandas passa a mostrar um número fixo de slots numerados
  (configurável em Configurações, 20 por padrão), cada um marcado como
  "aberta" (com total e tempo desde a abertura) ou "livre" — em vez da
  lista dinâmica anterior só com comandas já abertas.
- Tela de edição de uma comanda passa a mostrar o menu de produtos como
  uma grade de cards (com placeholder de foto) filtrável por categoria,
  e o carrinho como uma tabela com colunas Descrição/Qtd./Preço/Total.
- Botão "Finalizar comanda" ganha o atalho de teclado F4; continua
  abrindo a mesma tela de Encerramento já existente desde a Etapa 4.
- Toda a lógica de negócio (abrir, adicionar/alterar/remover item,
  cancelar, encerrar) foi reaproveitada sem alteração — esta fatia é
  só a reconstrução visual da tela.

## 1.10 - 2026-09-18 - Configurações do estabelecimento (RF25)

- Nova tela "Configurações": nome do estabelecimento, quantidade máxima de
  comandas e pasta de backup externa padrão, persistidos na tabela
  `configuracao` já existente desde a base técnica.
- `AbrirComanda` passa a respeitar a quantidade máxima configurada (sem
  teto enquanto nada for configurado, preservando o comportamento
  anterior) — resolve QV02.
- O backup automático do encerramento do app (`CriarBackupAutomatico`,
  ramo incondicional) passa a também copiar para a pasta externa
  configurada, quando houver uma — cumprindo a política de cópia externa
  ao fim do dia (docs/08) sem exigir clique manual. A abertura do dia
  continua só na pasta gerenciada.

## 1.9 - 2026-09-18

- backup e recuperação (Etapa 6, RF21-24): backup automático na primeira
  abertura do dia e ao encerrar o app, backup manual com opção de pasta
  externa, validação de formato/versão/integridade/checksum, e restauração
  com cópia preventiva da base atual e reinício automático do aplicativo;
  motor de backup passa a usar a API de snapshot nativa do SQLite em vez de
  cópia de arquivo direta; retenção mantém as 30 cópias mais recentes na
  pasta gerenciada.

## 1.8 - 2026-09-18

- histórico e resumo (Etapa 5, RF18-20): tela combinando consulta de vendas
  por data, localização pelo número da comanda, detalhe dos itens de uma
  venda e resumo diário (quantidade, total, ticket médio); filtro de data
  respeita o fuso de Brasília (RN20) mesmo com os horários gravados em UTC;
  estado vazio quando não há vendas concluídas na data selecionada.

## 1.7 - 2026-09-18

- encerramento e venda (Etapa 4, RF14-17): tela de encerramento exibindo itens,
  preços e total a pagar; confirmação manual de cobrança aprovada fora do sistema;
  encerramento grava a venda e fecha a comanda em uma única transação (RN13-15);
  número da comanda é liberado após o fechamento; comanda vazia não pode ser
  encerrada (RN09).

## 1.6 - 2026-09-17

- implementadas comandas (Etapa 3): grade de números abertos, abertura,
  catálogo rápido, lançamento/alteração/remoção de itens e cancelamento
  (RF06-13);
- lançar o mesmo produto duas vezes na mesma comanda incrementa a
  quantidade de uma única linha em vez de duplicar, desde que o preço
  não tenha mudado entre os dois lançamentos;
- abrir comanda não faz pré-checagem de número livre — insere direto e
  deixa o índice único do banco ser a fonte da verdade, cobrindo o caso
  de concorrência (RN01);
- `MainWindow` deixa de mostrar só Produtos e vira um shell com dois
  botões (Atendimento, Produtos) — Atendimento abre por padrão, conforme
  a tela inicial esperada;
- criado o projeto `VarthexComanda.Desktop.Tests`, com testes de unidade
  reais para `AtendimentoViewModel` e (retroativamente) `ProdutosViewModel`
  — fecha a lacuna que a Etapa 2 deixou aberta e que tinha deixado passar
  dois bugs reais;
- ainda sem encerramento nem venda (RF14-17) — entra na próxima fatia.

## 1.5 - 2026-09-17

- implementado o catálogo (Etapa 2): cadastro e busca de produtos, cadastro de
  categorias (RF01, RF03, RF05); os casos de uso de alteração e desativação de
  categoria (RF02) existem e têm testes, mas ainda não têm tela própria —
  produtos podem ser alterados e desativados pela tela "Produtos", categorias
  ainda não;
- casos de uso de catálogo validam nome obrigatório, preço positivo em centavos e
  categoria ativa, sem exceções para erros esperados;
- repositórios de categoria e produto sobre `IDbContextFactory`, com DbContext
  de curta duração por operação;
- composição do Desktop passa a usar `Microsoft.Extensions.DependencyInjection`
  de verdade (container, `IDbContextFactory` registrado, ViewModels resolvidas
  pelo container) — fecha a pendência de DI/MVVM deixada em aberto na Etapa 0+1;
- `MainWindow` deixa de ser uma janela vazia e passa a exibir a tela "Produtos";
- limitação conhecida: busca por nome e detecção de duplicidade de categoria
  ignoram maiúsculas/minúsculas apenas em caracteres ASCII (SQLite `LOWER()`/
  `NOCASE` não tratam acentos) — nomes acentuados como "Açaí" podem não bater
  em buscas ou podem ser duplicados sob acentuação diferente; correção própria
  fica para uma fatia futura;
- ainda sem comandas, itens ou vendas (RF06+) — entra na próxima fatia.

## 1.4 - 2026-09-16

- criada a base técnica do código em `backend/` (Etapas 0 e 1 do guia de implementação);
- solução .NET 10 com Domain, Application, Infrastructure e Desktop (WPF);
- SQLite + EF Core mapeados a partir de `database/schema.sql`, com migração inicial;
- instância única via mutex, backup preventivo antes de migrar e logging rotativo com Serilog;
- ainda sem telas ou regras de negócio (RF01+) — entra na próxima fatia.

## 1.3 - 2026-09-13

- adotado o nome oficial Varthex Comanda;
- confirmada a família do sistema operacional Windows;
- substituída a proposta Java por C# .NET 10 LTS WPF e SQLite;
- definido Entity Framework Core para persistência e migrações;
- incluída publicação autocontida para Windows;
- adicionadas regras de instância única e recuperação de comandas abertas;
- adicionados controle de rotação de logs e testes específicos de Windows;
- incluídos guia técnico da plataforma e ADR da stack;
- mantido pagamento completamente fora do MVP.

## 1.2 - 2026-09-13

- retirado o processamento de pagamentos do MVP;
- retirada a entidade `PAGAMENTO` do modelo e do SQL;
- definido que o sistema apenas exibe o total a pagar;
- definida digitação manual do total na maquininha;
- incluída confirmação explícita antes do encerramento;
- registrada integração com maquininha somente como evolução futura;
- criado pacote documental para implementação pelo GitHub.

## 1.1 - 2026-09-13

- documento consolidado em padrão ABNT;
- incluídos requisitos, regras, casos de uso, modelo de dados, arquitetura, testes, riscos e roadmap.

## 1.0 - 2026-09-13

- definição inicial do sistema local de comandas.
