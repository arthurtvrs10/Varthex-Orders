using System.Windows;
using System.Windows.Controls;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Tests.Atendimento;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Desktop.Catalogo;
using VarthexComanda.Desktop.Configuracao;
using VarthexComanda.Domain;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Xaml;

/// <summary>Dialogos e formularios operaveis por teclado (RNF18 / CT17): Enter, Esc, teclas de acesso e Ctrl+F.</summary>
[Trait("Requisito", "RNF18")]
public class DialogosTecladoXamlTests
{
    private static EncerramentoView CriarEncerramento(out EncerramentoViewModel viewModel, out FakeComandaRepository comandas)
    {
        var relogio = new FakeClock();
        comandas = new FakeComandaRepository();
        var produto = new Produto { Id = 1, CategoriaId = 1, Nome = "Refrigerante", PrecoCentavos = 500, Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow };
        var comanda = comandas.AbrirComanda(10, relogio.UtcNow);
        comandas.AdicionarItem(comanda.Id, produto, 2, relogio.UtcNow);
        viewModel = new EncerramentoViewModel(new EncerrarComanda(comandas, relogio), comandas);
        viewModel.Carregar(comanda.Id);
        return new EncerramentoView(viewModel);
    }

    [Fact]
    [Trait("Caso", "CT17")]
    public void EncerramentoView_EscVoltaEEnterConfirma_ConfirmarSoHabilitadoComCobrancaAprovada()
    {
        ThreadingHelper.EmSta(() =>
        {
            CarregamentoDeXamlTests.GarantirApp();
            var janela = CriarEncerramento(out var viewModel, out var comandas);
            CarregamentoDeXamlTests.MedirEOrganizar((FrameworkElement)janela.Content);

            var voltar = Assert.IsType<Button>(janela.FindName("BotaoVoltar"));
            var confirmar = Assert.IsType<Button>(janela.FindName("BotaoConfirmarEncerrar"));
            var caixa = Assert.IsType<CheckBox>(janela.FindName("CaixaCobrancaAprovada"));

            Assert.True(voltar.IsCancel);
            Assert.False(voltar.IsDefault);
            Assert.True(confirmar.IsDefault);
            Assert.False(confirmar.IsCancel);
            Assert.Same(caixa, System.Windows.Input.FocusManager.GetFocusedElement(janela));

            // sem aprovacao: Enter (botao padrao) nao pode encerrar
            Assert.False(confirmar.IsEnabled);
            Assert.False(caixa.IsChecked);
            Assert.Single(comandas.ListarAbertas());

            caixa.IsChecked = true;
            Assert.True(viewModel.CobrancaAprovada);
            Assert.True(confirmar.IsEnabled);

            caixa.IsChecked = false;
            Assert.False(confirmar.IsEnabled);
        });
    }

    [Fact]
    [Trait("Caso", "CT17")]
    public void ConfirmacaoView_TeclasDeAcessoSimNao_NaoEPadraoEECancel()
    {
        ThreadingHelper.EmSta(() =>
        {
            CarregamentoDeXamlTests.GarantirApp();
            var janela = new ConfirmacaoView("Cancelar comanda", "Deseja cancelar a comanda?");

            var sim = Assert.IsType<Button>(janela.FindName("BotaoSim"));
            var nao = Assert.IsType<Button>(janela.FindName("BotaoNao"));

            Assert.Equal("_Sim", sim.Content);
            Assert.Equal("_Não", nao.Content);
            Assert.True(nao.IsDefault);
            Assert.True(nao.IsCancel);
            Assert.False(sim.IsDefault);
            Assert.False(sim.IsCancel);
        });
    }

    [Fact]
    [Trait("Caso", "CT17")]
    public void ProdutosView_EnterGravaPeloBotaoVisivelECtrlFTemCampoDeBusca()
    {
        ThreadingHelper.EmSta(() =>
        {
            CarregamentoDeXamlTests.GarantirApp();
            var viewModel = CarregamentoDeXamlTests.CriarProdutos();
            var view = new ProdutosView(viewModel);
            viewModel.NovoCommand.Execute(null);
            CarregamentoDeXamlTests.MedirEOrganizar(view);

            var cadastrar = Assert.IsType<Button>(view.FindName("BotaoCadastrar"));
            var salvar = Assert.IsType<Button>(view.FindName("BotaoSalvar"));
            Assert.IsType<TextBox>(view.FindName("CampoBuscaProduto"));

            // modo "novo": so o Cadastrar e padrao
            Assert.Equal(Visibility.Visible, cadastrar.Visibility);
            Assert.True(cadastrar.IsDefault);
            Assert.NotEqual(Visibility.Visible, salvar.Visibility);
            Assert.False(salvar.IsDefault);
            Assert.Same(viewModel.SalvarCommand, cadastrar.Command);

            // o estilo herda o de Button do App (foco visivel e tamanho de toque)
            var foco = System.Windows.Application.Current.FindResource("FocoVisivel");
            Assert.Same(foco, cadastrar.FocusVisualStyle);
            Assert.Same(foco, salvar.FocusVisualStyle);
            Assert.True(cadastrar.MinHeight >= 46);

            // modo "edicao": so o Salvar e padrao
            viewModel.ProdutoSelecionado = viewModel.Produtos[0];
            CarregamentoDeXamlTests.MedirEOrganizar(view);
            Assert.Equal(Visibility.Visible, salvar.Visibility);
            Assert.True(salvar.IsDefault);
            Assert.NotEqual(Visibility.Visible, cadastrar.Visibility);
            Assert.False(cadastrar.IsDefault);
            Assert.Same(viewModel.SalvarCommand, salvar.Command);
        });
    }

    [Fact]
    [Trait("Caso", "CT17")]
    public void ConfiguracaoView_SalvarEPadrao()
    {
        ThreadingHelper.EmSta(() =>
        {
            CarregamentoDeXamlTests.GarantirApp();
            var viewModel = CarregamentoDeXamlTests.CriarConfiguracao();
            var view = new ConfiguracaoView(viewModel);
            CarregamentoDeXamlTests.MedirEOrganizar(view);

            var salvar = Assert.IsType<Button>(view.FindName("BotaoSalvar"));

            Assert.True(salvar.IsDefault);
            Assert.Same(viewModel.SalvarCommand, salvar.Command);
        });
    }

    [Fact]
    [Trait("Caso", "CT17")]
    public void HistoricoView_TemCampoDeBuscaParaCtrlF()
    {
        ThreadingHelper.EmSta(() =>
        {
            CarregamentoDeXamlTests.GarantirApp();
            var viewModel = CarregamentoDeXamlTests.CriarHistorico();
            var view = new HistoricoView(viewModel);
            CarregamentoDeXamlTests.MedirEOrganizar(view);

            var busca = Assert.IsType<TextBox>(view.FindName("CampoBuscaNumero"));

            viewModel.TextoBuscaNumero = "12";
            Assert.Equal("12", busca.Text);
        });
    }
}
