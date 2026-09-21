# Etapa 8 · Homologação — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ligar cada caso de teste de aceitação (CT01–CT22) a evidência
automatizada, gerar a massa mínima, executar ensaios com o app real em dados
descartáveis e deixar os documentos de homologação prontos para quem opera.

**Architecture:** trait `Caso` + teste de reflexão de rastreabilidade; projeto
console `VarthexComanda.MassaMinima` (biblioteca de geração + `Main`); script
PowerShell de ensaio por UI Automation; documentos em `docs/homologacao/`.

**Tech Stack:** C#/.NET 10, xUnit, EF Core SQLite, PowerShell 5.1, UI Automation.

**Spec:** [specs/2026-09-21-etapa8-homologacao-design.md](2026-09-21-etapa8-homologacao-design.md)

## Global Constraints

- `dotnet` com `export PATH="$PATH:/c/Program Files/dotnet" &&`. Solução:
  `backend/VarthexComanda.slnx`. Suíte atual: **503** testes verdes (ajustar se a
  contagem mudou ao mesclar).
- **PROIBIDO** tocar em `%LOCALAPPDATA%\VarthexComanda` real (nem ler). Toda
  execução do app usa `VARTHEX_COMANDA_DADOS` apontando para pasta descartável
  dentro da pasta temporária/scratchpad. **Nunca enviar teclas** (`SendKeys`);
  só `InvokePattern`/`ValuePattern` do UI Automation. Nunca encerrar processo
  que você não iniciou; aborte se já existir `VarthexComanda` em execução.
- Traits: `[Trait("Caso", "CT03")]` (um por caso; repetir para vários) e
  `[Trait("Requisito", "RFxx")]` já existentes. Não renomear testes existentes
  só para colocar trait — apenas **adicione** o atributo.
- Massa mínima: sem dados reais, nomes genéricos ("Lanchonete Exemplo",
  produtos comuns), fotos sintéticas geradas (nada de imagens de terceiros).
- Commits em português, **sem** `Co-Authored-By`.
- Docs em Markdown com CRLF já existente: edite preservando CRLF (Node script
  que normaliza e restaura), confira com `git diff --stat`.

---

## Task 1: Traits `Caso`, lacunas de cobertura e teste de rastreabilidade

**Files:**
- Modify (adicionar `[Trait("Caso","CTnn")]`): testes existentes em `backend/tests/**` que cobrem CT01–CT12, CT14–CT20, CT22 (leia `docs/docs/09-testes-aceitacao.md` para o critério de cada CT).
- Create: `backend/tests/VarthexComanda.Infrastructure.Tests/Homologacao/` com testes que faltarem (candidatos: **CT11** rollback por falha entre venda e fechamento — injeção de falha via `DbContext` de teste/interceptor que lança no `SaveChanges` da venda ou do fechamento; **CT16** atualizar aplicação — banco criado só até a migração `InitialCreate` (`IMigrator.Migrate("<id da InitialCreate>")`), com produto, comanda encerrada e venda inseridos por SQL, depois `Migrate()` até a última; totais e vendas preservados, coluna `foto_arquivo` nula; **CT13** por construção; **CT05/CT06** se ainda sem teste direto; **CT08** total).
- Create: `backend/tests/VarthexComanda.Infrastructure.Tests/Homologacao/RastreabilidadeDosCasosTests.cs`

**Interfaces:** o teste de reflexão percorre os assemblies de teste carregados no processo `Infrastructure.Tests` — **para ver os outros projetos**, ele localiza os `*.Tests.dll` ao lado do assembly em execução (`AppContext.BaseDirectory\..` não serve; use as referências de projeto: adicione `ProjectReference` de `Infrastructure.Tests` para `Application.Tests` e `Domain.Tests`; para `Desktop.Tests` (net10.0-windows) adicione também, se o `TargetFramework` permitir — se não permitir, ponha o teste de rastreabilidade em `Desktop.Tests`, que é o único que referencia todos... escolha o projeto que compila sem hacks e explique).

- [ ] **Testes primeiro** para as lacunas listadas (CT11, CT16, CT13).
  - CT13 por construção: um teste que carrega os assemblies `VarthexComanda.Domain/Application/Infrastructure/Desktop` e falha se qualquer um referenciar `System.Net.Http`, `System.Net.Sockets`, `System.Net.WebClient`/`System.Net.WebSockets` em `GetReferencedAssemblies()` (a exceção conhecida: `System.Net.*` puxado por `Microsoft.Data.Sqlite`/EF não conta porque só analisamos os quatro assemblies do produto).
  - Comentário nos testes: `// CTnn — critério de docs/docs/09-testes-aceitacao.md`.
- [ ] **Teste de rastreabilidade**: coleta `[Trait("Caso", …)]` em todos os métodos `[Fact]`/`[Theory]` dos assemblies de teste; mapa `CT → testes`; **exige ≥ 1** teste para cada um de CT01–CT20 e CT22 (CT21 = manual; se CT13 tiver só a verificação estrutural, ela conta com a nota "estrutural"); imprime a tabela Markdown quando a variável de ambiente `VARTHEX_GERAR_RASTREABILIDADE` tiver um caminho (grava o arquivo lá) — o teste **não** escreve no repo por padrão.
- [ ] Rodar a solução inteira (verde). Gerar a tabela para `docs/homologacao/rastreabilidade-testes.md` (crie a pasta), com cabeçalho "Gerado por `RastreabilidadeDosCasosTests`; não editar à mão" e a coluna "Tipo" (`automatizado` / `estrutural` / `manual`).
- [ ] Commit `test: rastreabilidade executavel dos casos CT01-CT22 e lacunas CT11/CT13/CT16`.

---

## Task 2: Massa mínima (gerador reproduzível)

**Files:**
- Create: `backend/tools/VarthexComanda.MassaMinima/VarthexComanda.MassaMinima.csproj` (`net10.0-windows` se precisar de `System.Drawing` para fotos; senão gerar PNG mínimo válido por bytes e usar `net10.0`), `Program.cs`, `MassaMinimaGerador.cs`
- Modify: `backend/VarthexComanda.slnx` (adicionar o projeto)
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/Homologacao/MassaMinimaTests.cs`

**Interfaces:**
- `public static class MassaMinimaGerador { public static ResumoMassa Gerar(string pastaRaiz, DateTime agoraUtc, bool forcar = false); }` — `ResumoMassa` (record) com contagens: categorias, categoriasInativas, produtos, produtosInativos, comandasAbertas, vendas, itensEmVendas, diasDeHistorico, backups (valido/antigo/corrompido).
- CLI: `dotnet run --project backend/tools/VarthexComanda.MassaMinima -- --saida <pasta> [--forcar]`.

- [ ] Reaproveite `AppPaths(pastaRaiz)`, migrações, `EfXRepository`/casos de uso reais, `EfBackupService` (para o backup válido). Backup **antigo** = cópia de um banco no esquema da `InitialCreate` (migrar só até ela); **corrompido** = arquivo com o nome padrão e conteúdo inválido (bytes aleatórios fixos por semente).
- [ ] Determinístico: `Random` com semente fixa; datas relativas a `agoraUtc` (parâmetro), 90 dias de vendas (várias por dia útil, valores plausíveis), encerradas via os casos de uso/`EncerrarComanda` com o horário explícito.
- [ ] Segurança: recusa `pastaRaiz` igual ou dentro de `%LOCALAPPDATA%\VarthexComanda`; recusa pasta não vazia sem `forcar`; mensagens claras; retorna código de saída ≠ 0.
- [ ] Testes: gerar em pasta temporária e conferir **exatamente** as contagens do design (5/1, 30/1, 20 números na configuração, ≥ 3 comandas abertas com perfis diferentes, ≥ 90 dias de histórico, 3 arquivos de backup com a classificação certa via `IBackupService.Validar`); o banco gerado abre com o app real? — não abra o app; valide `PRAGMA integrity_check` = ok e que `Migrate()` não tem migrações pendentes; recusa da pasta real.
- [ ] Rodar a solução inteira (verde). Commit `feat: gerador da massa minima de homologacao`.

---

## Task 3: Ensaios com o app real (UI Automation, sem teclas)

**Files:**
- Create: `scripts/homologacao/Executar-Ensaio.ps1`, `scripts/homologacao/Ajuda.ps1` (funções: captura de janela, achar botão por nome/AutomationId, aguardar janela, encerrar PID próprio)
- Create (saída, commitada): `docs/homologacao/evidencias/ensaio-AAAA-MM-DD.txt` (transcript do script, sem caminhos com nome de usuário — mascare `%USERPROFILE%`)

**Ensaios** (cada um imprime `PASSOU`/`FALHOU` + evidência; código de saída ≠ 0 se algo falhar). Pré-requisitos: pacote publicado (`scripts\publicar.ps1` já existe) instalado em pasta temporária por `Instalar.ps1 -Destino`; dados gerados por `MassaMinima --saida <pasta descartável>`.
- [ ] **E1 RNF03**: 5 aberturas consecutivas, tempo do `Start-Process` até `MainWindowHandle` + `IsVisible`; média ≤ 5 s (imprima cada tempo).
- [ ] **E2 CT19**: com uma instância aberta, iniciar a segunda com as mesmas variáveis; esperar que ela **encerre sozinha** em ≤ 10 s, que exista uma janela "Varthex Comanda" de aviso (título/mensagem "O Varthex Comanda já está aberto neste computador.") e que continue existindo **um** processo com janela principal; fechar o aviso por `InvokePattern` no botão OK; verificar que o log não registra segunda abertura do banco.
- [ ] **E3 CT20 / RNF20**: abrir uma comanda livre pelo botão do slot (`InvokePattern`), adicionar 2 itens (cards), ler o total exibido; `Stop-Process -Force` **no PID iniciado por você** (término forçado); reabrir; verificar que a mesma comanda aparece aberta com o mesmo total e que o log traz `Comandas abertas recuperadas: N` com N igual ao esperado; verificar que nenhuma venda nova foi criada (consulte o banco pelo gerador/ferramenta C# pequena fora do repo ou pela contagem de vendas no Histórico do dia).
- [ ] **E4 erro da maquininha / CT09**: abrir comanda com itens, "Finalizar comanda" (Invoke), na janela de Encerramento **não** marcar a cobrança e clicar "Voltar para a comanda" (Invoke); comanda continua aberta, total igual, sem venda nova.
- [ ] **E5 restauração**: pela tela de Backup, escolher a cópia válida da massa mínima e restaurar; o app reinicia sozinho; conferir o histórico/estado esperado (por exemplo, contagem de vendas do backup). Se a janela de confirmação exigir cliques, use `InvokePattern` nos botões "Sim". Depois repetir com o backup **corrompido** e verificar que o app **recusa** (mensagem) e o banco ativo não muda (hash do arquivo `.db` antes/depois).
- [ ] **E6 sem rede**: durante a sessão, `Get-NetTCPConnection -OwningProcess <pid>` e `netstat -ano` do PID: nenhuma conexão TCP/UDP remota (somente loopback/nada); registra evidência. Nota: não substitui o teste com a rede física desligada.
- [ ] Trava: aborta se `Get-Process VarthexComanda*` já existir; usa apenas pastas descartáveis; ao final remove a instalação temporária e a pasta de dados (mas guarda o transcript e as capturas de tela sem dados reais na pasta `evidencias`, se úteis, < 300 KB cada).
- [ ] Executar o script **uma vez** de ponta a ponta e commitar o transcript. Se algum ensaio revelar bug do produto, **pare** e reporte (não corrija em silêncio): descreva o defeito com evidência.
- [ ] Commit `test: ensaios de homologacao com o app real em dados descartaveis`.

---

## Task 4: Documentos de homologação

**Files (create):** `docs/homologacao/README.md` (índice + como rodar cada coisa), `checklist-etapa8.md`, `matriz-compatibilidade.md`, `resultados-ct.md`, `roteiro-ensaio-manual.md`, `guia-do-operador.md`, `pendencias.md`. **Modify:** `docs/docs/10-rastreabilidade.md` (linha do CT17; ponteiro para a matriz gerada), `docs/CHANGELOG.md` (entrada `1.17 – Homologação (Etapa 8)`).

- [ ] `checklist-etapa8.md`: os 8 itens de `docs/docs/14`, cada um com **situação real** (`Feito (automatizado)`, `Feito parcialmente – falta <coisa>`, `Pendente – exige pessoa`) e o link da evidência.
- [ ] `matriz-compatibilidade.md` (RNF17): colunas Windows/versão/arquitetura/RAM/resolução/escala/toque/resultado; linha "Máquina de desenvolvimento" preenchida com os dados **reais desta máquina** (Windows 11 Pro 10.0.26200, x64, ~14,9 GB RAM, 1920×1080, escala lida do sistema — leia via PowerShell e registre, sem nome de usuário) marcada como *referência, não é o computador-alvo*; linha "Computador da loja" em branco; escalas 100/150/200 % a testar.
- [ ] `resultados-ct.md`: tabela CT01–CT22 com: cenário, tipo de evidência (teste automatizado / ensaio E# / manual), situação (`Aprovado` só onde há evidência executada; `Pendente` senão), evidência (nomes dos testes/transcript), observações. **Honestidade**: CT13 físico e CT21 = `Pendente`; nunca marque `Aprovado` sem evidência.
- [ ] `roteiro-ensaio-manual.md`: passo a passo (em português, para uma pessoa) do que roda no PC da loja: gerar massa mínima com dados descartáveis, desligar a rede, CT13, CT17 (teclado), CT19, CT20 com término forçado (Gerenciador de Tarefas), reinício do PC com comanda aberta, escalas 100/150/200 %, instalação limpa CT21 (sem .NET), erro da maquininha com a maquininha real, restauração de backup, troca de pendrive; com caixas de seleção e campos para "Resultado obtido / Situação / Evidência".
- [ ] `guia-do-operador.md`: uma página: abrir comanda, adicionar, corrigir quantidade, ver total, digitar na maquininha, marcar aprovada, encerrar; o que fazer se a maquininha falhar (Voltar); atalhos (Ctrl+N, Ctrl+F, Enter, F4, Esc, +/−); backup; o que o aviso de banco corrompido significa; quem chamar. Tom simples.
- [ ] `pendencias.md`: QV01–QV14 (pergunta, decisor, **situação real** — decidida no código / comportamento existente sem decisão registrada / sem rastro — e o que falta) e **pendências técnicas** (CT21, CT13 físico, QV07, alerta de pouco espaço R15, poda de `corrompido-*`, assinatura de código, instalador MSI, geração do zip de fotos no thread da interface, `Ativo` etc. do backlog das fatias anteriores, log de reinício consumindo arquivo de retenção, polimento do Histórico sem requisito).
- [ ] Ajustar `docs/docs/10-rastreabilidade.md` e o `CHANGELOG.md`.
- [ ] Commit `docs: documentos de homologacao da Etapa 8`.
