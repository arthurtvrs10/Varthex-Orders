using Serilog.Events;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Backup;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Application.Tests.Atendimento;
using VarthexComanda.Application.Tests.Backup;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Application.Tests.Suporte;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Desktop.Backup;
using VarthexComanda.Desktop.Catalogo;
using VarthexComanda.Desktop.Configuracao;
using VarthexComanda.Desktop.Tests.Atendimento;
using VarthexComanda.Desktop.Tests.Backup;
using VarthexComanda.Desktop.Tests.Suporte;
using Xunit;

namespace VarthexComanda.Desktop.Tests;

// Etapa 7, ruling 4: com o banco corrompido o app abre em modo de restauracao, entao os
// ViewModels resolvidos junto com a MainWindow nao podem lancar no construtor.
public class ConstrucaoComBancoCorrompidoTests
{
    private static AtendimentoViewModel CriarAtendimento(Serilog.ILogger? logger)
    {
        var comandas = new ComandaRepositoryDeBancoCorrompido();
        var categorias = new CategoriaRepositoryDeBancoCorrompido();
        var produtos = new ProdutoRepositoryDeBancoCorrompido();
        var configuracoes = new ConfiguracaoRepositoryDeBancoCorrompido();
        var relogio = new FakeClock();

        return new AtendimentoViewModel(
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
            relogio,
            logger);
    }

    [Fact]
    public void AtendimentoViewModel_ComBancoCorrompido_ConstroiComListasVaziasERegistraNoLog()
    {
        var coletor = new ColetorDeLog();

        var viewModel = CriarAtendimento(coletor.Logger);

        Assert.Empty(viewModel.ComandasAbertas);
        Assert.Empty(viewModel.Slots);
        Assert.Empty(viewModel.Categorias);
        Assert.Empty(viewModel.ProdutosCatalogo);
        Assert.Equal(2, coletor.Eventos.Count);
        Assert.All(coletor.Eventos, e =>
        {
            Assert.Equal(LogEventLevel.Error, e.Level);
            Assert.NotNull(e.Exception);
        });
    }

    [Fact]
    public void AtendimentoViewModel_ComBancoCorrompidoESemLogger_TambemConstroi()
    {
        var viewModel = CriarAtendimento(null);

        Assert.Empty(viewModel.ComandasAbertas);
    }

    [Fact]
    public void ProdutosViewModel_ComBancoCorrompido_ConstroiComListasVaziasERegistraNoLog()
    {
        var coletor = new ColetorDeLog();
        var categorias = new CategoriaRepositoryDeBancoCorrompido();
        var produtos = new ProdutoRepositoryDeBancoCorrompido();
        var relogio = new FakeClock();
        var fotos = new FakeFotoStorage();

        var viewModel = new ProdutosViewModel(
            new ListarCategoriasAtivas(categorias),
            new CadastrarCategoria(categorias, relogio),
            new PesquisarProdutos(produtos),
            new CadastrarProduto(produtos, categorias, relogio),
            new AlterarProduto(produtos, categorias, relogio),
            new DesativarProduto(produtos, relogio),
            new DefinirFotoProduto(produtos, fotos, relogio),
            new RemoverFotoProduto(produtos, fotos, relogio),
            coletor.Logger);

        Assert.Empty(viewModel.Categorias);
        Assert.Empty(viewModel.Produtos);
        var evento = Assert.Single(coletor.Eventos);
        Assert.Equal(LogEventLevel.Error, evento.Level);
        Assert.NotNull(evento.Exception);
    }

    [Fact]
    public void ConfiguracaoViewModel_ComBancoCorrompido_ConstroiERegistraNoLog()
    {
        var coletor = new ColetorDeLog();
        var configuracoes = new ConfiguracaoRepositoryDeBancoCorrompido();

        var viewModel = new ConfiguracaoViewModel(
            new ObterConfiguracao(configuracoes),
            new SalvarConfiguracao(configuracoes, new FakeClock()),
            coletor.Logger);

        Assert.Equal(string.Empty, viewModel.QuantidadeComandasTexto);
        var evento = Assert.Single(coletor.Eventos);
        Assert.Equal(LogEventLevel.Error, evento.Level);
        Assert.NotNull(evento.Exception);
    }

    [Fact]
    public void BackupViewModel_ComRegistroDeBackupsIlegivel_ConstroiComListaVaziaEOrientaSelecionarArquivo()
    {
        var coletor = new ColetorDeLog();
        var backupService = new FakeBackupService();

        var viewModel = new BackupViewModel(
            new CriarBackupManual(backupService),
            new RestaurarBackup(backupService),
            new ListarBackupsRecentes(new BackupRegistroRepositoryDeBancoCorrompido()),
            new FakeConfirmadorDeBackup(),
            coletor.Logger);

        Assert.Empty(viewModel.Backups);
        Assert.Contains("Selecionar arquivo", viewModel.Mensagem);
        var evento = Assert.Single(coletor.Eventos);
        Assert.Equal(LogEventLevel.Error, evento.Level);
        Assert.NotNull(evento.Exception);
    }

    [Fact]
    public void BackupViewModel_AtualizarListaComRegistroIlegivel_NaoLanca()
    {
        var backupService = new FakeBackupService();
        var viewModel = new BackupViewModel(
            new CriarBackupManual(backupService),
            new RestaurarBackup(backupService),
            new ListarBackupsRecentes(new BackupRegistroRepositoryDeBancoCorrompido()),
            new FakeConfirmadorDeBackup());

        viewModel.AtualizarLista();

        Assert.Empty(viewModel.Backups);
    }
}
