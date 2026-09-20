using VarthexComanda.Application.Backup;
using VarthexComanda.Application.Tests.Backup;
using VarthexComanda.Desktop.Backup;
using VarthexComanda.Desktop.Tests.Suporte;
using VarthexComanda.Infrastructure.Storage;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Backup;

// Etapa 7, ruling 4: no modo de restauracao (banco corrompido) o BackupViewModel nao cria backups
// e lista os arquivos da pasta de backups, ja que o registro de backups vive no banco corrompido.
// MainWindow.EntrarModoRestauracao apenas liga ModoRestauracao e chama AtualizarLista.
public sealed class BackupViewModelModoRestauracaoTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), $"varthex-modo-restauracao-{Guid.NewGuid()}");
    private readonly AppPaths _paths;

    public BackupViewModelModoRestauracaoTests()
    {
        _paths = new AppPaths(_raiz);
        _paths.EnsureCreated();
    }

    public void Dispose()
    {
        if (Directory.Exists(_raiz)) Directory.Delete(_raiz, recursive: true);
    }

    private (BackupViewModel ViewModel, FakeBackupService Servico, FakeConfirmadorDeBackup Confirmador) CriarViewModel(bool confirmar = true)
    {
        var servico = new FakeBackupService();
        var confirmador = new FakeConfirmadorDeBackup { ProximaResposta = confirmar };
        var viewModel = new BackupViewModel(
            new CriarBackupManual(servico),
            new RestaurarBackup(servico),
            new ListarBackupsRecentes(new BackupRegistroRepositoryDeBancoCorrompido()),
            confirmador,
            null,
            _paths);
        return (viewModel, servico, confirmador);
    }

    private void CriarArquivo(string nome, string conteudo = "x") =>
        File.WriteAllText(Path.Combine(_paths.BackupsDirectory, nome), conteudo);

    [Fact]
    public void ModoRestauracao_DesabilitaCriarBackupEPodeCriarBackup()
    {
        var (viewModel, servico, _) = CriarViewModel();
        Assert.True(viewModel.PodeCriarBackup);
        Assert.True(viewModel.CriarBackupCommand.CanExecute(null));

        viewModel.ModoRestauracao = true;

        Assert.False(viewModel.PodeCriarBackup);
        Assert.False(viewModel.CriarBackupCommand.CanExecute(null));
        Assert.False(viewModel.CriarBackupCommand.CanExecute("C:\\externa"));
        // mesmo executando direto, nada e criado
        viewModel.CriarBackupCommand.Execute(null);
        Assert.Equal(0, servico.ChamadasCriarBackupGerenciado);
        Assert.Equal(0, servico.ChamadasCriarBackupExterno);
    }

    [Fact]
    public void ModoRestauracao_DesligadoDeNovo_HabilitaCriarBackup()
    {
        var (viewModel, _, _) = CriarViewModel();
        viewModel.ModoRestauracao = true;

        viewModel.ModoRestauracao = false;

        Assert.True(viewModel.PodeCriarBackup);
        Assert.True(viewModel.CriarBackupCommand.CanExecute(null));
    }

    [Fact]
    public void ModoRestauracao_ListaArquivosDaPastaDeBackupsMaisRecentePrimeiro()
    {
        CriarArquivo("varthex-comanda-2026-09-10-080000.db");
        CriarArquivo("varthex-comanda-2026-09-12-090000.db", "b");
        CriarArquivo("varthex-comanda-2026-09-12-090000.db.sha256", "abc123\n");
        CriarArquivo("varthex-comanda-2026-09-11-100000.db");
        CriarArquivo("corrompido-2026-09-13-000000.db.bak");
        CriarArquivo("anotacoes.txt");
        var (viewModel, _, _) = CriarViewModel();

        viewModel.ModoRestauracao = true;
        viewModel.AtualizarLista();

        Assert.Equal(
            new[]
            {
                "varthex-comanda-2026-09-12-090000.db",
                "varthex-comanda-2026-09-11-100000.db",
                "varthex-comanda-2026-09-10-080000.db"
            },
            viewModel.Backups.Select(b => b.Arquivo).ToArray());
        Assert.All(viewModel.Backups, b => Assert.Equal(_paths.BackupsDirectory, b.Destino));
        Assert.Equal("abc123", viewModel.Backups[0].Checksum);
        Assert.Null(viewModel.Backups[1].Checksum);
        Assert.Equal(string.Empty, viewModel.Mensagem);
    }

    [Fact]
    public void ModoRestauracao_SemBackupsNaPasta_OrientaSelecionarArquivo()
    {
        var (viewModel, _, _) = CriarViewModel();

        viewModel.ModoRestauracao = true;
        viewModel.AtualizarLista();

        Assert.Empty(viewModel.Backups);
        Assert.Contains("Selecionar arquivo", viewModel.Mensagem);
    }

    [Fact]
    public void ModoRestauracao_SelecionarEConfirmar_RestauraEPedeReinicio()
    {
        CriarArquivo("varthex-comanda-2026-09-12-090000.db");
        var (viewModel, servico, _) = CriarViewModel(confirmar: true);
        viewModel.ModoRestauracao = true;
        viewModel.AtualizarLista();
        viewModel.BackupSelecionado = viewModel.Backups[0];
        var reinicio = false;
        viewModel.SolicitouReinicio += (_, _) => reinicio = true;

        viewModel.RestaurarCommand.Execute(null);

        Assert.True(reinicio);
        Assert.Equal(1, servico.ChamadasRestaurarPara);
    }

    [Fact]
    public void ModoRestauracao_ConfirmadorRecusa_NaoRestaura()
    {
        CriarArquivo("varthex-comanda-2026-09-12-090000.db");
        var (viewModel, servico, _) = CriarViewModel(confirmar: false);
        viewModel.ModoRestauracao = true;
        viewModel.AtualizarLista();
        viewModel.BackupSelecionado = viewModel.Backups[0];

        viewModel.RestaurarCommand.Execute(null);

        Assert.Equal(0, servico.ChamadasRestaurarPara);
    }

    [Fact]
    public void ModoRestauracao_ArquivoNaoValidado_MostraMotivoSemRestaurar()
    {
        CriarArquivo("varthex-comanda-2026-09-12-090000.db");
        var (viewModel, servico, _) = CriarViewModel(confirmar: true);
        servico.ProximoRelatorio = new RelatorioValidacao
        {
            FormatoValido = true,
            VersaoCompativel = true,
            IntegridadeOk = false,
            ChecksumConfere = null,
            Motivo = "Arquivo de backup está corrompido."
        };
        viewModel.ModoRestauracao = true;
        viewModel.AtualizarLista();
        viewModel.BackupSelecionado = viewModel.Backups[0];

        viewModel.RestaurarCommand.Execute(null);

        Assert.Equal(0, servico.ChamadasRestaurarPara);
        Assert.Contains("corrompido", viewModel.Mensagem);
    }
}
