using Serilog;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Backup;

public class CriarBackupManual
{
    private readonly IBackupService _backupService;
    private readonly ILogger? _logger;

    public CriarBackupManual(IBackupService backupService, ILogger? logger = null)
    {
        _backupService = backupService;
        _logger = logger;
    }

    public Resultado<BackupRegistro> Executar(string? pastaExterna)
    {
        var resultado = _backupService.CriarBackupGerenciado();
        if (pastaExterna is not null && resultado.Sucesso)
        {
            try
            {
                var resultadoExterno = _backupService.CriarBackupExterno(pastaExterna);
                if (!resultadoExterno.Sucesso)
                {
                    resultado.Valor!.Mensagem = "Backup criado na pasta do aplicativo, mas a cópia na pasta externa falhou: " + string.Join(" ", resultadoExterno.Erros);
                }
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "Falha em {Operacao}", "CriarBackupExterno");
                // falha na cópia externa não invalida o backup gerenciado, que já teve sucesso,
                // mas o operador precisa ser avisado de que a cópia externa não foi feita
                resultado.Valor!.Mensagem = "Backup criado na pasta do aplicativo, mas a cópia na pasta externa falhou: " + ex.Message;
            }
        }

        return resultado;
    }
}
