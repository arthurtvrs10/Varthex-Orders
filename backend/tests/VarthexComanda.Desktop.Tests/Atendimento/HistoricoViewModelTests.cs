using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Tests.Atendimento;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Atendimento;

public class HistoricoViewModelTests
{
    private static Venda CriarVenda(int id, int comandaId, long totalCentavos, DateTime finalizadaEmUtc) => new()
    {
        Id = id,
        ComandaId = comandaId,
        Numero = id,
        TotalCentavos = totalCentavos,
        FinalizadaEm = finalizadaEmUtc,
        Status = StatusVenda.Concluida
    };

    [Fact]
    public void Construtor_CarregaVendasDeHojeAutomaticamente()
    {
        var relogio = new FakeClock { UtcNow = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc) };
        var vendas = new FakeVendaRepository();
        vendas.AdicionarVenda(CriarVenda(1, 1, 1000, new DateTime(2026, 9, 18, 15, 0, 0, DateTimeKind.Utc)), 10, new List<ItemComanda>());

        var viewModel = new HistoricoViewModel(new ListarVendasPorData(vendas), new BuscarItensDaVenda(vendas), relogio);

        Assert.Single(viewModel.Vendas);
    }

    [Fact]
    [Trait("Caso", "CT18")]
    public void Resumo_DuasVendas_CalculaQuantidadeTotalETicketMedio()
    {
        var relogio = new FakeClock { UtcNow = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc) };
        var vendas = new FakeVendaRepository();
        vendas.AdicionarVenda(CriarVenda(1, 1, 1000, new DateTime(2026, 9, 18, 15, 0, 0, DateTimeKind.Utc)), 10, new List<ItemComanda>());
        vendas.AdicionarVenda(CriarVenda(2, 2, 2000, new DateTime(2026, 9, 18, 16, 0, 0, DateTimeKind.Utc)), 20, new List<ItemComanda>());

        var viewModel = new HistoricoViewModel(new ListarVendasPorData(vendas), new BuscarItensDaVenda(vendas), relogio);

        Assert.Equal(2, viewModel.QuantidadeVendas);
        Assert.Equal(3000, viewModel.TotalDiaCentavos);
        Assert.Equal(1500, viewModel.TicketMedioCentavos);
    }

    [Fact]
    public void DiaSemVendas_ResumoZeradoSemExcecao()
    {
        var relogio = new FakeClock { UtcNow = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc) };
        var vendas = new FakeVendaRepository();

        var viewModel = new HistoricoViewModel(new ListarVendasPorData(vendas), new BuscarItensDaVenda(vendas), relogio);

        Assert.Empty(viewModel.Vendas);
        Assert.Equal(0, viewModel.QuantidadeVendas);
        Assert.Equal(0, viewModel.TotalDiaCentavos);
        Assert.Equal(0, viewModel.TicketMedioCentavos);
    }

    [Fact]
    public void BuscaPorNumero_FiltraListaCarregadaSemNovaConsulta()
    {
        var relogio = new FakeClock { UtcNow = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc) };
        var vendas = new FakeVendaRepository();
        vendas.AdicionarVenda(CriarVenda(1, 1, 1000, new DateTime(2026, 9, 18, 15, 0, 0, DateTimeKind.Utc)), 10, new List<ItemComanda>());
        vendas.AdicionarVenda(CriarVenda(2, 2, 2000, new DateTime(2026, 9, 18, 16, 0, 0, DateTimeKind.Utc)), 20, new List<ItemComanda>());
        var viewModel = new HistoricoViewModel(new ListarVendasPorData(vendas), new BuscarItensDaVenda(vendas), relogio);
        var chamadasAntes = vendas.ChamadasListarPorData;

        viewModel.TextoBuscaNumero = "20";

        Assert.Single(viewModel.Vendas);
        Assert.Equal(20, viewModel.Vendas[0].NumeroComanda);
        Assert.Equal(chamadasAntes, vendas.ChamadasListarPorData);
    }

    [Fact]
    public void BuscaPorNumero_SemCorrespondencia_ResumoDiarioPermaneceEListaFicaVazia()
    {
        var relogio = new FakeClock { UtcNow = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc) };
        var vendas = new FakeVendaRepository();
        vendas.AdicionarVenda(CriarVenda(1, 1, 1000, new DateTime(2026, 9, 18, 15, 0, 0, DateTimeKind.Utc)), 10, new List<ItemComanda>());
        vendas.AdicionarVenda(CriarVenda(2, 2, 2000, new DateTime(2026, 9, 18, 16, 0, 0, DateTimeKind.Utc)), 20, new List<ItemComanda>());
        var viewModel = new HistoricoViewModel(new ListarVendasPorData(vendas), new BuscarItensDaVenda(vendas), relogio);

        viewModel.TextoBuscaNumero = "999";

        Assert.Empty(viewModel.Vendas);
        Assert.Equal(0, viewModel.QuantidadeExibida);
        Assert.Equal(2, viewModel.QuantidadeVendas);
        Assert.Equal(3000, viewModel.TotalDiaCentavos);
        Assert.Equal(1500, viewModel.TicketMedioCentavos);
        Assert.Equal("Nenhuma venda encontrada para esse número.", viewModel.MensagemListaVazia);
    }

    [Fact]
    [Trait("Caso", "CT12")]
    public void SelecionarVenda_PopulaItensDaVendaSelecionada()
    {
        var relogio = new FakeClock { UtcNow = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc) };
        var vendas = new FakeVendaRepository();
        var item = new ItemComanda { Id = 1, ComandaId = 1, ProdutoId = 1, NomeProduto = "Refrigerante", PrecoUnitarioCentavos = 500, Quantidade = 2, SubtotalCentavos = 1000, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow };
        vendas.AdicionarVenda(CriarVenda(1, 1, 1000, new DateTime(2026, 9, 18, 15, 0, 0, DateTimeKind.Utc)), 10, new[] { item });
        var viewModel = new HistoricoViewModel(new ListarVendasPorData(vendas), new BuscarItensDaVenda(vendas), relogio);

        viewModel.VendaSelecionada = viewModel.Vendas[0];

        Assert.Single(viewModel.ItensDaVendaSelecionada);
        Assert.Equal(2, viewModel.ItensDaVendaSelecionada[0].Quantidade);
    }

    [Fact]
    public void AtualizarVendas_RepositorioFalha_MostraMensagemSemPropagarExcecao()
    {
        var relogio = new FakeClock { UtcNow = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc) };
        var vendas = new FakeVendaRepository { LancarExcecao = true };

        var viewModel = new HistoricoViewModel(new ListarVendasPorData(vendas), new BuscarItensDaVenda(vendas), relogio);

        Assert.Empty(viewModel.Vendas);
        Assert.Equal("Não foi possível carregar as vendas. Tente novamente.", viewModel.Mensagem);
    }

    [Fact]
    public void SelecionarVenda_RepositorioFalha_MostraMensagemSemPropagarExcecao()
    {
        var relogio = new FakeClock { UtcNow = new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc) };
        var vendas = new FakeVendaRepository();
        vendas.AdicionarVenda(CriarVenda(1, 1, 1000, new DateTime(2026, 9, 18, 15, 0, 0, DateTimeKind.Utc)), 10, new List<ItemComanda>());
        var viewModel = new HistoricoViewModel(new ListarVendasPorData(vendas), new BuscarItensDaVenda(vendas), relogio);
        var vendaCarregada = viewModel.Vendas[0];
        vendas.LancarExcecao = true;

        viewModel.VendaSelecionada = vendaCarregada;

        Assert.Empty(viewModel.ItensDaVendaSelecionada);
        Assert.Equal("Não foi possível carregar os itens da venda. Tente novamente.", viewModel.Mensagem);
    }
}
