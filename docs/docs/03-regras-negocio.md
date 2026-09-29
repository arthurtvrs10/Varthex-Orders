# Regras de negócio

| Código | Regra |
| --- | --- |
| RN01 | Somente uma comanda aberta pode usar determinado número |
| RN02 | O número pode ser reutilizado após fechamento ou cancelamento |
| RN03 | Somente produto ativo pode ser incluído em nova comanda |
| RN04 | O item copia nome e preço praticados no lançamento |
| RN05 | Alterar o preço atual não modifica itens ou vendas anteriores |
| RN06 | Quantidade é um inteiro maior que zero |
| RN07 | Subtotal é preço unitário multiplicado pela quantidade |
| RN08 | Total da comanda é a soma dos subtotais válidos |
| RN09 | Comanda vazia não pode gerar venda |
| RN10 | Comanda fechada é imutável no MVP |
| RN11 | O MVP não processa nem registra pagamento, forma, valor recebido ou troco |
| RN12 | A tela final exibe produto, quantidade, preço unitário, subtotal e total |
| RN13 | O operador somente encerra após confirmar cobrança externa |
| RN14 | Encerrar grava venda e situação da comanda atomicamente |
| RN15 | Falha no encerramento desfaz todas as gravações da operação |
| RN16 | Desativar produto ou categoria preserva o histórico |
| RN17 | Produto com histórico não pode ser excluído fisicamente |
| RN18 | Cancelar comanda com itens exige confirmação e não gera venda |
| RN19 | Restaurar backup exige confirmação e cópia preventiva da base atual |
| RN20 | Datas usam relógio local e são exibidas no padrão brasileiro |
| RN21 | Uma segunda instância do aplicativo não pode acessar a mesma base enquanto a primeira estiver ativa |
| RN22 | Reiniciar o aplicativo não fecha cancela nem altera uma comanda que estava aberta |
| RN23 | Cada alteração confirmada de item deve ser persistida antes de a interface indicar sucesso |

## Invariantes de dados

RN28: nome do cliente é opcional, limitado a 80 caracteres sem controles/quebras de
linha. Espaços nas pontas são removidos; vazio remove o nome. Somente comandas
abertas podem ser renomeadas. O nome fica no histórico da sessão e não é reutilizado
automaticamente quando o mesmo número abre uma nova comanda.

RN24–RN27: prazos fixos de licença, bloqueio por licença inválida/vencida,
preservação de dados e detecção de atraso do relógio estão definidos em
[Licenciamento offline](20-licenciamento-offline.md).

- `subtotal_centavos = preco_unitario_centavos * quantidade`;
- `comanda.total_centavos = soma(item_comanda.subtotal_centavos)`;
- `venda.total_centavos = comanda.total_centavos` revalidado no encerramento;
- uma comanda possui no máximo uma venda;
- uma venda não possui pagamento no MVP;
- nenhuma comanda com status diferente de `ABERTA` recebe alterações de itens;
- nenhum número aparece em duas comandas abertas simultaneamente.
- encerramento inesperado não cria venda nem muda automaticamente o status da comanda;
- uma venda só existe após a confirmação explícita e o commit do encerramento.

## Arredondamento

O MVP trabalha apenas com preço unitário em centavos e quantidade inteira. Não existe arredondamento por item. Ticket médio usa divisão do total em centavos pela quantidade de vendas e deve ser apresentado com duas casas decimais.
