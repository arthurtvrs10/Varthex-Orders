using System.Diagnostics;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Backup;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Desktop.Backup;
using VarthexComanda.Desktop.Catalogo;
using VarthexComanda.Desktop.Configuracao;
using VarthexComanda.Infrastructure.Backup;
using VarthexComanda.Infrastructure.Concurrency;
using VarthexComanda.Infrastructure.Configuracao;
using VarthexComanda.Infrastructure.Logging;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Persistence.Atendimento;
using VarthexComanda.Infrastructure.Persistence.Catalogo;
using VarthexComanda.Infrastructure.Storage;
using VarthexComanda.Infrastructure.Time;

namespace VarthexComanda.Desktop;

public partial class App : System.Windows.Application
{
    private SingleInstanceGuard? _guard;
    private ILogger? _logger;
    private ServiceProvider? _serviceProvider;
    private bool _startupConcluido;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _guard = new SingleInstanceGuard("VarthexComanda.SingleInstance");
        if (!_guard.TryAcquire())
        {
            MessageBox.Show(
                "O Varthex Comanda já está aberto neste computador.",
                "Varthex Comanda",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var raizDados = Environment.GetEnvironmentVariable("VARTHEX_COMANDA_DADOS");
        var paths = new AppPaths(string.IsNullOrWhiteSpace(raizDados) ? null : raizDados);
        paths.EnsureCreated();
        VarthexComanda.Desktop.Catalogo.FotoArquivoParaImagemConverter.DiretorioFotos = paths.FotosDirectory;

        _logger = LoggingConfigurator.CreateLogger(paths.LogsDirectory);
        _logger.Information("Iniciando Varthex Comanda");

        DispatcherUnhandledException += (sender, args) =>
        {
            _logger?.Error(args.Exception, "Erro nao tratado na interface");
            MessageBox.Show(
                "Ocorreu um erro inesperado no Varthex Comanda. Consulte os logs.",
                "Varthex Comanda",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        var services = new ServiceCollection();
        services.AddSingleton(paths);
        services.AddSingleton(_logger);
        services.AddSingleton<IClock, SystemClock>();
        services.AddDbContextFactory<VarthexComandaDbContext>(options =>
            options.UseSqlite($"Data Source={paths.DatabasePath};Foreign Keys=True"));
        services.AddTransient<ICategoriaRepository, EfCategoriaRepository>();
        services.AddTransient<IProdutoRepository, EfProdutoRepository>();
        services.AddTransient<CadastrarCategoria>();
        services.AddTransient<AlterarCategoria>();
        services.AddTransient<ListarCategoriasAtivas>();
        services.AddTransient<CadastrarProduto>();
        services.AddTransient<AlterarProduto>();
        services.AddTransient<DesativarProduto>();
        services.AddTransient<PesquisarProdutos>();
        services.AddSingleton<IFotoStorage>(sp => new ArquivoFotoStorage(
            sp.GetRequiredService<AppPaths>(),
            sp.GetRequiredService<ILogger>()));
        services.AddTransient<DefinirFotoProduto>();
        services.AddTransient<RemoverFotoProduto>();
        services.AddTransient<IComandaRepository, EfComandaRepository>();
        services.AddTransient<AbrirComanda>();
        services.AddTransient<AdicionarItem>();
        services.AddTransient<AlterarQuantidade>();
        services.AddTransient<RemoverItem>();
        services.AddTransient<CancelarComanda>();
        services.AddTransient<IConfirmador, JanelaConfirmador>();
        services.AddTransient<EncerrarComanda>();
        services.AddTransient<EncerramentoViewModel>();
        services.AddTransient<Func<EncerramentoViewModel>>(sp => () => sp.GetRequiredService<EncerramentoViewModel>());
        services.AddTransient<IEncerramentoDialog, EncerramentoDialog>();
        services.AddTransient<IVendaRepository, EfVendaRepository>();
        services.AddTransient<ListarVendasPorData>();
        services.AddTransient<BuscarItensDaVenda>();
        services.AddTransient<HistoricoViewModel>();
        services.AddTransient<HistoricoView>();
        services.AddTransient<IBackupRegistroRepository, EfBackupRegistroRepository>();
        services.AddTransient<IBackupService, EfBackupService>();
        services.AddTransient<CriarBackupAutomatico>();
        services.AddTransient<CriarBackupManual>();
        services.AddTransient<ValidarBackup>();
        services.AddTransient<RestaurarBackup>();
        services.AddTransient<ListarBackupsRecentes>();
        services.AddTransient<BackupViewModel>();
        services.AddTransient<BackupView>();
        services.AddTransient<IConfiguracaoRepository, EfConfiguracaoRepository>();
        services.AddTransient<ObterConfiguracao>();
        services.AddTransient<SalvarConfiguracao>();
        services.AddTransient<ConfiguracaoViewModel>();
        services.AddTransient<ConfiguracaoView>();
        services.AddTransient<AtendimentoViewModel>();
        services.AddTransient<AtendimentoView>();
        services.AddTransient<ProdutosViewModel>();
        services.AddTransient<ProdutosView>();
        services.AddTransient<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        var bancoCorrompido = false;
        try
        {
            var factory = _serviceProvider.GetRequiredService<IDbContextFactory<VarthexComandaDbContext>>();
            using var dbContext = factory.CreateDbContext();

            // A integridade é verificada ANTES de migrar: nunca aplicamos migrações (escritas) num
            // arquivo corrompido, e um arquivo que nem é SQLite também cai aqui (o PRAGMA lança
            // SQLITE_NOTADB/SQLITE_CORRUPT), em vez de virar "falha ao preparar o banco".
            var linhas = dbContext.Database
                .SqlQueryRaw<string>("PRAGMA integrity_check")
                .AsEnumerable()
                .ToList();
            if (linhas.Count != 1 || linhas[0] != "ok")
            {
                _logger.Error("PRAGMA integrity_check retornou {Linhas}", string.Join("; ", linhas));
                bancoCorrompido = true;
            }
            else
            {
                var pendentes = dbContext.Database.GetPendingMigrations().ToList();
                if (pendentes.Count > 0)
                {
                    var backupService = _serviceProvider.GetRequiredService<IBackupService>();
                    var resultadoPreventivo = backupService.CriarBackupGerenciado();
                    if (resultadoPreventivo.Sucesso)
                    {
                        _logger.Information("Backup preventivo criado antes de aplicar {Quantidade} migração(ões) pendente(s)", pendentes.Count);
                    }
                    else
                    {
                        _logger.Warning("Backup preventivo antes da migração falhou: {Mensagem}", string.Join(" ", resultadoPreventivo.Erros));
                    }
                }

                dbContext.Database.Migrate();

                _logger.Information("Banco pronto");

                var abertas = _serviceProvider.GetRequiredService<IComandaRepository>().ListarAbertas().Count;
                _logger.Information("Comandas abertas recuperadas: {Quantidade}", abertas);

                _serviceProvider.GetRequiredService<CriarBackupAutomatico>().Executar();

                _serviceProvider.GetRequiredService<MainWindow>().Show();
                _startupConcluido = true;
            }
        }
        catch (Exception ex) when (CorrupcaoDeBanco.EhErroDeCorrupcao(ex))
        {
            _logger.Error(ex, "Banco de dados corrompido ou inválido");
            bancoCorrompido = true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Falha ao preparar o banco de dados");
            MessageBox.Show(
                "Não foi possível preparar o banco de dados do Varthex Comanda. Consulte os logs.",
                "Varthex Comanda",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        if (bancoCorrompido)
        {
            AbrirModoRestauracao();
        }
    }

    // Banco corrompido: abre a janela só com a aba Backup habilitada e a faixa de aviso.
    // Nada de backup automático ao sair (_startupConcluido continua false) e nada de Shutdown:
    // a restauração é sempre uma ação explícita do operador (BackupViewModel confirma e reinicia).
    private void AbrirModoRestauracao()
    {
        try
        {
            _startupConcluido = false;
            var janela = _serviceProvider!.GetRequiredService<MainWindow>();
            janela.EntrarModoRestauracao();
            janela.Show();
        }
        catch (Exception ex)
        {
            _logger!.Error(ex, "Falha em {Operacao}", "AbrirModoRestauracao");
            MessageBox.Show(
                "O banco de dados está corrompido e não foi possível abrir a tela de restauração. Consulte os logs.",
                "Varthex Comanda",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_startupConcluido)
        {
            _serviceProvider?.GetRequiredService<CriarBackupAutomatico>().Executar(incondicional: true);
        }
        _logger?.Information("Encerrando Varthex Comanda");
        _serviceProvider?.Dispose();
        (_logger as IDisposable)?.Dispose();
        _guard?.Dispose();
        base.OnExit(e);
    }

    public void ReiniciarAplicativo()
    {
        _guard?.Release();
        _guard?.Dispose();
        _serviceProvider?.Dispose();

        // o logger so e descartado depois da tentativa, para registrar uma falha ao reiniciar
        var reiniciou = false;
        try
        {
            var caminhoExecutavel = Environment.ProcessPath
                ?? throw new InvalidOperationException("Não foi possível determinar o caminho do executável.");
            Process.Start(caminhoExecutavel);
            reiniciou = true;
        }
        catch (Exception ex)
        {
            _logger?.Warning(ex, "Falha em {Operacao}", "ReiniciarAplicativo");
            (_logger as IDisposable)?.Dispose();
            MessageBox.Show(
                "A restauração foi concluída, mas não foi possível reiniciar automaticamente. Feche e abra o Varthex Comanda manualmente.",
                "Varthex Comanda",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        if (reiniciou)
        {
            (_logger as IDisposable)?.Dispose();
        }

        Environment.Exit(0);
    }
}
