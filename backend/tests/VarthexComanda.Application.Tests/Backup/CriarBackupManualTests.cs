using Serilog.Events;
using VarthexComanda.Application.Backup;
using VarthexComanda.Application.Tests.Suporte;
using Xunit;

namespace VarthexComanda.Application.Tests.Backup;

public class CriarBackupManualTests
{
    [Fact]
    public void Executar_SemPastaExterna_SoCriaNaGerenciada()
    {
        var backupService = new FakeBackupService();
        var caso = new CriarBackupManual(backupService);

        var resultado = caso.Executar(null);

        Assert.True(resultado.Sucesso);
        Assert.Equal(1, backupService.ChamadasCriarBackupGerenciado);
        Assert.Equal(0, backupService.ChamadasCriarBackupExterno);
    }

    [Fact]
    public void Executar_ComPastaExterna_CriaNasDuas()
    {
        var backupService = new FakeBackupService();
        var caso = new CriarBackupManual(backupService);

        caso.Executar("D:\\pendrive");

        Assert.Equal(1, backupService.ChamadasCriarBackupGerenciado);
        Assert.Equal(1, backupService.ChamadasCriarBackupExterno);
    }

    [Fact]
    public void Executar_GerenciadaFalha_NaoTentaExterna()
    {
        var backupService = new FakeBackupService { ProximaCriacaoFalha = true };
        var caso = new CriarBackupManual(backupService);

        var resultado = caso.Executar("D:\\pendrive");

        Assert.False(resultado.Sucesso);
        Assert.Equal(0, backupService.ChamadasCriarBackupExterno);
    }

    [Fact]
    public void Executar_ExternaFalha_GerenciadaContinuaComSucessoMasMensagemAvisaDaFalhaExterna()
    {
        var backupService = new FakeBackupService { ProximaCriacaoExternaFalha = true };
        var caso = new CriarBackupManual(backupService);

        var resultado = caso.Executar("D:\\pendrive");

        Assert.True(resultado.Sucesso);
        Assert.False(string.IsNullOrEmpty(resultado.Valor!.Mensagem));
        Assert.Contains("externa", resultado.Valor.Mensagem, StringComparison.OrdinalIgnoreCase);
    }

    // RNF16: excecao na copia externa e registrada como Error, e a mensagem amigavel ao usuario continua a mesma
    [Fact]
    public void Executar_ExternaLancaExcecao_RegistraErroNoLogEMantemMensagem()
    {
        var backupService = new FakeBackupService { LancarExcecaoAoCriarExterno = true };
        var coletor = new ColetorDeLog();
        var caso = new CriarBackupManual(backupService, coletor.Logger);

        var resultado = caso.Executar("D:\\pendrive");

        Assert.True(resultado.Sucesso);
        Assert.Contains("a cópia na pasta externa falhou", resultado.Valor!.Mensagem);
        coletor.AfirmarFalhaTecnica(LogEventLevel.Error, "CriarBackupExterno");
    }
}
