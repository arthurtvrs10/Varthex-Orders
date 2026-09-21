# Etapa 8 — Homologação (design)

Cobre o checklist de `docs/docs/14-*.md` (Etapa 8): (1) CT01–CT22, (2) massa
mínima, (3) sem internet, (4) no computador real, (5) erro da maquininha,
(6) restaurar backup, (7) treinar operadores, (8) registrar pendências.
Este trabalho entrega **tudo que uma máquina de desenvolvimento consegue provar
sem terceiros**, e deixa para quem opera o que só existe fora daqui.

## Separação honesta: o que eu faço × o que exige uma pessoa

| Item | O que esta fatia entrega | Fica com uma pessoa |
| --- | --- | --- |
| 1. CT01–CT22 | Cada CT ligado a testes automatizados (`[Trait("Caso","CTnn")]`), lacunas cobertas (CT11, CT16, CT13 por construção), matriz `CT → testes` gerada e conferida por teste, ensaios executados com o app real (CT17, CT19, CT20, RNF03) | CT13 sem rede física, CT21 em Windows limpo |
| 2. Massa mínima | Gerador reproduzível (`MassaMinima`) + teste que confere as contagens | Executar no computador-alvo |
| 3. Sem internet | Prova por construção (nenhuma referência de rede no código) + ensaio que confere que o processo não abre conexões | Desligar a rede de verdade no PC da loja |
| 4. Computador real | Roteiro passo a passo e matriz de compatibilidade (RNF17) com os dados desta máquina preenchidos como *referência de desenvolvimento* | Preencher e executar na máquina-alvo (QV07) |
| 5. Erro da maquininha | Ensaio com o app real: abrir Encerramento, "Voltar", comanda continua aberta e sem venda (CT09) | Repetir com a maquininha de verdade |
| 6. Restaurar backup | Testes de restauração (incl. banco corrompido e fotos) + ensaio de restauração com dados isolados | Ensaio trimestral no PC real |
| 7. Treinar operadores | Guia de uma página em português | O treinamento em si |
| 8. Pendências | Lista consolidada QV01–QV14 + pendências técnicas com dono | Respostas do responsável |

## Decisões (rulings)

1. **Rastreabilidade executável.** Um atributo/trait `Caso` (`CT01`…`CT22`) por
   teste. Um teste (`RastreabilidadeDosCasosTests`) usa reflexão nos assemblies
   de teste, exige ≥ 1 teste automatizado para cada CT automatizável e
   **falha** se um CT automatizável perder a cobertura. CT13 usa uma
   verificação estrutural (o código não referencia `System.Net.Http`,
   `System.Net.Sockets`, `WebClient` etc.) — é prova *por construção*, não
   substitui o teste sem rede. CT21 e CT13 físico ficam como "manual".
2. **Massa mínima** = projeto console `backend/tools/VarthexComanda.MassaMinima`
   (`--saida <pasta>`), que usa as mesmas migrações e casos de uso do app;
   **recusa** a pasta real (`%LOCALAPPDATA%\VarthexComanda`), recusa pasta
   não vazia sem `--forcar`, e imprime as contagens. Conteúdo: 5 categorias
   (1 inativa), 30 produtos (1 inativo, fotos sintéticas em alguns), 20
   números configurados, comandas com 1 item / muitos itens / itens repetidos,
   encerramentos confirmados e comandas abandonadas (canceladas), 90 dias de
   histórico, e três arquivos em `backups\` (válido, antigo — esquema da
   `InitialCreate` — e corrompido).
3. **Ensaios com o app real** (`scripts/homologacao/Executar-Ensaio.ps1`) rodam
   o app **instalado do pacote** com `VARTHEX_COMANDA_DADOS` numa pasta
   descartável gerada pela massa mínima, sem enviar teclas (só UI Automation
   `InvokePattern`, que não roubam foco), com trava: aborta se já houver
   `VarthexComanda` em execução; só encerra o PID que iniciou.
   Ensaios: **RNF03** (tempo até a janela, média de 5 aberturas, meta ≤ 5 s),
   **CT19** (segunda instância mostra o aviso e não abre outra janela),
   **CT20** (abrir comanda, adicionar item, término forçado do PID, reabrir e
   ver a mesma comanda/total, sem venda), **CT09/erro da maquininha**
   (Encerramento → Voltar → comanda aberta), **restauração** (restaurar uma
   cópia pela tela de Backup), **sem conexões de rede** do processo.
   Resultados registrados em `docs/homologacao/resultados-ct.md` com data, versão
   e evidência (saída do script).
4. **Documentos** em `docs/homologacao/`: checklist da Etapa 8 (com situação
   real de cada item), matriz de compatibilidade, resultados por CT (formato do
   `docs/templates/caso-teste.md`, condensado), roteiro do ensaio manual no PC
   real, guia do operador, pendências (QV01–QV14 com situação e "quem responde"
   + pendências técnicas: alerta de pouco espaço, CT21, assinatura de código,
   instalador MSI, poda de `corrompido-*`, etc.). `docs/docs/10-rastreabilidade.md`
   ganha a linha do CT17 e o ponteiro para a matriz gerada.

## Fora de escopo

Executar qualquer coisa no computador da loja; treinar pessoas; decidir as
respostas QV; assinar código.
