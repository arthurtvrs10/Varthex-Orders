# Checklist da Etapa 8

Os oito itens vêm de [Guia de implementação, Etapa 8](../docs/14-guia-implementacao.md). Cada um mostra a **situação real** e a evidência. Legenda:

- **Feito (automatizado)**: provado por testes ou ensaios executados, sem depender de ninguém.
- **Feito parcialmente**: o que uma máquina de desenvolvimento consegue provar foi feito; a parte que falta está dita.
- **Pendente – exige pessoa**: só existe com uma pessoa, um computador ou um equipamento que não estão aqui.

Nenhum item está totalmente "Feito": todos têm ao menos uma parte que depende de pessoa ou do computador da loja.

| # | Item | Situação | Evidência | O que falta |
| --- | --- | --- | --- | --- |
| 1 | Executar CT01 a CT22 | **Feito parcialmente** | [resultados-ct.md](resultados-ct.md); [rastreabilidade-testes.md](rastreabilidade-testes.md); 554 testes verdes; [ensaios E2, E3 e E4](evidencias/ensaio-2026-09-21.txt) | 19 dos 22 casos têm evidência executada (15 só automatizados; CT09, CT15, CT19 e CT20 também confirmados com o aplicativo real). CT13 (rede física desligada) e CT21 (Windows limpo) estão **pendentes**; CT17 (teclado físico) está **parcial** |
| 2 | Executar a massa mínima | **Feito parcialmente** | Ferramenta `backend\tools\VarthexComanda.MassaMinima`; teste `MassaMinimaTests` (contagens exatas, integridade, reprodutibilidade, recusas); geração real no cabeçalho de [ensaio-2026-09-21.txt](evidencias/ensaio-2026-09-21.txt) | Gerar a massa e abrir o aplicativo com ela **no computador da loja** ([roteiro](roteiro-ensaio-manual.md), parte 1) |
| 3 | Testar sem internet | **Feito parcialmente** (apoio) / **Pendente – exige pessoa** (o teste em si) | CT13 **estrutural**: `OperacaoOfflinePorConstrucaoTests` (os quatro assemblies do produto não referenciam APIs de rede); [ensaio E6](evidencias/ensaio-2026-09-21.txt): 0 conexões TCP/UDP do processo numa sessão pelas 5 telas | Operar de fato com a rede desligada no computador da loja ([roteiro](roteiro-ensaio-manual.md), parte 3). A prova por construção e o E6 **não substituem** esse teste |
| 4 | Testar no computador real | **Pendente – exige pessoa** | [matriz-compatibilidade.md](matriz-compatibilidade.md) (só a máquina de desenvolvimento, como referência); [roteiro-ensaio-manual.md](roteiro-ensaio-manual.md) | O computador da loja não é conhecido (QV07). Executar o roteiro inteiro nele, incluindo RNF02, RNF03 (o E1 mediu 1736 ms de média **na máquina de desenvolvimento**), RNF07, escalas 100/150/200 %, reinício do PC com comanda aberta e CT21 |
| 5 | Simular erro na maquininha e confirmar que a comanda permanece aberta | **Feito parcialmente** | CT09: 5 testes automatizados (`CriteriosDeTelaComBancoRealTests.CT09_FalhaNaCobranca_VoltarMantemComandaAbertaENaoCriaVenda` e outros); [ensaio E4](evidencias/ensaio-2026-09-21.txt): Encerramento, "Voltar para a comanda", comanda aberta com R$ 24,00 e vendas do dia em 0 | Repetir com a **maquininha de verdade** ([roteiro](roteiro-ensaio-manual.md), parte 9) |
| 6 | Restaurar backup | **Feito parcialmente** | Testes de restauração e de banco corrompido (CT14, CT15, CT16 em [rastreabilidade-testes.md](rastreabilidade-testes.md)); [ensaio E5](evidencias/ensaio-2026-09-21.txt): backup válido restaurado pela tela (o app reiniciou e voltou às 5 comandas da massa); backup corrompido recusado com o banco ativo intacto (SHA-256 igual) | (a) A restauração pela caixa **"Selecionar arquivo..." não foi automatizada** (o diálogo de arquivos do Windows não expõe padrões de UI Automation; a recusa do corrompido foi exercitada pelo modo de restauração). (b) Troca de pendrive e ensaio de restauração no computador real ([roteiro](roteiro-ensaio-manual.md), parte 10) |
| 7 | Treinar os operadores | **Pendente – exige pessoa** | Guia entregue: [guia-do-operador.md](guia-do-operador.md) | O treinamento em si, com os operadores reais; registrar quem foi treinado e a data. É também a ocasião de validar o texto de QV04 |
| 8 | Registrar pendências restantes | **Feito (documentação)**; as respostas em si ficam **Pendentes – exige pessoa** | [pendencias.md](pendencias.md): QV01–QV14 com situação real e pendências técnicas; ponteiro em [13 Pendências](../docs/13-pendencias-validacao.md) | Respostas do responsável às QV; o registro delas segue o procedimento do documento 13 |

## Conclusão do checklist

- Concluídos sem terceiros: a parte automatizável dos itens 1, 2, 5 e 6, e o registro documental do item 8.
- Dependem de pessoa ou do computador da loja: os itens 3 (teste físico), 4 e 7 por inteiro, e o restante dos itens 1, 2, 5 e 6.
- A Etapa 8 só pode ser dada como concluída depois que o [roteiro](roteiro-ensaio-manual.md) for executado no computador da loja e os resultados forem copiados para [resultados-ct.md](resultados-ct.md).
