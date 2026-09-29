# Testes e aceitação

## Cenários essenciais

| Código | Cenário | Critério de aceitação |
| --- | --- | --- |
| CT01 | Abrir comanda livre | Número fica ocupado e comanda fica `ABERTA` |
| CT02 | Impedir duplicidade | Segunda abertura do mesmo número é recusada |
| CT03 | Adicionar produto | Duas inclusões de R$ 18,00 resultam em quantidade 2 e subtotal R$ 36,00 |
| CT04 | Preservar preço | Alterar produto para R$ 22,00 não muda item lançado por R$ 18,00 |
| CT05 | Corrigir quantidade | Reduzir 3 para 2 recalcula e persiste subtotal e total |
| CT06 | Recusar quantidade inválida | Valor negativo ou fracionário não é gravado |
| CT07 | Recusar comanda vazia | Nenhuma venda é criada |
| CT08 | Exibir total | Itens de R$ 18,00, R$ 18,00 e R$ 6,00 mostram total R$ 42,00 |
| CT09 | Falha na cobrança externa | Voltar mantém comanda aberta e não cria venda |
| CT10 | Encerrar após cobrança manual | Cria venda e fecha comanda sem dados de pagamento |
| CT11 | Rollback | Falha entre venda e fechamento desfaz tudo |
| CT12 | Histórico | Itens, total e horário correspondem ao encerramento |
| CT13 | Operar offline | Cadastro, comanda, total, encerramento e histórico funcionam sem rede |
| CT14 | Criar backup | Cópia abre, passa na integridade e possui checksum |
| CT15 | Rejeitar backup corrompido | Base ativa permanece inalterada |
| CT16 | Atualizar aplicação | Migrações completam e totais anteriores permanecem |
| CT17 | Navegar por teclado | É possível localizar, adicionar e iniciar encerramento |
| CT18 | Resumo diário | Quatro vendas totalizando R$ 120,00 geram ticket médio R$ 30,00 |
| CT19 | Impedir segunda instância | Com o aplicativo aberto, nova execução exibe aviso e não abre outra conexão com a base |
| CT20 | Recuperar comandas abertas | Após término forçado, a reabertura mostra os mesmos itens e totais sem criar venda |
| CT21 | Instalar no Windows | Pacote autocontido inicia em instalação limpa sem exigir SDK ou runtime separado |
| CT22 | Controlar logs | Rotação remove arquivos além da retenção e respeita o limite de armazenamento configurado |

## Testes adicionais obrigatórios

CT24: salvar/corrigir/remover nome opcional, validar limite, reabrir aplicativo e
conferir persistência, exibir nome na grade/encerramento/histórico, impedir alteração
após encerramento/cancelamento e não herdar nome ao reutilizar número. Migração
deve preservar comandas antigas com nome nulo.

CT23: ativação e renovação offline, assinatura inválida, computador incorreto,
vencimento, chave vitalícia, relógio atrasado e persistência protegida.
Ver [Licenciamento offline](20-licenciamento-offline.md).

### Catálogo

- nomes vazios e com espaços externos;
- preços zero e negativos;
- categoria inativa;
- desativação com histórico;
- busca sem diferenciar maiúsculas.

### Concorrência e transação

- duas tentativas simultâneas de abrir o mesmo número;
- falha de banco ao adicionar item;
- falha depois de criar venda e antes de fechar comanda;
- repetição da confirmação de encerramento;
- reinício após commit.

### Backup

- pasta inexistente;
- pasta sem permissão;
- disco cheio;
- arquivo incompatível;
- checksum inválido;
- base antiga que exige migração;
- interrupção antes da substituição.

### Windows e recuperação

- instalação e desinstalação sem remover a pasta de dados;
- escala de exibição de 100%, 150% e 200%;
- segunda tentativa de abertura do executável;
- término forçado após item confirmado e antes de novo lançamento;
- reinício do computador com comanda aberta;
- diretório de dados sem permissão e disco com pouco espaço;
- rotação dos logs acima do período e do tamanho configurados.

## Pirâmide de testes

- unidade: valores, estados e regras puras;
- integração: repositórios, índices, migrações e rollback;
- interface: lançamento, correção, total e confirmação;
- sistema: fluxo completo offline;
- aceitação: operação real no balcão;
- recuperação: backup, corrupção e restauração.

## Massa mínima

- 5 categorias, sendo 1 inativa;
- 30 produtos, sendo 1 inativo;
- 20 números de comanda;
- comandas com 1, muitos e itens repetidos;
- encerramentos confirmados e abandonados;
- 90 dias de histórico;
- backup válido, antigo e corrompido.

## Definição de pronto

Uma tarefa está pronta quando:

- atende ao requisito e às regras relacionadas;
- possui testes automatizados adequados;
- não quebra operação offline;
- não introduz dados de pagamento;
- atualiza migrações quando necessário;
- possui mensagens compreensíveis;
- passa na revisão de código;
- atualiza documentação e rastreabilidade.
