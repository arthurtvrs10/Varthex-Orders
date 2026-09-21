# Etapa 7 · Acabamento — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Trocar as janelas nativas restantes por uma janela de aviso de toque,
mostrar "Sim/Não" na lista de produtos e incluir as fotos no backup/restauração.

**Architecture:** `JanelaAviso` estática no Desktop; conversor XAML; no
`EfBackupService` um zip de fotos ao lado do `.db` (criação, retenção, cópia
externa, restauração); log de fotos órfãs na inicialização.

**Tech Stack:** C#/.NET 10, WPF, System.IO.Compression, xUnit.

**Spec:** [specs/2026-09-21-etapa7-acabamento-design.md](2026-09-21-etapa7-acabamento-design.md)

## Global Constraints

- `dotnet` com `export PATH="$PATH:/c/Program Files/dotnet" &&`. Solução:
  `backend/VarthexComanda.slnx`. Suíte atual: 447 testes verdes.
- **PROIBIDO abrir/rodar o app** e mexer em `%LOCALAPPDATA%\VarthexComanda`;
  testes usam pastas temporárias e `AppPaths` com raiz temporária.
- Commits em português, **sem** `Co-Authored-By`.
- ViewModels sem `System.Windows`; `JanelaAviso` fica no Desktop (não em ViewModels).
- Nunca apagar arquivos de foto automaticamente; restauração de fotos **não**
  apaga nada, só cria/sobrescreve por nome (`Path.GetFileName` obrigatório).
- Falha ao criar o zip de fotos **não** falha o backup do banco.
- Extensões de imagem do zip/restauração: `.jpg .jpeg .png .bmp`.
- Use `[Trait("Requisito", "...")]` nos testes novos (RF23/RF24 backup, RNF16
  mensagens quando aplicável).
- `System.Windows.Application` sempre qualificado (colisão de namespace).

---

## Task 1: `JanelaAviso` e fim dos `MessageBox` nativos; "Sim/Não" na lista de produtos

**Files:**
- Create: `backend/src/VarthexComanda.Desktop/JanelaAviso.xaml`, `JanelaAviso.xaml.cs`, `TipoAviso.cs` (enum `Informacao, Aviso, Erro`)
- Create: `backend/src/VarthexComanda.Desktop/Catalogo/BooleanoParaSimNaoConverter.cs`
- Modify: `App.xaml.cs` (5 chamadas), `Backup/BackupView.xaml.cs` (1 chamada), `Catalogo/ProdutosView.xaml` (coluna Ativo)
- Test: `backend/tests/VarthexComanda.Desktop.Tests/` (`JanelaAvisoTests`, `BooleanoParaSimNaoConverterTests`, extensão do teste de XAML existente)

**Interfaces:**
- `public static class JanelaAviso { public static void Mostrar(string titulo, string mensagem, TipoAviso tipo = TipoAviso.Informacao); }` (classe estática em `VarthexComanda.Desktop`; a janela em si é `JanelaAvisoView : Window`).

- [ ] Janela: largura ~560, `SizeToContent="Height"`, botão "OK" grande (altura 56, `IsDefault`+`IsCancel`, com o foco inicial), texto `TextWrapping` 16 pt, faixa lateral colorida por tipo (azul `#1F5FBF`, laranja `#D9822B`, vermelho `#B23B32`), `ResizeMode="NoResize"`, ícone do app. Mostrada com `ShowDialog()`.
- [ ] Trocar as seis chamadas de `MessageBox.Show` **mantendo os textos exatos** e os tipos (Information/Error/Warning). Em `App.xaml.cs` a chamada de segunda instância roda antes de existir janela principal → centro da tela.
- [ ] Coluna "Ativo" da lista de Produtos: `Converter={StaticResource SimNao}` (`"Sim"`/`"Não"`).
- [ ] Testes: conversor (true/false/null/tipo errado); `JanelaAvisoView` carrega em STA (reuse `ThreadingHelper.EmSta`) e o `OK` é o botão padrão; `grep` no `backend/src` não encontra mais `MessageBox`.
- [ ] Rodar a solução; commit `feat: janela de aviso de toque no lugar das MessageBox e Sim/Nao na lista de produtos`.

---

## Task 2: Fotos no backup e na restauração

**Files:**
- Modify: `backend/src/VarthexComanda.Infrastructure/Backup/EfBackupService.cs` (criação, retenção, backup externo, restauração), possivelmente `IBackupService`/`AppPaths` só se necessário
- Modify: `backend/src/VarthexComanda.Desktop/App.xaml.cs` (log de fotos órfãs; **sem** apagar)
- Test: `backend/tests/VarthexComanda.Infrastructure.Tests/Backup/EfBackupServiceFotosTests.cs` (novo)

- [ ] **Testes que falham** (pasta temporária, `AppPaths` temporário, mesmo estilo de `EfBackupServiceTests`): (a) `CriarBackupGerenciado` com 3 fotos em `fotos\` cria `varthex-comanda-….db.fotos.zip` com exatamente essas 3 entradas (nomes só o arquivo, sem caminho); (b) sem `fotos\` ou vazia → nenhum zip; (c) arquivo não-imagem em `fotos\` (ex.: `x.txt`) não entra; (d) retenção: com `retencaoMaxima=2` e 4 backups, os zips dos removidos também somem e os dos mantidos ficam; (e) falha ao zipar (ex.: arquivo de foto bloqueado exclusivamente) → resultado Sucesso, backup do `.db` existe, sem zip parcial, Warning no log (use o `ColetorDeLog` de `Infrastructure.Tests/Suporte`); (f) `RestaurarPara(backup com zip)`: fotos do zip aparecem em `fotos\`, foto que já existia com outro nome **permanece**, foto com mesmo nome é sobrescrita; (g) zip com entrada maliciosa `..\..\evil.png` → só `evil.png` dentro de `fotos\` (ou ignorada), nada fora da pasta; (h) `CriarBackupExterno` copia o zip junto quando existir; (i) `RestaurarPara` sem zip funciona como hoje (regressão); (j) o `.sha256` e `Validar` do `.db` inalterados.
- [ ] Implementar. Zip criado em arquivo temporário `.tmp` e movido para o nome final só quando completo; a criação roda **depois** do `.db` estar no lugar e do registro gravado; falhas capturadas e logadas com `_logger?.Warning(ex, "Falha em {Operacao}", "ZiparFotosDoBackup")`.
- [ ] `App.xaml.cs`: após o banco pronto, contar arquivos em `fotos\` não referenciados por `Produto.FotoArquivo` (consulta simples via repositório existente; se não houver método, adicione `IProdutoRepository.ListarNomesDeFotos()` **somente leitura** com teste) e `Information "Fotos sem produto: {Quantidade}"` — só o número, sem nomes.
- [ ] Rodar a solução inteira (verde). Commit `feat: fotos entram no backup e voltam na restauracao`.
