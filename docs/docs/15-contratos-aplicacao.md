# Contratos da aplicação

Os nomes são sugestões. A implementação pode adaptá-los sem alterar comportamento.

> **Correspondência com o código (2026-09-21).** O comportamento dos contratos abaixo foi mantido, mas as interfaces foram organizadas assim:
> - `VendaRepository.salvar`/`ComandaRepository.salvar` não existem como tais: as escritas são métodos por verbo em `IComandaRepository` (`AbrirComanda`, `AdicionarItem`, `AlterarQuantidade`, `RemoverItem`, `CancelarComanda`, `EncerrarComanda`), cada um com uma transação própria; `IVendaRepository` é somente leitura (`ListarPorData`, `BuscarItensDaVenda`).
> - O encerramento transacional (carregar, exigir `ABERTA`, exigir ao menos um item, recalcular, criar a venda e fechar a comanda) acontece dentro de `IComandaRepository.EncerrarComanda`, não em `VendaRepository.salvar` nem numa `UnidadeTrabalho` separada.
> - `RecuperarAtendimento` é `IComandaRepository.ListarAbertas` (mais `BuscarComItens`); `InstanciaAplicacao` é `SingleInstanceGuard`; `ConsultarHistorico` é `ListarVendasPorData`.
> - As operações são síncronas (sem `CancellationToken`) e as datas são guardadas em UTC.

## Tipos de valor

### Dinheiro

```text
Dinheiro
  centavos: inteiro >= 0
  somar(outro): Dinheiro
  multiplicar(quantidade inteira): Dinheiro
  formatarBRL(): texto
```

Não usar `float` ou `double` para dinheiro.

### Quantidade

```text
Quantidade
  valor: inteiro > 0
```

## Repositórios

```text
CategoriaRepository
  salvar(categoria)
  buscarPorId(id)
  listarAtivas()
  existeNome(nome, ignorarId?)

ProdutoRepository
  salvar(produto)
  buscarPorId(id)
  listarAtivosPorCategoria(categoriaId)
  pesquisarAtivos(texto)

ComandaRepository
  salvar(comanda)
  buscarAbertaPorNumero(numero)
  listarNumerosOcupados()
  buscarComItens(id)

VendaRepository
  salvar(venda)
  buscarPorId(id)
  existePorComanda(comandaId)
  listarPorPeriodo(inicio, fim)

BackupRegistroRepository
  salvar(registro)
  listarRecentes(limite)

UnidadeTrabalho
  executarEmTransacao(operacao)

InstanciaAplicacao
  tentarAdquirirBloqueio() -> booleano
  liberarBloqueio()
```

## Casos de uso

```text
AbrirComanda.executar(numero) -> ComandaResumo
AdicionarItem.executar(comandaId, produtoId, quantidade) -> ComandaDetalhe
AlterarQuantidade.executar(itemId, quantidade) -> ComandaDetalhe
RemoverItem.executar(itemId) -> ComandaDetalhe
CancelarComanda.executar(comandaId) -> void
PrepararEncerramento.executar(comandaId) -> ResumoCobrancaManual
EncerrarComanda.executar(comandaId, confirmacaoExterna) -> VendaDetalhe
ConsultarHistorico.executar(periodo) -> lista de VendaResumo
CriarBackup.executar(destino) -> ResultadoBackup
RestaurarBackup.executar(arquivo, confirmacao) -> ResultadoRestauracao
RecuperarAtendimento.executar() -> lista de ComandaResumo
```

## Resumo de cobrança manual

```text
ResumoCobrancaManual
  comandaId
  numeroComanda
  itens[]
    nome
    quantidade
    precoUnitarioCentavos
    subtotalCentavos
  totalCentavos
  instrucao = "Digite este valor na maquininha"
```

Não adicionar forma de pagamento a esse contrato no MVP.

## Encerramento transacional

```text
iniciar transação
  carregar comanda e itens
  exigir status ABERTA
  exigir pelo menos um item
  recalcular total
  exigir confirmação externa verdadeira
  exigir que não exista venda para a comanda
  criar venda com total recalculado
  alterar comanda para FECHADA
  definir fechada_em
confirmar transação
```

Qualquer exceção executa rollback.

## Contratos técnicos no .NET

- valores monetários usam `long` em centavos; `double` e `float` são proibidos;
- datas persistidas usam texto ISO 8601 e são convertidas de forma explícita;
- operações assíncronas recebem `CancellationToken` quando puderem aguardar arquivo ou banco;
- `DbContext` não é compartilhado entre telas nem mantido durante toda a aplicação;
- ViewModels dependem de casos de uso e nunca de `VarthexDbContext`;
- o mutex de instância única é adquirido antes de abrir a conexão principal;
- falhas técnicas são convertidas em resultado ou exceção de aplicação compreensível para a interface.

## Erros de domínio

| Código | Situação |
| --- | --- |
| COMANDA_NUMERO_OCUPADO | Já existe comanda aberta com o número |
| COMANDA_NAO_ENCONTRADA | Identificador inexistente |
| COMANDA_NAO_ABERTA | Operação exige status aberta |
| COMANDA_VAZIA | Encerramento sem itens |
| PRODUTO_INATIVO | Inclusão de produto não disponível |
| QUANTIDADE_INVALIDA | Quantidade não inteira ou menor que um |
| CONFIRMACAO_EXTERNA_AUSENTE | Encerramento sem confirmação humana |
| VENDA_DUPLICADA | Já existe venda para a comanda |
| BACKUP_INVALIDO | Arquivo incompatível ou corrompido |
| APLICACAO_JA_ABERTA | Outra instância já está utilizando a base local |
| BANCO_INDISPONIVEL | Base ausente, bloqueada, sem permissão ou com falha de integridade |

Mensagens de interface devem traduzir esses códigos para linguagem simples.
