# Etapa 7 · Publicação autocontida — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Gerar um pacote `.zip` autocontido `win-x64` (sem SDK/runtime na
máquina) com instalação/atualização/desinstalação por script que preservam os
dados do usuário; versão visível; ícone.

**Architecture:** propriedades de versão/ícone no csproj; um serviço de versão
lido do assembly; `scripts/publicar.ps1` + scripts de instalação embarcados no
zip; documentação atualizada.

**Tech Stack:** .NET 10 SDK (`dotnet publish`), PowerShell 5.1, WPF.

**Spec:** [specs/2026-09-20-etapa7-publicacao-design.md](2026-09-20-etapa7-publicacao-design.md)

## Global Constraints

- `dotnet` com `export PATH="$PATH:/c/Program Files/dotnet" &&`. PowerShell 5.1
  (sem `&&`/`||`, sem `?.`); scripts salvos em **UTF-8 com BOM**.
- **PROIBIDO** mexer em `%LOCALAPPDATA%\VarthexComanda` real ou abrir o app sem
  `VARTHEX_COMANDA_DADOS` apontando para pasta descartável. Nada de instalar em
  `%LOCALAPPDATA%\Programs\VarthexComanda` real durante testes: os scripts
  aceitam `-Destino` e os testes usam pasta temporária.
- Nunca matar processos que você não iniciou; guarde o PID.
- Valores exatos: `AssemblyName=VarthexComanda`, `Version=1.0.0`,
  `Product=Varthex Comanda`, `Company=Varthex`, RID `win-x64`, publicação
  **multi-arquivo** (sem `PublishSingleFile`), destino padrão de instalação
  `%LOCALAPPDATA%\Programs\VarthexComanda`, atalhos "Varthex Comanda" (Área de
  Trabalho e Menu Iniciar > Programas).
- Commits em português, **sem** `Co-Authored-By`.

---

## Task 1: Identidade do executável (nome, versão, ícone) e versão na tela

**Files:**
- Modify: `backend/src/VarthexComanda.Desktop/VarthexComanda.Desktop.csproj`
- Create: `backend/src/VarthexComanda.Desktop/Assets/VarthexComanda.ico` (gerado por script descartável **fora do repo**, com `System.Drawing`; multi-tamanho 16–256; letra "V" clara sobre quadrado escuro `#23252A` com faixa inferior laranja `#D9822B`)
- Modify: `MainWindow.xaml` (`Icon`), `Configuracao/ConfiguracaoView.xaml` (rodapé com a versão), `Configuracao/ConfiguracaoViewModel.cs`
- Create: `backend/src/VarthexComanda.Desktop/VersaoDoAplicativo.cs` (`static string Atual` = `InformationalVersion` do assembly sem o sufixo `+hash`)
- Modify: `App.xaml.cs` (`"Iniciando Varthex Comanda {Versao}"`)
- Modify: `.gitignore` (raiz): adicionar `artifacts/`
- Test: `backend/tests/VarthexComanda.Desktop.Tests/VersaoDoAplicativoTests.cs`

- [ ] Testes: `Atual` é `1.0.0`; `ConfiguracaoViewModel.VersaoTexto` (`"Versão 1.0.0"`).
- [ ] csproj: `AssemblyName`, `Version`/`FileVersion`/`AssemblyVersion`/`InformationalVersion` (todas `1.0.0`; `IncludeSourceRevisionInInformationalVersion=false`), `Product`, `Company`, `Copyright`, `ApplicationIcon`, `Resource`/`Content` do `.ico` para `Window.Icon`.
- [ ] Renomear impactos do `AssemblyName` (procure `VarthexComanda.Desktop.exe` / `Environment.ProcessPath` / docs de execução; `dotnet run --project ...` segue funcionando).
- [ ] Rodar solução inteira (verde) e **compilar sem avisos**; conferir com `Get-Item ...\VarthexComanda.exe | Select VersionInfo` (via PowerShell, sem executar o app).
- [ ] Commit `feat: nome, versao 1.0.0 e icone do aplicativo`.

---

## Task 2: Script de publicação e pacote

**Files:**
- Create: `scripts/publicar.ps1`, `scripts/pacote/Instalar.cmd`, `scripts/pacote/Instalar.ps1`, `scripts/pacote/Desinstalar.ps1`, `scripts/pacote/LEIAME.txt`

**Interfaces:**
- `publicar.ps1 [-Saida <pasta>]` → `artifacts\publish\win-x64\` e `artifacts\VarthexComanda-1.0.0-win-x64.zip` (versão lida do csproj via `Select-Xml`/`[xml]`).
- `Instalar.ps1 [-Destino <pasta>] [-SemAtalhos]`: (1) recusa se `VarthexComanda.exe` do destino estiver em execução (mensagem "Feche o Varthex Comanda e tente de novo."), (2) copia os arquivos do pacote (a pasta do script) para `-Destino` (padrão `$env:LOCALAPPDATA\Programs\VarthexComanda`), **sem** mexer em `$env:LOCALAPPDATA\VarthexComanda`, (3) cria atalhos na Área de Trabalho e no Menu Iniciar (a menos de `-SemAtalhos`), (4) grava `versao-instalada.txt` (versão + data ISO), (5) imprime versão instalada e o caminho **dos dados** deixando claro que são preservados. Se já existir instalação: informa "Atualizando de X para Y" e mantém uma cópia do diretório anterior em `<Destino>.anterior` (uma só, sobrescrita) para retorno (docs/12).
- `Desinstalar.ps1 [-Destino <pasta>]`: remove binários e atalhos; **nunca** remove a pasta de dados; recusa se o exe estiver em execução.
- `Instalar.cmd`: `powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Instalar.ps1" %*`.

- [ ] Escrever os scripts (PS 5.1, UTF-8 com BOM, `Set-StrictMode`, `$ErrorActionPreference='Stop'`), com mensagens em português.
- [ ] Rodar `scripts\publicar.ps1`; conferir: zip existe; contém `VarthexComanda.exe`, `e_sqlite3.dll` (ou o equivalente nativo do provedor), `hostfxr.dll`/`coreclr.dll` (prova de runtime embutido), `Instalar.cmd`, `Desinstalar.ps1`, `LEIAME.txt`; tamanho reportado.
- [ ] Testar Instalar/Desinstalar **apenas com `-Destino` temporário e `-SemAtalhos`** (atalhos testar só numa execução com `-Destino` temporário e removê-los em seguida por `Desinstalar.ps1`, conferindo a Área de Trabalho depois); provar que uma pasta de dados descartável criada ao lado permanece intacta após desinstalar. **Não** executar o `.exe`.
- [ ] Commit `feat: publicacao autocontida win-x64 com instalar/desinstalar por script (RNF09)`.

---

## Task 3: Documentação de implantação

**Files:**
- Modify: `README.md` (seção de instalação; remover "sem instalador precisa do SDK"; comando de publicação), `docs/docs/12-operacao-implantacao.md` (procedimento com o pacote, retorno via `.anterior`, nome do exe, versão), `docs/docs/19-plataforma-windows-dotnet.md` (nome do exe, scripts, decisão de não usar instalador de terceiros agora), `docs/CHANGELOG.md` (nova entrada `1.13 – Publicação autocontida` e entradas atrasadas: `1.13.x` para Produtos/toque/diálogo de confirmação/tabela do carrinho/`VARTHEX_COMANDA_DADOS` — reunir em uma entrada honesta "Ajustes de interface e de dados isolados", datada do dia)

- [ ] Sem inventar comportamento: o texto deve refletir exatamente o que os scripts fazem.
- [ ] Commit `docs: implantacao autocontida e changelog`.

## Notas do controlador (2026-09-21, após as fatias 1–3)

- Ao renomear `AssemblyName` para `VarthexComanda`, procure e ajuste **tudo** que dependa do nome do assembly ou do exe: URIs `pack://application:,,,/VarthexComanda.Desktop;component/...` (inclusive nos testes STA em `Desktop.Tests/Xaml`), `Environment.ProcessPath`, `InternalsVisibleTo`, `README.md`, `docs/`. Os testes de Desktop referenciam o projeto: confirme que compilam e passam.
- O app já tem `JanelaAviso`, modo de restauração, fotos no backup (`.db.fotos.zip`): o pacote de instalação não muda isso, mas `LEIAME.txt` deve mencionar que os dados ficam em `%LOCALAPPDATA%\VarthexComanda` e que atualizar/desinstalar não os apaga.
- Ao gerar o `.ico` (fora do repo), use `System.Drawing` em PowerShell 5.1 (`Add-Type -AssemblyName System.Drawing`) e escreva o ICO com PNGs embutidos (formato válido para Vista+); valide relendo o arquivo (cabeçalho ICONDIR, contagem de imagens = 7).
- Não instalar nada em `%LOCALAPPDATA%\Programs\VarthexComanda` real durante os testes; use `-Destino` em pasta temporária.
