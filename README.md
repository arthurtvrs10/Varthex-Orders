<div align="center">

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


# Varthex Comanda

![Plataforma](https://img.shields.io/badge/plataforma-Windows-blue)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![WPF](https://img.shields.io/badge/UI-WPF-5C2D91)
![SQLite](https://img.shields.io/badge/banco-SQLite-003B57)
![Testes](https://img.shields.io/badge/testes-240%20passando-brightgreen)
![Licen%C3%A7a](https://img.shields.io/badge/licen%C3%A7a-n%C3%A3o%20definida-lightgrey)

**Um aplicativo desktop para Windows que controla o consumo por comandas
numeradas: você lança os itens, ele calcula o total e mostra o valor para você
digitar na maquininha — sem nunca tocar em pagamento.**

<img src="docs/design/atendimento-grade.png" alt="Grade de comandas" width="900">

</div>

Pensado para o balcão de um pequeno negócio: uma grade com todas as comandas
(livres ou abertas), um menu de produtos com foto e um carrinho em tabela, tudo
com botões grandes o bastante para o toque. Funciona 100% offline, com os dados
num banco SQLite local.

## Telas

![Dentro da comanda](docs/design/atendimento-comanda.png)

Dentro da comanda: menu com fotos à esquerda, carrinho à direita.

![Produtos](docs/design/produtos.png)

Produtos: cadastro com foto, modo de edição.

![Histórico](docs/design/historico.png)

Histórico: vendas do dia, resumo e detalhe dos itens.

## Instalação

O aplicativo é entregue como um pacote `.zip` **autocontido** (`win-x64`): já
inclui o runtime do .NET, então quem usa **não precisa instalar o .NET nem o
SDK**, e a instalação não exige conta de administrador. O pacote não é um
instalador `.msi`/`.exe`: são scripts (`Instalar.cmd`, `Instalar.ps1`,
`Desinstalar.cmd`, `Desinstalar.ps1`) que acompanham os arquivos do programa.

**Gerar o pacote** (na máquina de desenvolvimento, que precisa do SDK do .NET 10):

```powershell
.\scripts\publicar.ps1
```

Isso lê a versão do `<Version>` do `VarthexComanda.Desktop.csproj`, publica em
`artifacts\publish\win-x64` e gera `artifacts\VarthexComanda-<versão>-win-x64.zip`
(hoje `VarthexComanda-1.0.0-win-x64.zip`, cerca de 64 MB). O parâmetro opcional
`-Saida <pasta>` troca a pasta `artifacts`. A pasta `artifacts\publish\win-x64`
é apagada a cada publicação; já os `.zip` de versões anteriores **acumulam** em
`artifacts` (apague os antigos à mão). Os arquivos `.pdb` não entram no pacote.

**Instalar** (no computador do balcão):

1. Extraia o `.zip` **inteiro** numa pasta (não execute de dentro do zip).
2. Dê dois cliques em `Instalar.cmd`.
3. O programa vai para `%LOCALAPPDATA%\Programs\VarthexComanda` e são criados
   os atalhos "Varthex Comanda" na Área de Trabalho e no Menu Iniciar. Para outra
   pasta ou sem atalhos, use no PowerShell:
   `.\Instalar.cmd -Destino "C:\Pasta\Outra" -SemAtalhos` (também aceita
   `-WhatIf`, para simular sem alterar nada).

**Atualizar:** feche o app, extraia o pacote novo e execute `Instalar.cmd`
de novo. Só os arquivos do programa são trocados; a versão anterior fica em
`%LOCALAPPDATA%\Programs\VarthexComanda.anterior` para o retorno manual.

**Desinstalar:** dê dois cliques em `Desinstalar.cmd` (na pasta do programa ou do
pacote; para outro destino, `.\Desinstalar.cmd -Destino "C:\Pasta\Outra"`). Remove
a pasta do programa **inteira** (não guarde arquivos seus ali), a cópia `.anterior`
e os atalhos.

**Os dados ficam separados do programa** (veja [Onde ficam os
dados](#onde-ficam-os-dados)): atualizar e desinstalar **nunca** apagam
`%LOCALAPPDATA%\VarthexComanda`. A variável de ambiente `VARTHEX_COMANDA_DADOS`
redireciona essa pasta (demonstrações e testes) e os scripts de instalação a
respeitam ao recusar destinos perigosos.

O passo a passo completo, o retorno à versão anterior e o diagnóstico estão em
[Operação e implantação](docs/docs/12-operacao-implantacao.md); as decisões de
publicação, em [Plataforma Windows e .NET](docs/docs/19-plataforma-windows-dotnet.md).

> **Para desenvolvedores:** rodar a partir do código exige o SDK do .NET 10 —
> `dotnet run --project backend/src/VarthexComanda.Desktop` (veja
> [Compilando](#compilando)).

## O que ele faz

| Tela | O que resolve |
|---|---|
| **Atendimento** | Grade fixa de comandas numeradas (20 por padrão, configurável). Cada card mostra se está **livre** ou **aberta**, há quanto tempo e o total parcial. Clicar abre a comanda; dentro dela, o **menu** (cards de produto com foto, filtrados por categoria) fica à esquerda e o **carrinho** em tabela à direita, com `−` / `+` / `Excluir` por linha. |
| **Encerramento** | **Finalizar comanda (F4)** mostra o total para digitar na maquininha e só encerra depois que você confirma que a cobrança foi aprovada fora do sistema. Se der erro na maquininha, a comanda continua aberta. |
| **Produtos** | Cadastro em dois modos (**novo** / **edição**) com foto, nome, categoria e preço. Desativar em vez de apagar, para não quebrar o histórico. |
| **Histórico** | Vendas por dia, busca por número, detalhe dos itens e resumo do dia (quantidade, total e ticket médio). |
| **Backup** | Cópia automática na primeira abertura do dia e ao fechar o app, cópia manual (inclusive numa pasta externa), validação e restauração. |
| **Configurações** | Nome do estabelecimento, quantidade de comandas e pasta de backup externa. |

### Foto do produto

Cada produto pode ter uma foto (JPG, PNG ou BMP, até 10 MB). O app **copia** a
imagem para a própria pasta de dados — o arquivo original nunca é alterado — e o
banco guarda só o nome do arquivo. Arquivos que não são imagem de verdade são
recusados antes de qualquer cópia, e trocar ou remover a foto apaga o arquivo
antigo.

### Backup e recuperação

- Usa a API de snapshot nativa do SQLite (nunca uma cópia crua do arquivo), com
  checagem de integridade e checksum SHA-256.
- Mantém as **30 cópias mais recentes** na pasta gerenciada.
- **Restaurar** valida formato, versão do esquema, integridade e checksum, faz
  uma cópia preventiva do banco atual antes de tocar em qualquer coisa e reinicia
  o aplicativo ao terminar.

## Onde ficam os dados

Tudo em `%LOCALAPPDATA%\VarthexComanda\`:

| Pasta | Conteúdo |
|---|---|
| `data\` | o banco `varthex-comanda.db` |
| `backups\` | cópias gerenciadas (`.db` + `.sha256`) |
| `fotos\` | fotos dos produtos |
| `logs\` | logs técnicos (Serilog), sem dados sensíveis |

Valores são guardados em **centavos** e datas em **UTC**; a tela converte para o
horário de Brasília (UTC−3, fixo) só na exibição. Só uma instância do app roda
por vez. A pasta de dados pode ser redirecionada com a variável de ambiente
`VARTHEX_COMANDA_DADOS` (útil para demonstrações e testes sem tocar nos dados
reais).

## O que ele não faz

Não processa pagamentos, não integra com a maquininha e não registra forma de
pagamento, valor recebido, troco, cartão ou autorização da adquirente. É de
propósito: o operador digita o total na maquininha, e o sistema só guarda que a
comanda foi encerrada.

## Compilando

Requer Windows e o [SDK do .NET 10](https://dotnet.microsoft.com/download).

```sh
dotnet build backend/VarthexComanda.slnx
dotnet test  backend/VarthexComanda.slnx --configuration Release
dotnet run --project backend/src/VarthexComanda.Desktop
```

O banco é criado e migrado sozinho na primeira execução; se houver migrações
pendentes, o app faz uma cópia preventiva antes de aplicá-las.

## Arquitetura

Camadas com dependência sempre para dentro, e cada capacidade de negócio em sua
própria pasta/namespace em todas as camadas (`Atendimento`, `Catalogo`,
`Backup`, `Configuracao`):

```
Desktop (WPF, MVVM)  →  Application (casos de uso, Resultado<T>)  →  Domain (entidades)
        └────────────→  Infrastructure (EF Core/SQLite, arquivos, backup)  ──┘
```

| Camada | Papel |
|---|---|
| `VarthexComanda.Domain` | Entidades puras, sem dependências |
| `VarthexComanda.Application` | Casos de uso e interfaces de repositório; erros esperados viram `Resultado<T>` |
| `VarthexComanda.Infrastructure` | Repositórios EF Core, armazenamento de fotos, motor de backup, logs |
| `VarthexComanda.Desktop` | Telas WPF e ViewModels (sem `System.Windows` nos ViewModels) |

Cada operação que muda dados abre um único contexto e salva uma única vez — a
atomicidade vem da construção, sem camada de unit-of-work. Os testes (xUnit)
ficam em `backend/tests/`, um projeto por camada.

## Documentação

A documentação em [`docs/`](docs/README.md) é a fonte de verdade (requisitos
`RF`, regras `RN`, casos de uso `UC` e testes de aceitação `CT`):

- [Visão e decisões](docs/docs/00-visao-e-decisoes.md) e
  [Escopo do MVP](docs/docs/01-escopo-mvp.md)
- [Guia de implementação](docs/docs/14-guia-implementacao.md)
- [Histórico de alterações](docs/CHANGELOG.md)
- [`specs/`](specs/): o design e o plano de cada fatia entregue
- [AGENTS.md](docs/AGENTS.md): leia antes de qualquer alteração assistida por IA

## Status

Etapas de catálogo, comandas, encerramento, histórico, backup/recuperação e
configurações estão entregues e cobertas por testes automatizados.

### Próximos passos

- Validar o pacote autocontido num Windows limpo, sem nenhum .NET instalado
  (CT21), e testar antivírus, a versão/arquitetura do Windows-alvo (RNF17) e a
  instalação com atalhos no computador do balcão.
- Instalador `.msi`/Inno Setup/WiX: **não adotado** por ora (o pacote é um
  `.zip` com scripts); avaliar quando a distribuição se estabilizar.
- Navegação completa por teclado com foco visível (RNF18); hoje só o **F4** existe.
- Rotação e limite de tamanho dos logs (RNF21).
- Homologação no computador real (CT01–CT22, teste sem internet, treinamento).

### Limitações conhecidas

- O pacote autocontido ainda **não foi validado num Windows limpo** sem .NET
  (CT21 pendente): só foi verificado numa máquina de desenvolvimento, iniciando
  a cópia instalada sem `dotnet` no `PATH`.
- Sem assinatura de código (o Windows pode exibir aviso de aplicativo
  desconhecido), sem atualização automática e com retorno à versão anterior
  manual.
- A versão e a arquitetura exatas do Windows-alvo ainda estão pendentes
  (QV07 em [pendências](docs/docs/13-pendencias-validacao.md)).

## Licença

Nenhuma licença definida. Repositório privado até decisão do proprietário.
