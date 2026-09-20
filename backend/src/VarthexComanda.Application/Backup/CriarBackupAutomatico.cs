using Serilog;
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Configuracao;

namespace VarthexComanda.Application.Backup;

public class CriarBackupAutomatico
{
    private readonly IBackupService _backupService;
    private readonly IBackupRegistroRepository _registros;
    private readonly IClock _relogio;
    private readonly ObterConfiguracao _obterConfiguracao;
    private readonly ILogger _logger;

    public CriarBackupAutomatico(IBackupService backupService, IBackupRegistroRepository registros, IClock relogio, ObterConfiguracao obterConfiguracao, ILogger logger)
    {
        _backupService = backupService;
        _registros = registros;
        _relogio = relogio;
        _obterConfiguracao = obterConfiguracao;
        _logger = logger;
    }

    public void Executar(bool incondicional = false)
    {
        try
        {
            if (!incondicional)
            {
                var hoje = FusoBrasilia.ParaLocal(_relogio.UtcNow).Date;
                var inicioUtc = FusoBrasilia.ParaUtc(hoje);
                var fimUtc = FusoBrasilia.ParaUtc(hoje.AddDays(1));
                if (_registros.ExisteBackupHoje(inicioUtc, fimUtc))
                {
                    return;
                }
            }

            _backupService.CriarBackupGerenciado();

            if (incondicional)
            {
                var configuracao = _obterConfiguracao.Executar();
                if (!string.IsNullOrEmpty(configuracao.PastaBackupExterna))
                {
                    var resultadoExterno = _backupService.CriarBackupExterno(configuracao.PastaBackupExterna);
                    if (!resultadoExterno.Sucesso)
                    {
                        _logger.Warning("Cópia externa automática falhou: {Mensagem}",
                            string.Join(" ", resultadoExterno.Erros));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // backup automático nunca deve interromper o app, mas a falha fica registrada
            _logger.Warning(ex, "Backup automatico falhou");
        }
    }
}
