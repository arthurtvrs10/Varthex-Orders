using System.Windows;
using System.Windows.Controls;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Backup;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Application.Tests.Atendimento;
using VarthexComanda.Application.Tests.Backup;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Application.Tests.Configuracao;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Desktop.Backup;
using VarthexComanda.Desktop.Catalogo;
using VarthexComanda.Desktop.Configuracao;
using VarthexComanda.Desktop.Tests.Atendimento;
using VarthexComanda.Desktop.Tests.Backup;
using VarthexComanda.Domain;
using Xunit;
using WpfApp = System.Windows.Application;

namespace VarthexComanda.Desktop.Tests.Xaml;

/// <summary>
/// Rede de seguranca contra XAML invalido: a UI nao pode ser aberta na verificacao, e um recurso
/// ausente ({StaticResource}), estilo ou converter inexistente so estouraria em runtime. Estes testes
/// constroem cada view com ViewModels reais sobre dobles em memoria e fazem Measure/Arrange.
/// (RNF18: o foco visivel vem de App.xaml, carregado aqui como o app o carrega.)
///
/// Carga do App.xaml: em processo de teste nao ha instancia de App. <c>new App()</c> mais
/// <c>InitializeComponent()</c> (gerado pelo compilador de XAML, publico) carrega o BAML real de
/// App.xaml na propria instancia e a torna <c>Application.Current</c> - sem Run(), portanto sem
/// OnStartup (nem banco, nem single instance, nem log). Exatamente um App por processo.
/// </summary>
public class CarregamentoDeXamlTests
{
    private static readonly Size Tela = new(1200, 700);
    private static readonly object TravaApp = new();
    private static App? _app;

    // Sempre chamado de dentro de EmSta (o App fica preso a thread STA dedicada)
    internal static void GarantirApp()
    {
        lock (TravaApp)
        {
            if (_app is not null)
            {
                return;
            }

            _app = WpfApp.Current as App ?? new App();
            _app.InitializeComponent();
        }
    }

    internal static void MedirEOrganizar(FrameworkElement elemento)
    {
        elemento.Measure(Tela);
        elemento.Arrange(new Rect(new Point(0, 0), Tela));
        elemento.UpdateLayout();
        Assert.True(elemento.ActualWidth > 0, $"{elemento.GetType().Name} ficou sem largura apos o layout.");
        Assert.True(elemento.ActualHeight > 0, $"{elemento.GetType().Name} ficou sem altura apos o layout.");
    }

    // ---- dobles cheios o bastante para os DataTemplates serem instanciados ----

    internal static (AtendimentoViewModel ViewModel, Comanda Comanda) CriarAtendimento()
    {
        var relogio = new FakeClock();
        var comandas = new FakeComandaRepository();
        var categorias = new FakeCategoriaRepository();
        var produtos = new FakeProdutoRepository();
        var configuracoes = new FakeConfiguracaoRepository();

        var categoria = categorias.Salvar(new Categoria { Id = 0, Nome = "Bebidas", Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow });
        var produto = produtos.Salvar(new Produto { Id = 0, CategoriaId = categoria.Id, Nome = "Refrigerante", PrecoCentavos = 500, Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow });
        var comanda = comandas.AbrirComanda(10, relogio.UtcNow);
        comandas.AdicionarItem(comanda.Id, produto, 2, relogio.UtcNow);

        var viewModel = new AtendimentoViewModel(
            new AbrirComanda(comandas, relogio, new ObterConfiguracao(configuracoes)),
            new AdicionarItem(comandas, produtos, relogio),
            new AlterarQuantidade(comandas, relogio),
            new RemoverItem(comandas, relogio),
            new CancelarComanda(comandas, relogio),
            comandas,
            new ListarCategoriasAtivas(categorias),
            new PesquisarProdutos(produtos),
            new FakeConfirmador(),
            new FakeEncerramentoDialog(comandas, relogio),
            new ObterConfiguracao(configuracoes),
            relogio);

        // preenche a comanda em edicao (painel de itens) alem de slots, categorias e catalogo
        viewModel.SelecionarComandaCommand.Execute(comanda);
        return (viewModel, comanda);
    }

    private static ProdutosViewModel CriarProdutos()
    {
        var relogio = new FakeClock();
        var categorias = new FakeCategoriaRepository();
        var produtos = new FakeProdutoRepository();
        var fotos = new FakeFotoStorage();

        var categoria = categorias.Salvar(new Categoria { Id = 0, Nome = "Bebidas", Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow });
        produtos.Salvar(new Produto { Id = 0, CategoriaId = categoria.Id, Nome = "Refrigerante", PrecoCentavos = 500, Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow });

        return new ProdutosViewModel(
            new ListarCategoriasAtivas(categorias),
            new CadastrarCategoria(categorias, relogio),
            new PesquisarProdutos(produtos),
            new CadastrarProduto(produtos, categorias, relogio),
            new AlterarProduto(produtos, categorias, relogio),
            new DesativarProduto(produtos, relogio),
            new DefinirFotoProduto(produtos, fotos, relogio),
            new RemoverFotoProduto(produtos, fotos, relogio));
    }

    private static HistoricoViewModel CriarHistorico()
    {
        var relogio = new FakeClock();
        var vendas = new FakeVendaRepository();
        vendas.AdicionarVenda(
            new Venda { Id = 1, ComandaId = 1, Numero = 1, TotalCentavos = 1000, FinalizadaEm = relogio.UtcNow, Status = StatusVenda.Concluida },
            10,
            new List<ItemComanda>());
        return new HistoricoViewModel(new ListarVendasPorData(vendas), new BuscarItensDaVenda(vendas), relogio);
    }

    private static BackupViewModel CriarBackup()
    {
        var backupService = new FakeBackupService();
        var registros = new FakeBackupRegistroRepository();
        registros.Registrar(new BackupRegistro
        {
            Id = 0,
            Arquivo = "varthex-comanda-2026-09-17-080000.db",
            Destino = "C:\\backups",
            CriadoEm = new DateTime(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc),
            Status = StatusBackup.Sucesso,
            Checksum = "xyz",
            Mensagem = null
        });
        return new BackupViewModel(
            new CriarBackupManual(backupService),
            new RestaurarBackup(backupService),
            new ListarBackupsRecentes(registros),
            new FakeConfirmadorDeBackup());
    }

    private static ConfiguracaoViewModel CriarConfiguracao()
    {
        var repositorio = new FakeConfiguracaoRepository();
        return new ConfiguracaoViewModel(new ObterConfiguracao(repositorio), new SalvarConfiguracao(repositorio, new FakeClock()));
    }

    private static MainWindow CriarMainWindow()
    {
        var (atendimento, _) = CriarAtendimento();
        return new MainWindow(
            new AtendimentoView(atendimento),
            new ProdutosView(CriarProdutos()),
            new HistoricoView(CriarHistorico()),
            new BackupView(CriarBackup()),
            new ConfiguracaoView(CriarConfiguracao()));
    }

    // ---- testes ----

    [Fact]
    [Trait("Requisito", "RNF18")]
    public void AppXaml_CarregaEExpoeEstiloFocoVisivelResolvidoPeloButton()
    {
        ThreadingHelper.EmSta(() =>
        {
            GarantirApp();

            var estilo = Assert.IsType<Style>(WpfApp.Current.Resources["FocoVisivel"]);
            Assert.Equal(typeof(Control), estilo.TargetType);

            var botao = new Button();
            MedirEOrganizar(new Grid { Width = 200, Height = 100, Children = { botao } });
            Assert.Same(estilo, botao.FocusVisualStyle);

            // os estilos implicitos de toque de App.xaml continuam valendo junto do foco
            Assert.Equal(46, botao.MinHeight);
        });
    }

    [Fact]
    [Trait("Requisito", "RNF18")]
    public void AtendimentoView_ComComandaAberta_CarregaEMedeSemExcecao()
    {
        ThreadingHelper.EmSta(() =>
        {
            GarantirApp();
            var (viewModel, _) = CriarAtendimento();

            var view = new AtendimentoView(viewModel);

            Assert.Same(viewModel, view.DataContext);
            Assert.NotNull(view.Content);
            MedirEOrganizar(view);
        });
    }

    [Fact]
    [Trait("Requisito", "RNF18")]
    public void ProdutosView_CarregaEMedeSemExcecao()
    {
        ThreadingHelper.EmSta(() =>
        {
            GarantirApp();
            var viewModel = CriarProdutos();

            var view = new ProdutosView(viewModel);

            Assert.Same(viewModel, view.DataContext);
            Assert.NotNull(view.Content);
            MedirEOrganizar(view);
        });
    }

    [Fact]
    [Trait("Requisito", "RNF18")]
    public void HistoricoView_CarregaEMedeSemExcecao()
    {
        ThreadingHelper.EmSta(() =>
        {
            GarantirApp();
            var viewModel = CriarHistorico();

            var view = new HistoricoView(viewModel);

            Assert.Same(viewModel, view.DataContext);
            Assert.NotNull(view.Content);
            MedirEOrganizar(view);
        });
    }

    [Fact]
    [Trait("Requisito", "RNF18")]
    public void BackupView_CarregaEMedeSemExcecao()
    {
        ThreadingHelper.EmSta(() =>
        {
            GarantirApp();
            var viewModel = CriarBackup();

            var view = new BackupView(viewModel);

            Assert.Same(viewModel, view.DataContext);
            Assert.NotNull(view.Content);
            MedirEOrganizar(view);
        });
    }

    [Fact]
    [Trait("Requisito", "RNF18")]
    public void ConfiguracaoView_CarregaEMedeSemExcecao()
    {
        ThreadingHelper.EmSta(() =>
        {
            GarantirApp();
            var viewModel = CriarConfiguracao();

            var view = new ConfiguracaoView(viewModel);

            Assert.Same(viewModel, view.DataContext);
            Assert.NotNull(view.Content);
            MedirEOrganizar(view);
        });
    }

    [Fact]
    [Trait("Requisito", "RNF18")]
    public void EncerramentoView_SemExibirJanela_CarregaEMedeSemExcecao()
    {
        ThreadingHelper.EmSta(() =>
        {
            GarantirApp();
            var relogio = new FakeClock();
            var comandas = new FakeComandaRepository();
            var produtos = new FakeProdutoRepository();
            var produto = produtos.Salvar(new Produto { Id = 0, CategoriaId = 1, Nome = "Refrigerante", PrecoCentavos = 500, Ativo = true, CriadoEm = relogio.UtcNow, AtualizadoEm = relogio.UtcNow });
            var comanda = comandas.AbrirComanda(10, relogio.UtcNow);
            comandas.AdicionarItem(comanda.Id, produto, 2, relogio.UtcNow);
            var viewModel = new EncerramentoViewModel(new EncerrarComanda(comandas, relogio), comandas);
            viewModel.Carregar(comanda.Id);

            // Window: nunca Show()/ShowDialog(); Measure/Arrange no conteudo
            var janela = new EncerramentoView(viewModel);

            Assert.Same(viewModel, janela.DataContext);
            var conteudo = Assert.IsAssignableFrom<FrameworkElement>(janela.Content);
            MedirEOrganizar(conteudo);
            Assert.False(janela.IsVisible);
        });
    }

    [Fact]
    [Trait("Requisito", "RNF18")]
    public void ConfirmacaoView_SemExibirJanela_CarregaEMedeSemExcecao()
    {
        ThreadingHelper.EmSta(() =>
        {
            GarantirApp();

            var janela = new ConfirmacaoView("Cancelar comanda", "Deseja cancelar a comanda?");

            Assert.Equal("Cancelar comanda", janela.Title);
            var conteudo = Assert.IsAssignableFrom<FrameworkElement>(janela.Content);
            MedirEOrganizar(conteudo);
            Assert.False(janela.IsVisible);
        });
    }

    [Fact]
    [Trait("Requisito", "RNF18")]
    public void MainWindow_ComAsCincoViews_CarregaEMedeSemExcecao()
    {
        ThreadingHelper.EmSta(() =>
        {
            GarantirApp();

            var janela = CriarMainWindow();

            var conteudo = Assert.IsAssignableFrom<FrameworkElement>(janela.Content);
            Assert.NotNull(janela.FindName("ConteudoPrincipal"));
            MedirEOrganizar(conteudo);
            Assert.False(janela.IsVisible);
        });
    }

    [Fact]
    [Trait("Requisito", "RNF18")]
    public void MainWindow_EmModoRestauracao_CarregaEMedeSemExcecao()
    {
        ThreadingHelper.EmSta(() =>
        {
            GarantirApp();
            var janela = CriarMainWindow();

            janela.EntrarModoRestauracao();

            var conteudo = Assert.IsAssignableFrom<FrameworkElement>(janela.Content);
            MedirEOrganizar(conteudo);
            Assert.False(janela.IsVisible);
        });
    }
}
