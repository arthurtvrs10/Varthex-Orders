using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Tests.Atendimento;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Atendimento;

public class EncerramentoViewModelTests
{
    private static (EncerramentoViewModel viewModel, FakeComandaRepository comandas, int comandaId) CriarViewModel(bool comItem = true)
    {
        var relogio = new FakeClock();
        var comandas = new FakeComandaRepository();
        var comanda = comandas.AbrirComanda(10, relogio.UtcNow);
        if (comItem)
        {
            var produto = new Produto { Id = 1, CategoriaId = 1, Nome = "Refrigerante", PrecoCentavos = 500, Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow };
            comandas.AdicionarItem(comanda.Id, produto, 2, relogio.UtcNow);
        }

        var viewModel = new EncerramentoViewModel(new EncerrarComanda(comandas, relogio), comandas);
        viewModel.Carregar(comanda.Id);
        return (viewModel, comandas, comanda.Id);
    }

    [Fact]
    [Trait("Caso", "CT08")]
    public void Carregar_ComandaComItens_PreencheNumeroTotalEItens()
    {
        var (viewModel, _, _) = CriarViewModel();

        Assert.Equal(10, viewModel.NumeroComanda);
        Assert.Equal(1000, viewModel.TotalCentavos);
        Assert.Single(viewModel.Itens);
    }

    [Fact]
    [Trait("Caso", "CT09")]
    public void ConfirmarEncerrar_SemCobrancaAprovada_ComandoDesabilitado()
    {
        var (viewModel, _, _) = CriarViewModel();

        Assert.False(viewModel.ConfirmarEncerrarCommand.CanExecute(null));
    }

    [Fact]
    [Trait("Requisito", "RNF18")]
    [Trait("Caso", "CT09")]
    public void ConfirmarEncerrar_ExecutadoDiretamenteSemCobrancaAprovada_NuncaCriaVenda()
    {
        var (viewModel, comandas, _) = CriarViewModel();
        bool eventoDisparado = false;
        viewModel.Concluido += (_, __) => eventoDisparado = true;

        // Execute do comando respeita CanExecute; o metodo privado (invocacao direta) tem a propria guarda.
        viewModel.ConfirmarEncerrarCommand.Execute(null);
        typeof(EncerramentoViewModel)
            .GetMethod("ConfirmarEncerrar", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(viewModel, null);

        Assert.False(eventoDisparado);
        Assert.Single(comandas.ListarAbertas());
    }

    [Fact]
    [Trait("Requisito", "RNF18")]
    public void ConfirmarEncerrar_HabilitaAoMarcarEDesabilitaAoDesmarcar()
    {
        var (viewModel, _, _) = CriarViewModel();
        var mudancas = 0;
        viewModel.ConfirmarEncerrarCommand.CanExecuteChanged += (_, __) => mudancas++;

        viewModel.CobrancaAprovada = true;
        Assert.True(viewModel.ConfirmarEncerrarCommand.CanExecute(null));
        viewModel.CobrancaAprovada = false;
        Assert.False(viewModel.ConfirmarEncerrarCommand.CanExecute(null));

        Assert.Equal(2, mudancas);
    }

    [Fact]
    [Trait("Caso", "CT10")]
    public void ConfirmarEncerrar_CobrancaAprovada_FechaComandaEDisparaConcluidoTrue()
    {
        var (viewModel, comandas, comandaId) = CriarViewModel();
        bool? resultadoEvento = null;
        viewModel.Concluido += (_, sucesso) => resultadoEvento = sucesso;
        viewModel.CobrancaAprovada = true;

        viewModel.ConfirmarEncerrarCommand.Execute(null);

        Assert.True(resultadoEvento);
        Assert.Empty(comandas.ListarAbertas());
    }

    [Fact]
    [Trait("Caso", "CT07")]
    public void ConfirmarEncerrar_ComandaVazia_MostraMensagemENaoDisparaConcluido()
    {
        var (viewModel, comandas, comandaId) = CriarViewModel(comItem: false);
        bool eventoDisparado = false;
        viewModel.Concluido += (_, __) => eventoDisparado = true;
        viewModel.CobrancaAprovada = true;

        viewModel.ConfirmarEncerrarCommand.Execute(null);

        Assert.False(eventoDisparado);
        Assert.Equal("Adicione um item antes de encerrar.", viewModel.Mensagem);
        Assert.Single(comandas.ListarAbertas());
    }

    [Fact]
    public void Carregar_SegundaVez_ResetaCobrancaAprovadaEMensagem()
    {
        var (viewModel, comandas, _) = CriarViewModel();
        viewModel.CobrancaAprovada = true;

        var relogio = new FakeClock();
        var produto = new Produto { Id = 2, CategoriaId = 1, Nome = "Água", PrecoCentavos = 300, Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow };
        var outraComanda = comandas.AbrirComanda(20, relogio.UtcNow);
        comandas.AdicionarItem(outraComanda.Id, produto, 1, relogio.UtcNow);

        viewModel.Carregar(outraComanda.Id);

        Assert.False(viewModel.CobrancaAprovada);
        Assert.Equal(string.Empty, viewModel.Mensagem);
    }

    [Fact]
    [Trait("Caso", "CT09")]
    public void Voltar_DisparaConcluidoFalseSemAlterarComanda()
    {
        var (viewModel, comandas, comandaId) = CriarViewModel();
        bool? resultadoEvento = null;
        viewModel.Concluido += (_, sucesso) => resultadoEvento = sucesso;

        viewModel.VoltarCommand.Execute(null);

        Assert.False(resultadoEvento);
        Assert.Single(comandas.ListarAbertas());
    }
}
