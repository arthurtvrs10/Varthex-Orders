# Plataforma Windows e .NET

Este documento define como iniciar, organizar, persistir, testar e publicar o Varthex Comanda. Ele complementa os requisitos sem substituir as regras de negócio.

## Linha de base

| Elemento | Decisão |
| --- | --- |
| Sistema operacional | Windows; versão e arquitetura exatas pendentes em QV07 |
| Linguagem | C# |
| Runtime | .NET 10 LTS |
| Interface | WPF e XAML |
| Padrão de apresentação | MVVM com CommunityToolkit.Mvvm |
| Persistência | Entity Framework Core com SQLite |
| Testes | xUnit |
| Logs | Serilog com rotação |
| Publicação | Autocontida; `win-x64` como padrão inicial |

## Estrutura da solução

```text
VarthexComanda.sln
src/
  VarthexComanda.Domain/
  VarthexComanda.Application/
  VarthexComanda.Infrastructure/
  VarthexComanda.Desktop/
tests/
  VarthexComanda.Domain.Tests/
  VarthexComanda.Application.Tests/
  VarthexComanda.Infrastructure.Tests/
```

Dependências permitidas:

- `Domain` não referencia outro projeto;
- `Application` referencia `Domain`;
- `Infrastructure` referencia `Application` e `Domain`;
- `Desktop` referencia `Application` e `Infrastructure` e funciona como raiz de composição;
- testes referenciam apenas os projetos que verificam.

## Criação inicial

Execute no PowerShell a partir da raiz do futuro repositório de código:

```powershell
dotnet new sln -n VarthexComanda
dotnet new classlib -n VarthexComanda.Domain -o src/VarthexComanda.Domain -f net10.0
dotnet new classlib -n VarthexComanda.Application -o src/VarthexComanda.Application -f net10.0
dotnet new classlib -n VarthexComanda.Infrastructure -o src/VarthexComanda.Infrastructure -f net10.0
dotnet new wpf -n VarthexComanda.Desktop -o src/VarthexComanda.Desktop -f net10.0
dotnet new xunit -n VarthexComanda.Domain.Tests -o tests/VarthexComanda.Domain.Tests -f net10.0
dotnet new xunit -n VarthexComanda.Application.Tests -o tests/VarthexComanda.Application.Tests -f net10.0
dotnet new xunit -n VarthexComanda.Infrastructure.Tests -o tests/VarthexComanda.Infrastructure.Tests -f net10.0
dotnet sln add (Get-ChildItem -Recurse -Filter *.csproj)
```

Depois, adicione as referências conforme a direção definida e os pacotes abaixo:

```powershell
dotnet add src/VarthexComanda.Infrastructure package Microsoft.EntityFrameworkCore.Sqlite
dotnet add src/VarthexComanda.Infrastructure package Microsoft.EntityFrameworkCore.Design
dotnet add src/VarthexComanda.Desktop package CommunityToolkit.Mvvm
dotnet add src/VarthexComanda.Desktop package Microsoft.Extensions.DependencyInjection
dotnet add src/VarthexComanda.Desktop package Serilog
dotnet add src/VarthexComanda.Desktop package Serilog.Sinks.File
```

As versões devem ser compatíveis com .NET 10, registradas centralmente e travadas no repositório. Não usar versão prévia em produção.

## Persistência

- usar `long` para centavos e conversão explícita na interface;
- usar um `DbContext` de curta duração por caso de uso ou unidade de trabalho;
- ativar `foreign_keys`, definir tempo limite de banco e usar transações explícitas nos fluxos documentados;
- criar a primeira migração a partir do modelo equivalente a `database/schema.sql`;
- nunca editar migração já aplicada em produção;
- criar backup válido antes de migrar uma base existente;
- validar `PRAGMA integrity_check` na rotina de diagnóstico e restauração.

Comandos básicos:

```powershell
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project src/VarthexComanda.Infrastructure --startup-project src/VarthexComanda.Desktop
dotnet ef database update --project src/VarthexComanda.Infrastructure --startup-project src/VarthexComanda.Desktop
```

## Arquivos locais

```text
%LOCALAPPDATA%\VarthexComanda\
  data\varthex-comanda.db
  logs\varthex-comanda-AAAA-MM-DD.log
  backups\varthex-comanda-AAAA-MM-DD-HHMMSS.db
```

O caminho deve ser obtido por `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)`. O executável e os arquivos operacionais ficam separados.

## Inicialização segura

1. adquirir mutex nomeado da aplicação;
2. resolver e criar as pastas locais;
3. configurar log com rotação;
4. criar cópia preventiva quando houver migração pendente;
5. abrir o SQLite e aplicar migrações;
6. validar consultas essenciais;
7. carregar comandas abertas;
8. exibir a tela de atendimento.

Se o mutex já estiver ocupado, informar que o Varthex Comanda está aberto e encerrar a segunda execução. Se a base estiver inválida, não permitir gravações nem restaurar cópia automaticamente.

## Testes e qualidade

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
```

Os testes de infraestrutura devem criar bancos temporários. Nunca executar testes automatizados destrutivos contra a base da lanchonete. Cada teste deve indicar os códigos RF, RN, RNF e CT que cobre quando aplicável.

## Publicação para Windows

Enquanto a arquitetura final estiver pendente (QV07), usa-se `win-x64`. O comando oficial é o script do repositório:

```powershell
.\scripts\publicar.ps1            # opcional: -Saida <pasta>
```

O script lê a versão do `<Version>` de `VarthexComanda.Desktop.csproj` (fonte única; `AssemblyVersion`, `FileVersion` e `InformationalVersion` ficam no mesmo arquivo), executa `dotnet publish` em `Release`, `win-x64`, `--self-contained true`, sem `PublishSingleFile` e sem trimming, e monta o pacote. Saídas (a pasta `artifacts/` é ignorada pelo Git):

- `artifacts\publish\win-x64\` — pasta publicada, com `VarthexComanda.exe` (`AssemblyName = VarthexComanda`);
- `artifacts\VarthexComanda-<versão>-win-x64.zip` — a pasta publicada mais `Instalar.cmd`, `Instalar.ps1`, `Desinstalar.ps1` e `LEIAME.txt` (de `scripts\pacote\`) na raiz do zip. Para a versão 1.0.0: cerca de 64 MB e cerca de 430 arquivos.

Manter múltiplos arquivos na primeira versão simplifica o diagnóstico das bibliotecas nativas do SQLite. O pacote deve ser testado em um Windows limpo sem SDK nem runtime do .NET (CT21) — **ainda pendente**; hoje só se verificou, numa máquina de desenvolvimento, que uma cópia instalada abre sem `dotnet` no `PATH`.

### Decisão: sem instalador de terceiros por enquanto

O pacote é um `.zip` com scripts de instalação por usuário (sem administrador), em vez de `.msi`, Inno Setup, WiX ou NSIS. Motivos: cumpre RNF09 (instalação limpa, com o runtime incluso) sem instalar nenhuma ferramenta nem software adicional na máquina do usuário; nenhuma dessas ferramentas está instalada no ambiente de desenvolvimento; e a distribuição ainda não se estabilizou. Os scripts separam binários (`%LOCALAPPDATA%\Programs\VarthexComanda`) e dados (`%LOCALAPPDATA%\VarthexComanda`): atualizar sobrescreve só os binários e desinstalar nunca apaga dados, como exigido nos critérios abaixo.

Próximas opções, todas futuras: MSIX, Inno Setup ou WiX embrulhando a mesma pasta publicada (o roteiro de instalação já está separado dos binários). Assinatura de código só se a distribuição for pública (veja o documento 08); sem ela o Windows pode mostrar aviso de aplicativo desconhecido. Também ficam fora desta etapa: atualização automática, retorno automático (hoje manual, via `.anterior`) e ARM64.

O procedimento de instalação, atualização, retorno e diagnóstico está no documento 12.

## Critério técnico de pronto

- build e testes passam em configuração Release;
- migrações funcionam em banco vazio e em cópia da versão anterior;
- aplicativo funciona sem internet;
- segunda instância é recusada;
- fechamento forçado preserva comandas já confirmadas;
- pacote autocontido inicia no Windows homologado;
- dados sobrevivem à atualização e desinstalação do executável;
- nenhum modelo, tabela, tela ou log registra pagamento no MVP.
