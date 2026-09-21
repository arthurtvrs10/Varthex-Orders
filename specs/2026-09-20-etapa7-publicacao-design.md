# Etapa 7 · Publicação autocontida e instalação limpa (design)

Cobre RNF09 (instalador inclui runtime), RNF17 (matriz de compatibilidade —
apenas o gabarito; preencher exige o computador-alvo), `docs/docs/12` (instalação,
atualização, "registrar versão instalada") e `docs/docs/19` (publicação).

## Estado atual (verificado)

- Nenhum RID/`SelfContained`, ícone, versão, empresa ou título no
  `VarthexComanda.Desktop.csproj`; o executável é `VarthexComanda.Desktop.exe`
  (docs/12 supõe `VarthexComanda.exe`).
- Nenhuma ferramenta de instalador instalada (Inno Setup, WiX, NSIS); nenhum
  script; `artifacts/` não é ignorado pelo Git. SDK .NET 10.0.401 presente.
- docs/12 admite que a **primeira implantação use a pasta autocontida**; um
  instalador só quando a distribuição se estabilizar.

## Decisões (rulings)

1. **Sem instalador de terceiros agora.** Pacote = pasta `win-x64` autocontida
   (multi-arquivo, sem single-file, conforme docs/19) compactada em `.zip`, com
   `Instalar.ps1`/`Instalar.cmd` e `Desinstalar.ps1` por usuário (sem
   administrador). Motivo: cumpre RNF09 ("instalação limpa") sem instalar
   software na máquina do usuário; um `.msi`/Inno vira melhoria futura
   registrada como pendência, e o roteiro fica pronto para ser embrulhado.
2. **Destino e dados separados:** binários em
   `%LOCALAPPDATA%\Programs\VarthexComanda`; dados continuam em
   `%LOCALAPPDATA%\VarthexComanda`. Atualizar **sobrescreve só** os binários;
   desinstalar **nunca** apaga dados (docs/19).
3. **Nome do executável:** `AssemblyName = VarthexComanda` → `VarthexComanda.exe`
   (alinha docs/12). Namespaces não mudam.
4. **Versão:** `1.0.0` (`Version`, `AssemblyVersion`, `FileVersion`,
   `InformationalVersion` = `1.0.0`), `Product = Varthex Comanda`,
   `Company = Varthex`. A versão aparece no rodapé da tela Configurações e no
   log de inicialização (docs/12: "registrar versão instalada", diagnóstico).
   O instalador grava `versao-instalada.txt` no destino.
5. **Ícone:** `.ico` multi-tamanho (16/24/32/48/64/128/256) gerado por script
   (letra "V" clara sobre quadrado escuro com detalhe laranja `#D9822B`, mesma
   paleta do app), commitado em `backend/src/VarthexComanda.Desktop/Assets/`.
   Usado em `ApplicationIcon`, `Window.Icon` e nos atalhos.
6. **`scripts/publicar.ps1`** roda `dotnet publish` (Release, `win-x64`,
   `--self-contained true`) e monta `artifacts\VarthexComanda-<versão>-win-x64.zip`
   com o conteúdo publicado + scripts de instalação + `LEIAME.txt` curto;
   `artifacts/` entra no `.gitignore`.
7. **Verificação por mim, sem tocar dados reais:** publicar; conferir que o
   `.exe` existe, que não há dependência do SDK (rodar o exe do pacote com
   `DOTNET_ROOT` vazio e `PATH` sem `dotnet`), que a biblioteca nativa do
   SQLite está no pacote; instalar num diretório temporário (parâmetro
   `-Destino`), abrir com `VARTHEX_COMANDA_DADOS` numa pasta descartável,
   confirmar janela por UI Automation, fechar só o PID iniciado, desinstalar e
   confirmar que os dados descartáveis permanecem. **CT21 num Windows limpo sem
   runtime continua exigindo outra máquina/VM: fica como pendência humana.**

## Fora de escopo

Assinatura de código (docs/08: só se a distribuição for pública); atualização
automática; instalador MSI/Inno/WiX; ARM64 (QV07 ainda aberto).
