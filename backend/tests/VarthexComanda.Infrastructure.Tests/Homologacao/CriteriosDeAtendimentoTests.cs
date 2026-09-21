using Microsoft.Data.Sqlite;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence.Atendimento;
using VarthexComanda.Infrastructure.Persistence.Catalogo;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Homologacao;

// Casos de aceitacao de atendimento (CT03, CT04, CT05, CT06, CT07, CT08, CT10, CT12) com os
// numeros LITERAIS de docs/docs/09-testes-aceitacao.md, usando casos de uso + repositorios reais
// sobre um SQLite real. Complementam (nao substituem) os testes por camada ja existentes.
public sealed class CriteriosDeAtendimentoTests : IDisposable
{
    private readonly BancoDeHomologacao _banco = new();
    private readonly RelogioFixo _relogio = new();
    private readonly EfComandaRepository _comandas;
    private readonly EfProdutoRepository _produtos;
    private readonly EfCategoriaRepository _categorias;

    public CriteriosDeAtendimentoTests()
    {
        _comandas = new EfComandaRepository(_banco.Fabrica);
        _produtos = new EfProdutoRepository(_banco.Fabrica);
        _categorias = new EfCategoriaRepository(_banco.Fabrica);
    }

    public void Dispose() => _banco.Dispose();

    private ComandaComItens Reler(int comandaId) =>
        new EfComandaRepository(_banco.Fabrica).BuscarComItens(comandaId)!;

    private int ContarVendas()
    {
        using var contexto = _banco.Fabrica.CreateDbContext();
        return contexto.Vendas.Count();
    }

    // CT03 — criterio de docs/docs/09-testes-aceitacao.md: duas inclusoes de R$ 18,00 resultam
    // em quantidade 2 e subtotal R$ 36,00.
    [Fact]
    [Trait("Caso", "CT03")]
    public void CT03_DuasInclusoesDeR18_ResultamEmQuantidade2ESubtotalR36()
    {
        var produto = _banco.CriarProduto("Prato do dia", 1800);
        var comanda = _comandas.AbrirComanda(10, _relogio.UtcNow);
        var adicionar = new AdicionarItem(_comandas, _produtos, _relogio);

        Assert.True(adicionar.Executar(comanda.Id, produto.Id, 1).Sucesso);
        Assert.True(adicionar.Executar(comanda.Id, produto.Id, 1).Sucesso);

        var persistido = Reler(comanda.Id);
        var item = Assert.Single(persistido.Itens);
        Assert.Equal(2, item.Quantidade);
        Assert.Equal(3600, item.SubtotalCentavos);
        Assert.Equal(3600, persistido.Comanda.TotalCentavos);
    }

    // CT04 — criterio: alterar o produto para R$ 22,00 nao muda o item ja lancado por R$ 18,00.
    [Fact]
    [Trait("Caso", "CT04")]
    public void CT04_AlterarPrecoDoProdutoParaR22_NaoMudaItemJaLancadoPorR18()
    {
        var produto = _banco.CriarProduto("Prato do dia", 1800);
        var comanda = _comandas.AbrirComanda(10, _relogio.UtcNow);
        var adicionar = new AdicionarItem(_comandas, _produtos, _relogio);
        Assert.True(adicionar.Executar(comanda.Id, produto.Id, 1).Sucesso);

        var alterar = new AlterarProduto(_produtos, _categorias, _relogio)
            .Executar(produto.Id, "Prato do dia (novo nome)", produto.CategoriaId, 2200, ativo: true);
        Assert.True(alterar.Sucesso);
        Assert.Equal(2200, _produtos.BuscarPorId(produto.Id)!.PrecoCentavos);

        var depois = Reler(comanda.Id);
        var item = Assert.Single(depois.Itens);
        Assert.Equal(1800, item.PrecoUnitarioCentavos);
        Assert.Equal(1800, item.SubtotalCentavos);
        Assert.Equal("Prato do dia", item.NomeProduto);
        Assert.Equal(1800, depois.Comanda.TotalCentavos);

        // uma nova inclusao usa o preco novo em outra linha; a linha antiga segue intacta
        Assert.True(adicionar.Executar(comanda.Id, produto.Id, 1).Sucesso);
        var final = Reler(comanda.Id);
        Assert.Equal(2, final.Itens.Count);
        Assert.Equal(new long[] { 1800, 2200 }, final.Itens.Select(i => i.PrecoUnitarioCentavos).ToArray());
        Assert.Equal(4000, final.Comanda.TotalCentavos);

        // e o historico da venda preserva o preco lancado
        var venda = new EncerrarComanda(_comandas, _relogio).Executar(comanda.Id).Valor!;
        var itensDaVenda = new EfVendaRepository(_banco.Fabrica).BuscarItensDaVenda(venda.Id)!;
        Assert.Equal(new long[] { 1800, 2200 }, itensDaVenda.Select(i => i.PrecoUnitarioCentavos).OrderBy(p => p).ToArray());
    }

    // CT05 — criterio: reduzir 3 para 2 recalcula e PERSISTE subtotal e total.
    [Fact]
    [Trait("Caso", "CT05")]
    public void CT05_ReduzirDe3Para2_RecalculaEPersisteSubtotalETotal()
    {
        var produto = _banco.CriarProduto("Refrigerante", 500);
        var comanda = _comandas.AbrirComanda(10, _relogio.UtcNow);
        var item = new AdicionarItem(_comandas, _produtos, _relogio)
            .Executar(comanda.Id, produto.Id, 3).Valor!.Itens[0];

        var resultado = new AlterarQuantidade(_comandas, _relogio).Executar(item.Id, 2);

        Assert.True(resultado.Sucesso);
        var persistido = Reler(comanda.Id); // leitura por repositorio/contexto novos
        var linha = Assert.Single(persistido.Itens);
        Assert.Equal(2, linha.Quantidade);
        Assert.Equal(1000, linha.SubtotalCentavos);
        Assert.Equal(1000, persistido.Comanda.TotalCentavos);
    }

    // CT06 — criterio: valor negativo ou fracionario nao e gravado.
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [Trait("Caso", "CT06")]
    public void CT06_QuantidadeNegativaOuZero_NaoEGravada(int quantidadeInvalida)
    {
        var produto = _banco.CriarProduto("Refrigerante", 500);
        var comanda = _comandas.AbrirComanda(10, _relogio.UtcNow);
        var item = new AdicionarItem(_comandas, _produtos, _relogio)
            .Executar(comanda.Id, produto.Id, 3).Valor!.Itens[0];

        var adicionar = new AdicionarItem(_comandas, _produtos, _relogio).Executar(comanda.Id, produto.Id, quantidadeInvalida);
        var alterar = new AlterarQuantidade(_comandas, _relogio).Executar(item.Id, quantidadeInvalida);

        Assert.False(adicionar.Sucesso);
        Assert.False(alterar.Sucesso);
        var persistido = Reler(comanda.Id);
        var linha = Assert.Single(persistido.Itens);
        Assert.Equal(3, linha.Quantidade);
        Assert.Equal(1500, linha.SubtotalCentavos);
        Assert.Equal(1500, persistido.Comanda.TotalCentavos);
    }

    // CT06 (ultima barreira) — mesmo por SQL direto, o banco recusa quantidade <= 0.
    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, -500)]
    [Trait("Caso", "CT06")]
    public void CT06_BancoRecusaQuantidadeNaoPositiva(int quantidade, long subtotal)
    {
        var produto = _banco.CriarProduto("Refrigerante", 500);
        var comanda = _comandas.AbrirComanda(10, _relogio.UtcNow);

        using (var conexao = new SqliteConnection($"Data Source={_banco.CaminhoDb};Foreign Keys=True;Pooling=False"))
        {
            conexao.Open();
            using var comando = conexao.CreateCommand();
            comando.CommandText =
                "INSERT INTO item_comanda (comanda_id, produto_id, nome_produto, preco_unitario_centavos, quantidade, subtotal_centavos, criado_em, atualizado_em) " +
                $"VALUES ({comanda.Id}, {produto.Id}, 'Refrigerante', 500, {quantidade}, {subtotal}, '2026-09-18 12:00:00', '2026-09-18 12:00:00')";

            Assert.Throws<SqliteException>(() => comando.ExecuteNonQuery());
        }
        Assert.Empty(Reler(comanda.Id).Itens);
    }

    // CT06 (fracionario) — por construcao: a quantidade e `int` em toda a cadeia de lancamento,
    // entao um valor fracionario nem chega a ser representavel. Prova estrutural, nao de tela.
    [Fact]
    [Trait("Caso", "CT06")]
    public void CT06_QuantidadeEInteiraEmToda_ACadeiaDeLancamento()
    {
        static Type TipoDaQuantidade(Type tipo, string metodo) =>
            tipo.GetMethod(metodo)!.GetParameters().Single(p => p.Name == "quantidade").ParameterType;

        Assert.Equal(typeof(int), TipoDaQuantidade(typeof(AdicionarItem), nameof(AdicionarItem.Executar)));
        Assert.Equal(typeof(int), TipoDaQuantidade(typeof(AlterarQuantidade), nameof(AlterarQuantidade.Executar)));
        Assert.Equal(typeof(int), TipoDaQuantidade(typeof(IComandaRepository), nameof(IComandaRepository.AdicionarItem)));
        Assert.Equal(typeof(int), TipoDaQuantidade(typeof(IComandaRepository), nameof(IComandaRepository.AlterarQuantidade)));
        Assert.Equal(typeof(int), typeof(ItemComanda).GetProperty(nameof(ItemComanda.Quantidade))!.PropertyType);
    }

    // CT07 — criterio: comanda vazia nao gera venda.
    [Fact]
    [Trait("Caso", "CT07")]
    public void CT07_EncerrarComandaVazia_NaoCriaVendaEComandaContinuaAberta()
    {
        var comanda = _comandas.AbrirComanda(10, _relogio.UtcNow);

        var resultado = new EncerrarComanda(_comandas, _relogio).Executar(comanda.Id);

        Assert.False(resultado.Sucesso);
        Assert.Equal("Adicione um item antes de encerrar.", resultado.Erros[0]);
        Assert.Equal(0, ContarVendas());
        Assert.Equal(StatusComanda.Aberta, Reler(comanda.Id).Comanda.Status);
    }

    // CT07 (defesa em profundidade) — mesmo chamando o repositorio direto, o banco recusa venda
    // de total zero (CK_venda_total_centavos_positivo) e a comanda nao fecha.
    [Fact]
    [Trait("Caso", "CT07")]
    public void CT07_RepositorioDireto_ComandaVazia_BancoRecusaEDesfazTudo()
    {
        var comanda = _comandas.AbrirComanda(10, _relogio.UtcNow);

        Assert.ThrowsAny<Exception>(() => _comandas.EncerrarComanda(comanda.Id, _relogio.UtcNow));

        Assert.Equal(0, ContarVendas());
        var depois = Reler(comanda.Id).Comanda;
        Assert.Equal(StatusComanda.Aberta, depois.Status);
        Assert.Null(depois.FechadaEm);
    }

    // CT08 — criterio: itens de R$ 18,00, R$ 18,00 e R$ 6,00 mostram total R$ 42,00.
    [Fact]
    [Trait("Caso", "CT08")]
    public void CT08_ItensDe18_18E6_TotalizamR42()
    {
        var prato = _banco.CriarProduto("Prato do dia", 1800);
        var executivo = _banco.CriarProduto("Prato executivo", 1800);
        var suco = _banco.CriarProduto("Suco", 600);
        var comanda = _comandas.AbrirComanda(10, _relogio.UtcNow);
        var adicionar = new AdicionarItem(_comandas, _produtos, _relogio);

        adicionar.Executar(comanda.Id, prato.Id, 1);
        adicionar.Executar(comanda.Id, executivo.Id, 1);
        var ultima = adicionar.Executar(comanda.Id, suco.Id, 1);

        Assert.Equal(4200, ultima.Valor!.Comanda.TotalCentavos);
        var persistido = Reler(comanda.Id);
        Assert.Equal(3, persistido.Itens.Count);
        Assert.Equal(4200, persistido.Itens.Sum(i => i.SubtotalCentavos));
        Assert.Equal(4200, persistido.Comanda.TotalCentavos);
        Assert.Equal(4200, new EncerrarComanda(_comandas, _relogio).Executar(comanda.Id).Valor!.TotalCentavos);
    }

    // CT10 — criterio: encerrar apos a cobranca manual cria a venda, fecha a comanda e nao guarda
    // dados de pagamento. A parte "sem dados de pagamento" e conferida no esquema real do banco.
    [Fact]
    [Trait("Caso", "CT10")]
    public void CT10_Encerrar_CriaVendaFechaComandaESemColunasDePagamento()
    {
        var produto = _banco.CriarProduto("Refrigerante", 500);
        var comanda = _comandas.AbrirComanda(10, _relogio.UtcNow);
        new AdicionarItem(_comandas, _produtos, _relogio).Executar(comanda.Id, produto.Id, 2);

        var venda = new EncerrarComanda(_comandas, _relogio).Executar(comanda.Id).Valor!;

        Assert.Equal(1000, venda.TotalCentavos);
        Assert.Equal(1, ContarVendas());
        var depois = Reler(comanda.Id).Comanda;
        Assert.Equal(StatusComanda.Fechada, depois.Status);
        Assert.NotNull(depois.FechadaEm);

        Assert.Equal(
            new[] { "comanda_id", "finalizada_em", "id", "numero", "status", "total_centavos" },
            Colunas("venda").OrderBy(c => c, StringComparer.Ordinal).ToArray());

        var termosDePagamento = new[] { "pag", "cartao", "card", "cpf", "cnpj", "bandeira", "nsu", "troco", "forma", "pix", "autoriza" };
        foreach (var tabela in new[] { "venda", "comanda", "item_comanda", "produto", "categoria", "configuracao" })
        {
            foreach (var coluna in Colunas(tabela))
            {
                Assert.DoesNotContain(termosDePagamento, t => coluna.Contains(t, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    // CT12 — criterio: itens, total e horario do historico correspondem ao encerramento.
    [Fact]
    [Trait("Caso", "CT12")]
    public void CT12_Historico_MostraItensTotalEHorarioDoEncerramento()
    {
        var refri = _banco.CriarProduto("Refrigerante", 500);
        var suco = _banco.CriarProduto("Suco", 750);
        var comanda = _comandas.AbrirComanda(10, new DateTime(2026, 9, 18, 17, 0, 0, DateTimeKind.Utc));
        var adicionar = new AdicionarItem(_comandas, _produtos, _relogio);
        adicionar.Executar(comanda.Id, refri.Id, 2);
        adicionar.Executar(comanda.Id, suco.Id, 3);
        var itensAntes = Reler(comanda.Id).Itens
            .Select(i => (i.NomeProduto, i.Quantidade, i.PrecoUnitarioCentavos, i.SubtotalCentavos)).ToList();

        // 17:45:30 UTC = 14:45:30 em Brasilia, no dia 18/09
        var encerramento = new DateTime(2026, 9, 18, 17, 45, 30, DateTimeKind.Utc);
        var venda = new EncerrarComanda(_comandas, new RelogioFixo(encerramento)).Executar(comanda.Id).Valor!;

        var historico = new ListarVendasPorData(new EfVendaRepository(_banco.Fabrica))
            .Executar(new DateTime(2026, 9, 18));

        var linha = Assert.Single(historico);
        Assert.Equal(venda.Id, linha.Venda.Id);
        Assert.Equal(10, linha.NumeroComanda);
        Assert.Equal(500 * 2 + 750 * 3, linha.Venda.TotalCentavos);
        Assert.Equal(encerramento.Ticks, linha.Venda.FinalizadaEm.Ticks);

        var itensDaVenda = new EfVendaRepository(_banco.Fabrica).BuscarItensDaVenda(venda.Id)!;
        Assert.Equal(
            itensAntes,
            itensDaVenda.OrderBy(i => i.Id)
                .Select(i => (i.NomeProduto, i.Quantidade, i.PrecoUnitarioCentavos, i.SubtotalCentavos)).ToList());
        Assert.Equal(linha.Venda.TotalCentavos, itensDaVenda.Sum(i => i.SubtotalCentavos));
    }

    private List<string> Colunas(string tabela)
    {
        using var conexao = new SqliteConnection($"Data Source={_banco.CaminhoDb};Pooling=False");
        conexao.Open();
        using var comando = conexao.CreateCommand();
        comando.CommandText = $"SELECT name FROM pragma_table_info('{tabela}')";
        using var leitor = comando.ExecuteReader();
        var nomes = new List<string>();
        while (leitor.Read()) nomes.Add(leitor.GetString(0));
        return nomes;
    }
}
