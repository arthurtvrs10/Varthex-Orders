# Etapa 7 · Robustez operacional — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Logs com rotação/limite e sem dados sensíveis (RNF21, RF26), falhas
técnicas de fato registradas (RNF16), recuperação de comandas abertas provada
por teste (RF27/RN22/RNF20) e modo de restauração quando o banco está
corrompido.

**Architecture:** `LoggingConfigurator` ganha `LogOptions` e um sink decorador
que mascara caminhos; ViewModels e `CriarBackupAutomatico` recebem
`ILogger? logger = null` opcional; testes de reinício sobre o mesmo arquivo
SQLite; `MainWindow` ganha um modo de restauração.

**Tech Stack:** C#/.NET 10, WPF, Serilog 4.4 / Serilog.Sinks.File 7.0, EF Core
SQLite, xUnit.

**Spec:** [specs/2026-09-20-etapa7-robustez-design.md](2026-09-20-etapa7-robustez-design.md)

## Global Constraints

- Prefixar todo `dotnet` com `export PATH="$PATH:/c/Program Files/dotnet" &&`
  (Git Bash). Solução: `backend/VarthexComanda.slnx`.
- **PROIBIDO abrir/rodar o aplicativo** (`dotnet run`, executar o exe) e mexer em
  `%LOCALAPPDATA%\VarthexComanda` real. Testes usam pastas temporárias.
- Commits em português no estilo do projeto (`feat:`/`fix:`/`test:`), **sem**
  trailer `Co-Authored-By`.
- Defaults do log: `TamanhoMaximoArquivoBytes = 5 * 1024 * 1024`,
  `QuantidadeMaximaArquivos = 30`, prefixo `varthex-comanda-`, extensão `.log`.
- Logs de falha **não** carregam dados de negócio (nome de produto, preço,
  total, número de comanda, caminho de pasta do usuário). Só: operação, tipo da
  exceção, mensagem/stack com caminhos mascarados.
- ViewModels continuam sem `System.Windows`/`Microsoft.Win32`.
- `ILogger` é `Serilog.ILogger`. Parâmetro opcional final: `ILogger? logger = null`.
- Suíte hoje: 240 testes, todos verdes. Nenhum teste existente pode ser
  removido; os novos somam.

---

## Task 1: Rotação, retenção e limite de tamanho (RNF21 / CT22)

**Files:**
- Create: `backend/src/VarthexComanda.Infrastructure/Logging/LogOptions.cs`
- Modify: `backend/src/VarthexComanda.Infrastructure/Logging/LoggingConfigurator.cs`
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/LoggingConfiguratorTests.cs`

**Interfaces:**
- Produces: `public sealed record LogOptions(long TamanhoMaximoArquivoBytes = 5 * 1024 * 1024, int QuantidadeMaximaArquivos = 30)` e
  `LoggingConfigurator.CreateLogger(string logsDirectory, LogOptions? options = null)` (assinatura antiga continua válida).

- [ ] **Step 1: Testes que falham** (`LoggingConfiguratorTests`, comentário de rastreio `// CT22, RNF21`):
  - `CreateLogger_RolaPorTamanho`: `LogOptions(TamanhoMaximoArquivoBytes: 2048, QuantidadeMaximaArquivos: 30)`; escreve ~200 linhas de 100 caracteres; após dispose há **≥ 2** arquivos `varthex-comanda-*.log` e nenhum passa de 2048 + uma linha de folga (assert `<= 2048 + 512`).
  - `CreateLogger_RetemNoMaximoNArquivos`: `LogOptions(1024, 3)`; escreve o bastante para gerar ≥ 8 rolagens; após dispose há **exatamente 3** arquivos e o **conteúdo mais recente** (marcador escrito por último) está em algum deles.
  - Manter o teste existente verde.
- [ ] **Step 2: Rodar** `dotnet test backend/tests/VarthexComanda.Infrastructure.Tests --filter LoggingConfiguratorTests` — os novos falham.
- [ ] **Step 3: Implementar** `LogOptions` e usar em `WriteTo.File(... rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true, fileSizeLimitBytes: options.TamanhoMaximoArquivoBytes, retainedFileCountLimit: options.QuantidadeMaximaArquivos ...)`. Valide `TamanhoMaximoArquivoBytes > 0` e `QuantidadeMaximaArquivos >= 1` (`ArgumentOutOfRangeException`). Comentário curto no configurador explicando o teto (`arquivos × tamanho`) e que 30 arquivos ≈ 30 dias em uso normal.
- [ ] **Step 4: Rodar testes do projeto** — verdes.
- [ ] **Step 5: Commit** `feat: log com rotacao por tamanho e retencao limitada (RNF21)`.

---

## Task 2: Máscara de dados sensíveis nos logs (RF26)

**Files:**
- Create: `backend/src/VarthexComanda.Infrastructure/Logging/MascaraCaminhosSink.cs` (e, se preferir, `LogMascara.cs` com a função pura)
- Modify: `backend/src/VarthexComanda.Infrastructure/Logging/LoggingConfigurator.cs`
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/LogMascaraTests.cs`

**Interfaces:**
- Consumes: `LoggingConfigurator.CreateLogger` da Task 1.
- Produces: `public static class LogMascara { public static string Aplicar(string texto, string? perfilUsuario = null); }` — troca ocorrências do perfil (por padrão `Environment.GetFolderPath(SpecialFolder.UserProfile)`), com `\` ou `/`, sem diferenciar maiúsculas, por `%USERPROFILE%`; texto nulo/vazio volta como veio; perfil vazio → sem alteração.

- [ ] **Step 1: Testes que falham** (`// RF26, docs/08`):
  - `Aplicar` troca `C:\Users\maria\AppData\Local\X` por `%USERPROFILE%\AppData\Local\X` com perfil `C:\Users\maria`; também `c:/users/MARIA/...`; não altera texto sem o perfil; várias ocorrências.
  - Teste ponta a ponta com o logger real e uma pasta temporária: registrar `logger.Information("Banco em {Caminho}", perfil + @"\AppData\banco.db")` e `logger.Error(new IOException("falha em " + perfil + @"\x.jpg"), "Falha")`; ler o arquivo: **não contém** o perfil (nem `Environment.UserName` como segmento de caminho `\<UserName>\`), **contém** `%USERPROFILE%`, e no caso da exceção contém o tipo `IOException` e a palavra `Falha`.
    Como o perfil real do usuário do teste é o alvo, o teste usa `Environment.GetFolderPath(UserProfile)`; se estiver vazio, `Skip`.
- [ ] **Step 2: Rodar** — falham.
- [ ] **Step 3: Implementar**: sink decorador (`ILogEventSink`) que renderiza `logEvent.RenderMessage()`, aplica `LogMascara`, e monta um novo `LogEvent` (mesmo timestamp/level; template = a mensagem já mascarada como texto literal, **escapando `{`/`}`** ou usando `MessageTemplate` com um único `{Mensagem}` e propriedade) e, se houver exceção, converte-a em texto (`ex.GetType().FullName + ": " + ex.Message` + `\n` + `ex.StackTrace`, mascarado) anexado à mensagem no campo da exceção do output template (uma exceção sintética `ExcecaoMascarada` com `ToString()` mascarado é aceitável). Ligar em `CreateLogger`: `WriteTo.Sink(new MascaraCaminhosSink(arquivoSink))` — pode-se usar `WriteTo.Logger(lc => lc.WriteTo.File(...))` + `Enrich`/`Filter`; escolha a forma mais simples que passa nos testes e **não** perca a rotação da Task 1 (os testes da Task 1 devem continuar verdes).
- [ ] **Step 4: Rodar suíte do projeto** — verde.
- [ ] **Step 5: Commit** `feat: mascara o perfil do usuario nos logs (RF26)`.

---

## Task 3: Registrar de fato as falhas técnicas (RNF16 / RF26)

**Files (modificar):**
- `backend/src/VarthexComanda.Desktop/Atendimento/AtendimentoViewModel.cs`, `EncerramentoViewModel.cs`, `HistoricoViewModel.cs`
- `backend/src/VarthexComanda.Desktop/Backup/BackupViewModel.cs`
- `backend/src/VarthexComanda.Desktop/Catalogo/ProdutosViewModel.cs`
- `backend/src/VarthexComanda.Desktop/Configuracao/ConfiguracaoViewModel.cs`
- `backend/src/VarthexComanda.Application/Backup/CriarBackupAutomatico.cs`
- `backend/src/VarthexComanda.Infrastructure/Backup/EfBackupService.cs` (injetar/usar logger opcional nos dois `catch (Exception)` que só devolvem `ex.Message`)
- `backend/src/VarthexComanda.Desktop/App.xaml.cs` (a linha `Banco pronto em {Caminho}` passa a `Banco pronto`; nenhuma outra mudança nesta task)
- Test: novos arquivos em `backend/tests/VarthexComanda.Desktop.Tests/` (um `LogDeFalhasTests.cs`) e `Application.Tests/Backup`

**Interfaces:**
- Consumes: `Serilog.ILogger`.
- Produces: cada ViewModel acima aceita `ILogger? logger = null` como **último** parâmetro do construtor; cada `catch (Exception ex)` que hoje mostra `Mensagem` amigável passa a chamar `_logger?.Error(ex, "Falha em {Operacao}", "<nome curto da operação>")` **antes** de montar a mensagem (mesma mensagem amigável ao usuário, sem mudança de comportamento visível). `CriarBackupAutomatico`: o `catch` vazio passa a `_logger.Warning(ex, "Backup automatico falhou")`. `EfBackupService`: construtor ganha `ILogger? logger = null` final; os catch em `CriarBackupGerenciado`/`RestaurarPara` (e demais que só devolvem `Falha(ex.Message)`) fazem `_logger?.Error(ex, "Falha em {Operacao}", ...)`.

- [ ] **Step 1: Testes que falham**: com um `ILogger` de teste (crie `ColetorDeLog : ILogEventSink` em `backend/tests/.../Suporte/` — ou use `LoggerConfiguration().WriteTo.Sink(coletor)`), para **cada** ViewModel: forçar uma falha (fake/stub que lança) e afirmar (a) `Mensagem` amigável igual à de hoje, (b) o coletor recebeu 1 evento `Error` com a exceção e propriedade `Operacao`, (c) o texto renderizado **não** contém dados de negócio usados no teste (nome do produto, preço). Cobrir ao menos: `AtendimentoViewModel` (uma operação), `EncerramentoViewModel`, `HistoricoViewModel`, `BackupViewModel`, `ProdutosViewModel`, `ConfiguracaoViewModel`, `CriarBackupAutomatico` (Warning), `EfBackupService` (restaurar com arquivo inexistente **não** loga; falha real de IO loga).
  Use `[Theory]` onde ajudar. Sem logger (`null`) nada muda e nada lança — 1 teste por VM cobre isso (os testes existentes já cobrem).
- [ ] **Step 2: Rodar** — falham.
- [ ] **Step 3: Implementar** conforme Interfaces. Conferir por `Grep "catch (Exception"` em `backend/src` que **nenhum** ficou sem log (exceto os catch de I/O intencionais em `ArquivoFotoStorage`, que já logam, e `App.ReiniciarAplicativo`, que deve logar `Warning`). Registrar em `AtendimentoViewModel` etc. **só** o nome da operação (ex.: "AdicionarItem"), nunca argumentos.
- [ ] **Step 4: Rodar a solução inteira** — `dotnet test backend/VarthexComanda.slnx` verde, sem avisos novos.
- [ ] **Step 5: Commit** `feat: registra falhas tecnicas no log local (RNF16, RF26)`.

---

## Task 4: Recuperação de comandas abertas (RF27 / RN22 / RNF20 / CT20)

**Files:**
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/Atendimento/RecuperacaoDeAtendimentoTests.cs`
- Modify: `backend/src/VarthexComanda.Desktop/App.xaml.cs` (log de recuperação no startup)

**Interfaces:**
- Consumes: `EfComandaRepository`, `EfProdutoRepository`/DbContextFactory dos testes existentes (copie o helper de fixture de `EfComandaRepositoryTests`).

- [ ] **Step 1: Testes** (comentário `// RF27, RN22, RN23, RNF20, CT20`), todos sobre **um arquivo SQLite real em pasta temporária** e **repositórios novos por fase** (simula reinício; use `SqliteConnection.ClearAllPools()` entre fases para garantir que nada está em cache):
  1. `ComandasAbertas_ReaparecemAposReinicio_ComMesmosItensETotais`: abre 3 comandas (uma com 1 item, uma com vários itens repetidos, uma vazia); "reinicia"; `ListarAbertas` devolve as 3 com os mesmos números/status `Aberta`; `BuscarComItens` de cada devolve os mesmos itens, quantidades, preços e total.
  2. `Reinicio_NaoCriaVendaNemAlteraStatus` (RN22): antes e depois há 0 vendas e nenhuma comanda mudou de status.
  3. `Reinicio_NaoDuplicaNemDescartaItemConfirmado`: quantidade de linhas de item idêntica antes/depois; alterar a quantidade de um item, "reiniciar", o valor confirmado persiste (RN23).
  4. `ComandaEncerrada_NaoReaparecemComoAberta`: uma comanda encerrada continua fora de `ListarAbertas` e sua venda existe uma única vez após reinício.
  5. Título dos testes referenciando os códigos (`RF27`, `CT20`) no nome ou `[Trait("Requisito","RF27")]` — adotar `[Trait("Requisito", "...")]` **em todos** e listar no relatório o formato usado (a fatia 4 reutiliza).
- [ ] **Step 2: Rodar** — devem **passar de primeira** (o comportamento já existe). Se algum falhar, é bug real: corrija o código de produção mínimo e diga no relatório.
- [ ] **Step 3: `App.xaml.cs`**: após `Migrate()` e integridade OK, `_logger.Information("Comandas abertas recuperadas: {Quantidade}", <ListarAbertas().Count>)` usando `IComandaRepository` do container (só contagem; sem números nem valores).
- [ ] **Step 4: Rodar a solução inteira** — verde.
- [ ] **Step 5: Commit** `test: prova a recuperacao de comandas abertas apos reinicio (RF27/CT20)`.

---

## Task 5: Banco corrompido abre em modo de restauração

**Files:**
- Modify: `backend/src/VarthexComanda.Desktop/App.xaml.cs`
- Modify: `backend/src/VarthexComanda.Desktop/MainWindow.xaml`, `MainWindow.xaml.cs`
- Modify: `backend/src/VarthexComanda.Infrastructure/Backup/EfBackupService.cs` (`RestaurarPara`)
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/Backup/EfBackupServiceTests.cs` (novos casos); `backend/tests/VarthexComanda.Desktop.Tests/` se houver lógica de ViewModel nova.

**Interfaces:**
- Produces: `MainWindow.EntrarModoRestauracao()` (público): desabilita os botões Atendimento/Produtos/Histórico/Configurações, mostra por padrão a view de Backup (atualizando a lista) e exibe uma faixa no topo com o texto exato `"O banco de dados está corrompido. Restaure um backup para continuar. Nenhuma outra função está disponível."`.
- `RestaurarPara` passa a funcionar com o banco ativo corrompido.

- [ ] **Step 1: Testes que falham** (Infrastructure, `EfBackupServiceTests`, estilo dos existentes):
  - `RestaurarPara_ComBancoAtivoCorrompido_RestauraEGuardaCopiaBruta`: preparar um `AppPaths` temporário; criar um backup válido; gravar bytes de lixo (ou um SQLite truncado) em `DatabasePath`; `RestaurarPara(backupValido)` → `Sucesso`; `DatabasePath` passa em `PRAGMA integrity_check`; existe em `BackupsDirectory` um arquivo `corrompido-*.db.bak` com os bytes originais do banco corrompido.
  - `RestaurarPara_ComBancoAtivoSaudavel_ContinuaUsandoBackupGerenciado` (regressão: não cria `corrompido-*`).
  - `RestaurarPara_ArquivoCorrompidoComoOrigem_ContinuaFalhando` (regressão: base ativa **não** é alterada).
- [ ] **Step 2: Rodar** — o primeiro falha.
- [ ] **Step 3: Implementar em `RestaurarPara`**: se `CriarBackupGerenciado()` falhar **e** a verificação de integridade do banco ativo indicar corrupção (ou o arquivo nem abre como SQLite), copiar `DatabasePath` bruto (`File.Copy`, com `-wal`/`-shm` se existirem não é necessário) para `BackupsDirectory\corrompido-{yyyy-MM-dd-HHmmss}.db.bak` (hora via o mesmo relógio/`IClock` já usado no serviço) e seguir com a restauração; se a cópia bruta também falhar, `Falha("Não foi possível guardar uma cópia do banco atual; restauração cancelada.")`. Se o preventivo falhou **e o banco está saudável**, mantém o comportamento atual (falha).
  Retorno em caso de sucesso: um `BackupRegistro` coerente (o registro da cópia bruta com `Tipo` adequado ao existente — reutilize o enum/valores atuais; se não houver valor apropriado, use o de preventivo e comente).
- [ ] **Step 4: `App.xaml.cs` + `MainWindow`**: no ramo `linhas.Count != 1 || linhas[0] != "ok"`: registrar o erro, **resolver `MainWindow`, chamar `EntrarModoRestauracao()` e `Show()`**, marcar `_startupConcluido = false` (sem backup automático no `OnExit`) e **não** chamar `Shutdown()`; substituir o `MessageBox` por essa faixa (não há diálogo). O `BackupViewModel` já pede confirmação e reinicia o app após restaurar — manter. `AtendimentoViewModel`/`ProdutosView` etc. **não** devem ser tocados por nenhum código nesse modo além de serem construídos (o construtor de `AtendimentoViewModel` chama `ListarAbertas` — se isso lançar com o banco corrompido, a criação do `MainWindow` falha: nesse caso o construtor precisa ser tolerante — capturar, registrar e seguir com lista vazia, com teste em `AtendimentoViewModelTests`). Investigue e reporte o que encontrou.
- [ ] **Step 5: Rodar a solução inteira** — verde. **Não abrir o app**; a verificação visual será feita depois por mim com dados isolados.
- [ ] **Step 6: Commit** `feat: banco corrompido abre em modo de restauracao em vez de fechar o app`.
