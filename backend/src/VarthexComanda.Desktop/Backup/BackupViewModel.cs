using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using VarthexComanda.Application.Backup;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Storage;

namespace VarthexComanda.Desktop.Backup;

public partial class BackupViewModel : ObservableObject
{
    private readonly CriarBackupManual _criarBackupManual;
    private readonly RestaurarBackup _restaurarBackup;
    private readonly ListarBackupsRecentes _listarBackupsRecentes;
    private readonly IConfirmador _confirmador;
    private readonly ILogger? _logger;
    private readonly AppPaths? _paths;

    public event EventHandler? SolicitouReinicio;

    public BackupViewModel(
        CriarBackupManual criarBackupManual,
        RestaurarBackup restaurarBackup,
        ListarBackupsRecentes listarBackupsRecentes,
        IConfirmador confirmador,
        ILogger? logger = null,
        AppPaths? paths = null)
    {
        _criarBackupManual = criarBackupManual;
        _restaurarBackup = restaurarBackup;
        _listarBackupsRecentes = listarBackupsRecentes;
        _confirmador = confirmador;
        _logger = logger;
        _paths = paths;

        Backups = new ObservableCollection<BackupRegistro>();
        AtualizarLista();
    }

    public ObservableCollection<BackupRegistro> Backups { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestaurarCommand))]
    private BackupRegistro? backupSelecionado;

    [ObservableProperty]
    private string mensagem = string.Empty;

    /// <summary>
    /// Modo de restauração (banco corrompido): não cria backups e a lista vem dos arquivos da
    /// pasta de backups, já que o registro de backups vive no banco corrompido.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CriarBackupCommand))]
    [NotifyPropertyChangedFor(nameof(PodeCriarBackup))]
    private bool modoRestauracao;

    public bool PodeCriarBackup => !ModoRestauracao;

    public void AtualizarLista()
    {
        Backups.Clear();
        if (ModoRestauracao)
        {
            ListarBackupsDoDisco();
            return;
        }

        try
        {
            foreach (var registro in _listarBackupsRecentes.Executar(20))
            {
                Backups.Add(registro);
            }
        }
        catch (Exception ex)
        {
            // o registro de backups vive no próprio banco; se ele estiver corrompido a lista
            // não carrega, mas a restauração por "Selecionar arquivo..." continua possível
            _logger?.Error(ex, "Falha em {Operacao}", "ListarBackups");
            Backups.Clear();
            Mensagem = "Não foi possível listar os backups registrados. Use \"Selecionar arquivo...\" para escolher um backup.";
        }
    }

    private void ListarBackupsDoDisco()
    {
        Mensagem = string.Empty;
        try
        {
            var pasta = _paths?.BackupsDirectory;
            if (pasta is not null && Directory.Exists(pasta))
            {
                // o nome carrega data e hora (yyyy-MM-dd-HHmmss), então a ordem decrescente do
                // nome é a mais recente primeiro; corrompido-*.db.bak nunca casa com este padrão
                var arquivos = Directory.GetFiles(pasta, "varthex-comanda-*.db")
                    .OrderByDescending(Path.GetFileName, StringComparer.Ordinal);
                foreach (var caminho in arquivos)
                {
                    var checksum = caminho + ".sha256";
                    Backups.Add(new BackupRegistro
                    {
                        Id = 0,
                        Arquivo = Path.GetFileName(caminho),
                        Destino = pasta,
                        CriadoEm = File.GetLastWriteTimeUtc(caminho),
                        Status = StatusBackup.Sucesso,
                        Checksum = File.Exists(checksum) ? File.ReadAllText(checksum).Trim() : null,
                        Mensagem = null
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "ListarBackupsDoDisco");
            Backups.Clear();
        }

        if (Backups.Count == 0)
        {
            Mensagem = "Nenhum backup encontrado na pasta de backups. Use \"Selecionar arquivo...\" para escolher um backup.";
        }
    }

    [RelayCommand(CanExecute = nameof(PodeCriarBackup))]
    private void CriarBackup(string? pastaExterna)
    {
        if (ModoRestauracao)
        {
            return;
        }

        try
        {
            var resultado = _criarBackupManual.Executar(pastaExterna);
            Mensagem = resultado.Sucesso
                ? (string.IsNullOrEmpty(resultado.Valor?.Mensagem) ? "Backup criado com sucesso." : resultado.Valor!.Mensagem!)
                : string.Join(" ", resultado.Erros);
            AtualizarLista();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "CriarBackup");
            Mensagem = "Não foi possível criar o backup. Tente novamente.";
        }
    }

    [RelayCommand(CanExecute = nameof(PodeRestaurar))]
    private void Restaurar()
    {
        if (BackupSelecionado is null)
        {
            return;
        }

        var caminho = Path.Combine(BackupSelecionado.Destino, BackupSelecionado.Arquivo);
        RestaurarCaminho(caminho, BackupSelecionado.CriadoEm);
    }

    public void RestaurarArquivoExterno(string caminho)
    {
        RestaurarCaminho(caminho, null);
    }

    private void RestaurarCaminho(string caminho, DateTime? criadoEm)
    {
        var mensagemConfirmacao = criadoEm is not null
            ? $"Isso vai substituir todos os dados atuais pelo backup de {criadoEm:dd/MM/yyyy HH:mm}. Uma cópia de segurança da base atual será criada antes."
            : "Isso vai substituir todos os dados atuais pelo backup selecionado. Uma cópia de segurança da base atual será criada antes.";
        if (!_confirmador.Confirmar("Restaurar backup", mensagemConfirmacao))
        {
            return;
        }

        try
        {
            var resultado = _restaurarBackup.Executar(caminho);
            if (!resultado.Sucesso)
            {
                Mensagem = string.Join(" ", resultado.Erros);
                return;
            }

            Mensagem = string.Empty;
            SolicitouReinicio?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "RestaurarBackup");
            Mensagem = "Não foi possível restaurar o backup. Tente novamente.";
        }
    }

    private bool PodeRestaurar() => BackupSelecionado is not null;
}
