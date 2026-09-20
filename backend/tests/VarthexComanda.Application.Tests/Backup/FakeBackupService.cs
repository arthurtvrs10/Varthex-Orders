using VarthexComanda.Application.Backup;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Tests.Backup;

public class FakeBackupService : IBackupService
{
    public int ChamadasCriarBackupGerenciado { get; private set; }
    public int ChamadasCriarBackupExterno { get; private set; }
    public int ChamadasRestaurarPara { get; private set; }
    public bool LancarExcecaoAoCriar { get; set; }
    public bool ProximaCriacaoFalha { get; set; }
    public bool ProximaCriacaoExternaFalha { get; set; }
    public RelatorioValidacao ProximoRelatorio { get; set; } = new()
    {
        FormatoValido = true,
        VersaoCompativel = true,
        IntegridadeOk = true,
        ChecksumConfere = true,
        Motivo = string.Empty
    };
    public bool ProximaRestauracaoFalha { get; set; }
    public bool LancarExcecaoAoRestaurar { get; set; }
    public bool LancarExcecaoAoCriarExterno { get; set; }

    public Resultado<BackupRegistro> CriarBackupGerenciado()
    {
        ChamadasCriarBackupGerenciado++;
        if (LancarExcecaoAoCriar)
        {
            throw new InvalidOperationException("Falha simulada.");
        }
        return ProximaCriacaoFalha
            ? Resultado<BackupRegistro>.Falha("Falha simulada ao criar backup gerenciado.")
            : Resultado<BackupRegistro>.Ok(NovoRegistro("C:\\backups"));
    }

    public Resultado<BackupRegistro> CriarBackupExterno(string pastaExterna)
    {
        ChamadasCriarBackupExterno++;
        if (LancarExcecaoAoCriarExterno)
        {
            throw new InvalidOperationException("Falha simulada.");
        }
        return ProximaCriacaoFalha || ProximaCriacaoExternaFalha
            ? Resultado<BackupRegistro>.Falha("Falha simulada ao criar backup externo.")
            : Resultado<BackupRegistro>.Ok(NovoRegistro(pastaExterna));
    }

    public RelatorioValidacao Validar(string caminhoArquivo) => ProximoRelatorio;

    public Resultado<BackupRegistro> RestaurarPara(string caminhoArquivo)
    {
        ChamadasRestaurarPara++;
        if (LancarExcecaoAoRestaurar)
        {
            throw new InvalidOperationException("Falha simulada.");
        }
        return ProximaRestauracaoFalha
            ? Resultado<BackupRegistro>.Falha("Falha simulada ao restaurar.")
            : Resultado<BackupRegistro>.Ok(NovoRegistro("C:\\backups"));
    }

    private static BackupRegistro NovoRegistro(string destino) => new()
    {
        Id = 1,
        Arquivo = "varthex-comanda-2026-09-18-120000.db",
        Destino = destino,
        CriadoEm = new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc),
        Status = StatusBackup.Sucesso,
        Checksum = "abc123",
        Mensagem = null
    };
}
