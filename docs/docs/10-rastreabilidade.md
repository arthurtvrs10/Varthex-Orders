# Rastreabilidade

| Objetivo | Requisitos | Regras | Casos de uso | Testes |
| --- | --- | --- | --- | --- |
| OBJ01 | RF06-RF13, RF27 | RN01-RN10, RN21-RN23 | UC02, UC03, UC04, UC06, UC10 | CT01-CT07, CT17, CT19, CT20 |
| OBJ02 | RF14-RF17 | RN09-RN15 | UC05 | CT07-CT11 |
| OBJ03 | RF18-RF20 | RN04, RN05, RN10, RN16, RN17 | UC07 | CT04, CT12, CT18 |
| OBJ04 | RF21-RF24, RF27 | RN19, RN22, RN23 | UC08, UC09, UC10 | CT14-CT16, CT20 |
| OBJ05 | RF25-RF27 | DEC02, DEC05, DEC07, DEC08 | Todos | CT13, CT16, CT19-CT22 |

Nota sobre o CT17 (navegar por teclado): o caso percorre localizar produto, adicionar item e iniciar o encerramento. Foi colocado no OBJ01 porque a maior parte do fluxo (abrir comanda, localizar, adicionar, corrigir quantidade) é de atendimento; o passo "iniciar encerramento" (RF14) toca o OBJ02. O requisito que o justifica é o RNF18 (acessibilidade por teclado), que não pertence a nenhum objetivo desta tabela.

## Rastreabilidade executável dos casos de teste

Cada teste automatizado ligado a um caso carrega `[Trait("Caso", "CTnn")]`. A matriz [CT → testes](../homologacao/rastreabilidade-testes.md) é **gerada** pelo teste `RastreabilidadeDosCasosTests` (não editar à mão) e o teste falha se um caso automatizável ficar sem cobertura. O que ainda falta provar por caso (CT13 físico, CT21, teclado físico, maquininha real) está em [resultados-ct.md](../homologacao/resultados-ct.md); o processo geral da Etapa 8, em [homologacao/README.md](../homologacao/README.md).

## Regra de manutenção

Todo requisito novo deve ter:

- objetivo relacionado;
- regra aplicável ou justificativa de ausência;
- caso de uso ou fluxo;
- pelo menos um teste de aceitação;
- etapa do roadmap;
- atualização no histórico de mudanças.

Nenhum requisito pode ser considerado implementado sem evidência de teste.
