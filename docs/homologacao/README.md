# Homologação (Etapa 8)

Esta pasta reúne tudo o que a Etapa 8 do [guia de implementação](../docs/14-guia-implementacao.md) produziu **sem depender de terceiros**: a evidência que uma máquina de desenvolvimento consegue gerar, e os roteiros para o que só uma pessoa, no computador da loja, pode fechar.

Versão do aplicativo homologada: **1.0.0** (pacote `VarthexComanda-1.0.0-win-x64.zip`, cerca de 64 MB, autocontido). Data das evidências: 2026-09-21.

> **Estado em uma frase:** a parte automatizável está provada (559 testes verdes e seis ensaios com o aplicativo real, todos aprovados); **a homologação humana ainda não aconteceu**. Falta o computador da loja (QV07), o teste sem rede física (CT13), a instalação num Windows limpo (CT21), a maquininha de verdade, o treinamento dos operadores e as respostas do responsável (QV01–QV14). Nada aqui autoriza dizer que o sistema está "homologado".

## Índice

| Documento | Para que serve |
| --- | --- |
| [checklist-etapa8.md](checklist-etapa8.md) | Os 8 itens da Etapa 8, cada um com a situação real e o link da evidência |
| [resultados-ct.md](resultados-ct.md) | CT01–CT22: cenário, tipo de evidência, situação, evidência e observações |
| [rastreabilidade-testes.md](rastreabilidade-testes.md) | **Gerado.** Cada CT ligado aos testes automatizados que o cobrem |
| [evidencias/ensaio-2026-09-21.txt](evidencias/ensaio-2026-09-21.txt) | Transcrição dos ensaios E1–E6 com o aplicativo real |
| [matriz-compatibilidade.md](matriz-compatibilidade.md) | RNF17: Windows, arquitetura, memória, resolução e escala (máquina de desenvolvimento como referência; computador da loja em branco) |
| [roteiro-ensaio-manual.md](roteiro-ensaio-manual.md) | Passo a passo, para uma pessoa, do que roda no computador da loja |
| [guia-do-operador.md](guia-do-operador.md) | Uma página, em linguagem simples, para o treinamento |
| [pendencias.md](pendencias.md) | QV01–QV14 com a situação real e as pendências técnicas |

Regra deste diretório: **nada é marcado como aprovado sem evidência executada que possa ser apontada** (um teste automatizado com nome ou um ensaio E#). Onde só existe prova por construção do código, ou só uma parte do cenário foi executada, o documento diz isso.

## Como rodar cada coisa

Todos os comandos partem da raiz do repositório e exigem o SDK do .NET (somente na máquina de desenvolvimento; o computador da loja não precisa dele).

### 1. Testes automatizados

```powershell
dotnet test backend\VarthexComanda.slnx
```

Resultado da última execução: **559 testes, 0 falhas** (Domain 4, Application 85, Infrastructure 254, Desktop 216).

### 2. Rastreabilidade dos casos (CT → testes)

Cada teste ligado a um caso tem `[Trait("Caso", "CTnn")]`. O teste `RastreabilidadeDosCasosTests` (em `backend\tests\VarthexComanda.Desktop.Tests\Homologacao`) **falha** se um caso automatizável ficar sem teste ou se um trait tiver valor fora de CT01–CT22.

```powershell
dotnet test backend\tests\VarthexComanda.Desktop.Tests --filter RastreabilidadeDosCasosTests
```

Para regenerar a matriz [rastreabilidade-testes.md](rastreabilidade-testes.md), aponte a variável `VARTHEX_GERAR_RASTREABILIDADE` para o arquivo de saída e rode o mesmo teste (sem a variável, nada é escrito):

```powershell
$env:VARTHEX_GERAR_RASTREABILIDADE = "$PWD\docs\homologacao\rastreabilidade-testes.md"
dotnet test backend\tests\VarthexComanda.Desktop.Tests --filter RastreabilidadeDosCasosTests
Remove-Item Env:\VARTHEX_GERAR_RASTREABILIDADE
```

### 3. Massa mínima (dados descartáveis)

Gera 5 categorias (1 inativa), 30 produtos (1 inativo), 20 números de comanda, comandas abertas de perfis diferentes, comandas abandonadas, 90 dias de histórico e três arquivos em `backups\` (válido, antigo e corrompido). Usa as mesmas migrações e casos de uso do aplicativo e é reproduzível.

```powershell
dotnet run --project backend\tools\VarthexComanda.MassaMinima -- --saida C:\HomologacaoVarthex\dados
```

- A ferramenta **recusa** a pasta real de dados (`%LOCALAPPDATA%\VarthexComanda`), a pasta do perfil, raízes de disco e qualquer pasta que contenha a real.
- Recusa pasta não vazia sem `--forcar`; `--forcar` só vale em pasta que a própria ferramenta criou (marcador `.varthex-massa-minima` na raiz) e só apaga os quatro subdiretórios `data`, `logs`, `backups` e `fotos`. Recusa também Documentos, Área de Trabalho, Imagens, Músicas, Vídeos, Downloads, o perfil do usuário e os pais/raiz dessas pastas, e caminhos que passem por junction/atalho para a pasta real dos dados.
- `--agora <data UTC ISO>` fixa a data de referência (o padrão é agora).

Para usar a massa no aplicativo, defina `VARTHEX_COMANDA_DADOS` com a pasta gerada antes de abri-lo (veja o [roteiro](roteiro-ensaio-manual.md)).

### 4. Ensaios com o aplicativo real (E1–E6)

```powershell
powershell -ExecutionPolicy Bypass -File scripts\homologacao\Executar-Ensaio.ps1 -Saida C:\Temp\ensaio
```

Instala o pacote de `artifacts\` numa pasta temporária, gera uma massa mínima por ensaio, abre o aplicativo com `VARTHEX_COMANDA_DADOS` apontando para ela e controla a interface só por UI Automation (sem enviar teclas, sem roubar o foco). Aborta se já houver um `VarthexComanda` em execução, encerra apenas o processo que ele mesmo iniciou e nunca lê nem escreve em `%LOCALAPPDATA%\VarthexComanda`. Parâmetros: `-Ensaios E1,E3` (subconjunto), `-VezesAbertura 5`, `-Transcript <arquivo>`, `-ManterAmbiente`. O código de saída é diferente de zero se algum ensaio falhar. Feche o Varthex Comanda antes de rodar.

| Ensaio | O que verifica |
| --- | --- |
| E1 | RNF03: tempo até a janela principal, média de 5 aberturas |
| E2 | CT19: segunda instância mostra o aviso e não abre outra janela |
| E3 | CT20: término forçado do processo; comanda e total sobrevivem |
| E4 | CT09: "Voltar para a comanda" na tela de Encerramento não gera venda |
| E5 | Restauração de backup válido pela tela; backup corrompido recusado (pelo modo de restauração) |
| E6 | CT13, **apenas apoio**: o processo não abre conexões de rede |

### 5. Gerar o pacote

```powershell
powershell -ExecutionPolicy Bypass -File scripts\publicar.ps1
```

Publica em `win-x64`, autocontido, e cria `artifacts\VarthexComanda-1.0.0-win-x64.zip` com `Instalar.cmd`, `Desinstalar.cmd` e `LEIAME.txt`. Detalhes de instalação, atualização e retorno em [Operação e implantação](../docs/12-operacao-implantacao.md).

## Onde cada resultado é registrado

- Situação de cada CT: [resultados-ct.md](resultados-ct.md).
- Situação de cada item da Etapa 8: [checklist-etapa8.md](checklist-etapa8.md).
- Quando a pessoa executar o [roteiro](roteiro-ensaio-manual.md), o "Resultado obtido / Situação / Evidência" de cada passo deve ser copiado para `resultados-ct.md` e para o checklist. Só então um item pode passar para "Feito".
