using VarthexComanda.Application.Atendimento;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence.Atendimento;
using VarthexComanda.Infrastructure.Tests.Homologacao;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Homologacao;

// CT09 e CT18 exercitados pelos ViewModels reais sobre um SQLite real e descartavel
// (BancoDeHomologacao, cedido por Infrastructure.Tests). Complementam os testes de ViewModel que
// usam repositorios em memoria.
public sealed class CriteriosDeTelaComBancoRealTests : IDisposable
{
    private readonly BancoDeHomologacao _banco = new();

    public void Dispose() => _banco.Dispose();

    // CT09 — criterio de docs/docs/09-testes-aceitacao.md: falha na cobranca externa (maquininha);
    // "Voltar" mantem a comanda aberta e nao cria venda.
    [Fact]
    [Trait("Caso", "CT09")]
    public void CT09_FalhaNaCobranca_VoltarMantemComandaAbertaENaoCriaVenda()
    {
        var comandas = new EfComandaRepository(_banco.Fabrica);
        var relogio = new RelogioFixo();
        var produto = _banco.CriarProduto("Refrigerante", 500);
        var comanda = comandas.AbrirComanda(10, relogio.UtcNow);
        comandas.AdicionarItem(comanda.Id, produto, 2, relogio.UtcNow);

        var viewModel = new EncerramentoViewModel(new EncerrarComanda(comandas, relogio), comandas);
        viewModel.Carregar(comanda.Id);
        bool? resultadoDoDialogo = null;
        viewModel.Concluido += (_, sucesso) => resultadoDoDialogo = sucesso;

        // a maquininha falhou: o operador NAO marca "cobranca aprovada" e volta
        Assert.False(viewModel.ConfirmarEncerrarCommand.CanExecute(null));
        viewModel.VoltarCommand.Execute(null);

        Assert.False(resultadoDoDialogo);
        using (var contexto = _banco.Fabrica.CreateDbContext())
        {
            Assert.Empty(contexto.Vendas.ToList());
        }
        var depois = new EfComandaRepository(_banco.Fabrica).BuscarComItens(comanda.Id)!;
        Assert.Equal(StatusComanda.Aberta, depois.Comanda.Status);
        Assert.Null(depois.Comanda.FechadaEm);
        Assert.Equal(1000, depois.Comanda.TotalCentavos);
        Assert.Single(depois.Itens);
        Assert.Single(new EfComandaRepository(_banco.Fabrica).ListarAbertas());

        // e a comanda segue utilizavel: com a cobranca refeita, o encerramento conclui
        viewModel.Carregar(comanda.Id);
        viewModel.CobrancaAprovada = true;
        viewModel.ConfirmarEncerrarCommand.Execute(null);
        Assert.True(resultadoDoDialogo);
        using var contextoFinal = _banco.Fabrica.CreateDbContext();
        Assert.Single(contextoFinal.Vendas.ToList());
    }

    // CT18 — criterio: quatro vendas totalizando R$ 120,00 geram ticket medio R$ 30,00.
    [Fact]
    [Trait("Caso", "CT18")]
    public void CT18_QuatroVendasQueSomamR120_GeramTicketMedioR30()
    {
        var comandas = new EfComandaRepository(_banco.Fabrica);
        var a = _banco.CriarProduto("Prato A", 3000);
        var b = _banco.CriarProduto("Prato B", 2500);
        var c = _banco.CriarProduto("Prato C", 3500);
        var vendas = new[] { (1, a, 15), (2, a, 16), (3, b, 17), (4, c, 18) }; // R$ 30 + 30 + 25 + 35 = 120
        foreach (var (numero, produto, horaUtc) in vendas)
        {
            var instante = new DateTime(2026, 9, 18, horaUtc, 0, 0, DateTimeKind.Utc);
            var comanda = comandas.AbrirComanda(numero, instante);
            comandas.AdicionarItem(comanda.Id, produto, 1, instante);
            comandas.EncerrarComanda(comanda.Id, instante);
        }

        // 20:00 UTC = 17:00 em Brasilia: "hoje" e 18/09 e as quatro vendas caem no dia
        var agora = new RelogioFixo(new DateTime(2026, 9, 18, 20, 0, 0, DateTimeKind.Utc));
        var repositorioDeVendas = new EfVendaRepository(_banco.Fabrica);
        var viewModel = new HistoricoViewModel(new ListarVendasPorData(repositorioDeVendas), new BuscarItensDaVenda(repositorioDeVendas), agora);

        Assert.Equal(4, viewModel.QuantidadeVendas);
        Assert.Equal(12000, viewModel.TotalDiaCentavos);
        Assert.Equal(3000, viewModel.TicketMedioCentavos);
    }
}
