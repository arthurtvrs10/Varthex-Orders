# Pendências de validação

Estas decisões devem ser respondidas antes da homologação.

Situação real de cada pendência (decidida no código, comportamento existente sem decisão registrada ou sem rastro), quem responde e o que falta: [homologacao/pendencias.md](../homologacao/pendencias.md).

| Código | Pergunta | Decisor | Prazo |
| --- | --- | --- | --- |
| QV01 | Como o cliente será associado ao número: ficha, mesa, pulseira ou outro método? | Responsável | Antes do protótipo |
| QV02 | Quantos números aparecerão e poderão ser digitados livremente? | Operação | Antes da tela final |
| QV03 | Existem produtos por peso, meia porção, adicionais ou combinações? | Operação | Antes do modelo definitivo |
| QV04 | Qual texto deixa claro que a cobrança foi realizada fora do aplicativo? | Caixa | Antes da tela final |
| QV05 | Qual adquirente e modelo de maquininha são usados? | Responsável | Antes de estimar integração futura |
| QV06 | Desconto, acréscimo, cortesia ou estorno são necessários no MVP? | Responsável | Antes de congelar escopo |
| QV07 | Windows foi confirmado; qual versão, arquitetura, memória, resolução e escala do computador? | Desenvolvimento | Antes do empacotamento |
| QV08 | Existe impressora térmica e ela é necessária inicialmente? | Responsável | Antes da homologação |
| QV09 | Qual dispositivo externo receberá backups? | Responsável | Antes da implantação |
| QV10 | Quem pode alterar preços, cancelar comandas e restaurar backup? | Responsável | Antes de decidir usuários |
| QV11 | Por quanto tempo o histórico deve ser mantido? | Responsável | Antes da política de retenção |
| QV12 | O computador é compartilhado por diferentes contas do Windows? | Responsável | Antes da instalação |
| QV13 | A aplicação deverá iniciar automaticamente com o Windows? | Operação | Antes da homologação |
| QV14 | Qual limite máximo de itens, quantidade e valor por comanda deve ser aceito? | Responsável | Antes dos testes de limite |

## Registro da decisão

Ao responder uma pendência:

1. registrar a resposta neste arquivo;
2. atualizar requisitos e regras afetadas;
3. atualizar testes e rastreabilidade;
4. registrar a mudança no `CHANGELOG.md`;
5. criar ADR se houver impacto arquitetural duradouro.
