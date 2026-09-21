using VarthexComanda.Application.Backup;
using Xunit;

namespace VarthexComanda.Application.Tests.Backup;

public class RestaurarBackupTests
{
    [Fact]
    public void Executar_RelatorioAprovado_ChamaRestaurarPara()
    {
        var backupService = new FakeBackupService();
        var caso = new RestaurarBackup(backupService);

        var resultado = caso.Executar("C:\\backup-valido.db");

        Assert.True(resultado.Sucesso);
    }

    [Fact]
    [Trait("Caso", "CT15")]
    public void Executar_RelatorioReprovado_NaoChamaRestaurarParaERetornaFalha()
    {
        var backupService = new FakeBackupService
        {
            ProximoRelatorio = new RelatorioValidacao
            {
                FormatoValido = false,
                VersaoCompativel = false,
                IntegridadeOk = false,
                ChecksumConfere = null,
                Motivo = "Arquivo corrompido."
            },
            ProximaRestauracaoFalha = true // se RestaurarPara for chamado por engano, o teste também falharia por outro motivo
        };
        var caso = new RestaurarBackup(backupService);

        var resultado = caso.Executar("C:\\backup-invalido.db");

        Assert.False(resultado.Sucesso);
        Assert.Equal("Arquivo corrompido.", resultado.Erros[0]);
    }
}
