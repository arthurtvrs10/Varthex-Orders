using Serilog;
using Serilog.Core;
using Serilog.Events;
using VarthexComanda.Application.Backup;
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Application.Tests.Catalogo;
using VarthexComanda.Application.Tests.Configuracao;
using VarthexComanda.Application.Tests.Suporte;
using Xunit;

namespace VarthexComanda.Application.Tests.Backup;

public class CriarBackupAutomaticoTests
{
    private static readonly ILogger LoggerSemDestino = new LoggerConfiguration().CreateLogger();

    private sealed class LoggerDeCaptura : ILogEventSink
    {
        public List<LogEvent> Eventos { get; } = new();
        public void Emit(LogEvent logEvent) => Eventos.Add(logEvent);
    }

    [Fact]
    public void Executar_JaExisteBackupHoje_NaoCriaNovoBackup()
    {
        var registros = new FakeBackupRegistroRepository { ExisteBackupHojeRetorno = true };
        var backupService = new FakeBackupService();
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository()), LoggerSemDestino);

        caso.Executar();

        Assert.Equal(0, backupService.ChamadasCriarBackupGerenciado);
    }

    [Fact]
    public void Executar_NaoExisteBackupHoje_CriaBackup()
    {
        var registros = new FakeBackupRegistroRepository { ExisteBackupHojeRetorno = false };
        var backupService = new FakeBackupService();
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository()), LoggerSemDestino);

        caso.Executar();

        Assert.Equal(1, backupService.ChamadasCriarBackupGerenciado);
    }

    [Fact]
    public void Executar_Incondicional_CriaBackupMesmoSeJaExisteHoje()
    {
        var registros = new FakeBackupRegistroRepository { ExisteBackupHojeRetorno = true };
        var backupService = new FakeBackupService();
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository()), LoggerSemDestino);

        caso.Executar(incondicional: true);

        Assert.Equal(1, backupService.ChamadasCriarBackupGerenciado);
    }

    [Fact]
    public void Executar_BackupServiceLancaExcecao_NaoPropagaExcecao()
    {
        var registros = new FakeBackupRegistroRepository { ExisteBackupHojeRetorno = false };
        var backupService = new FakeBackupService { LancarExcecaoAoCriar = true };
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository()), LoggerSemDestino);

        var excecao = Record.Exception(() => caso.Executar());

        Assert.Null(excecao);
    }

    [Fact]
    public void Executar_IncondicionalComPastaExternaConfigurada_TentaCopiaExterna()
    {
        var registros = new FakeBackupRegistroRepository();
        var backupService = new FakeBackupService();
        var configuracoes = new FakeConfiguracaoRepository();
        configuracoes.Definir("backup.pasta_externa", "D:\\backups", DateTime.UtcNow);
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock(), new ObterConfiguracao(configuracoes), LoggerSemDestino);

        caso.Executar(incondicional: true);

        Assert.Equal(1, backupService.ChamadasCriarBackupExterno);
    }

    [Fact]
    public void Executar_IncondicionalSemPastaExternaConfigurada_NaoTentaCopiaExterna()
    {
        var registros = new FakeBackupRegistroRepository();
        var backupService = new FakeBackupService();
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository()), LoggerSemDestino);

        caso.Executar(incondicional: true);

        Assert.Equal(0, backupService.ChamadasCriarBackupExterno);
    }

    [Fact]
    public void Executar_AberturaDoDiaComPastaExternaConfigurada_NuncaTentaCopiaExterna()
    {
        var registros = new FakeBackupRegistroRepository { ExisteBackupHojeRetorno = false };
        var backupService = new FakeBackupService();
        var configuracoes = new FakeConfiguracaoRepository();
        configuracoes.Definir("backup.pasta_externa", "D:\\backups", DateTime.UtcNow);
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock(), new ObterConfiguracao(configuracoes), LoggerSemDestino);

        caso.Executar();

        Assert.Equal(0, backupService.ChamadasCriarBackupExterno);
    }

    [Fact]
    public void Executar_IncondicionalComFalhaNaCopiaExterna_RegistraAvisoNoLog()
    {
        var registros = new FakeBackupRegistroRepository();
        var backupService = new FakeBackupService { ProximaCriacaoExternaFalha = true };
        var configuracoes = new FakeConfiguracaoRepository();
        configuracoes.Definir("backup.pasta_externa", "D:\\backups", DateTime.UtcNow);
        var captura = new LoggerDeCaptura();
        var logger = new LoggerConfiguration().WriteTo.Sink(captura).CreateLogger();
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock(), new ObterConfiguracao(configuracoes), logger);

        caso.Executar(incondicional: true);

        var evento = Assert.Single(captura.Eventos);
        Assert.Equal(LogEventLevel.Warning, evento.Level);
        // o caso de uso so repassa a mensagem do servico e nao acrescenta a pasta como propriedade;
        // a mascara do caminho dentro de mensagens de erro do servico e testada de ponta a ponta em
        // Infrastructure.Tests (PastasMascaradasTests.BackupAutomaticoComFalhaNaPastaExterna_...)
        Assert.DoesNotContain("Pasta", evento.Properties.Keys);
        Assert.DoesNotContain("PastaBackupExterna", evento.Properties.Keys);
        Assert.Contains("Falha simulada ao criar backup externo.", evento.RenderMessage());
    }

    // RNF16: falha tecnica do backup automatico nao interrompe o app, mas fica no log como Warning
    [Fact]
    public void Executar_BackupServiceLancaExcecao_RegistraAvisoComExcecaoSemPropagar()
    {
        var registros = new FakeBackupRegistroRepository { ExisteBackupHojeRetorno = false };
        var backupService = new FakeBackupService { LancarExcecaoAoCriar = true };
        var coletor = new ColetorDeLog();
        var caso = new CriarBackupAutomatico(backupService, registros, new FakeClock(), new ObterConfiguracao(new FakeConfiguracaoRepository()), coletor.Logger);

        var excecao = Record.Exception(() => caso.Executar());

        Assert.Null(excecao);
        var evento = Assert.Single(coletor.Eventos);
        Assert.Equal(LogEventLevel.Warning, evento.Level);
        Assert.IsType<InvalidOperationException>(evento.Exception);
        Assert.Equal("Backup automatico falhou", evento.RenderMessage());
    }
}
