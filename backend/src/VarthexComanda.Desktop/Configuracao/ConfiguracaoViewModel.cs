using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using VarthexComanda.Application.Configuracao;

namespace VarthexComanda.Desktop.Configuracao;

public partial class ConfiguracaoViewModel : ObservableObject
{
    private readonly ObterConfiguracao _obterConfiguracao;
    private readonly SalvarConfiguracao _salvarConfiguracao;
    private readonly ILogger? _logger;

    public ConfiguracaoViewModel(ObterConfiguracao obterConfiguracao, SalvarConfiguracao salvarConfiguracao, ILogger? logger = null)
    {
        _obterConfiguracao = obterConfiguracao;
        _salvarConfiguracao = salvarConfiguracao;
        _logger = logger;

        // Com o banco corrompido (modo de restauração) a leitura lança; a construção não pode
        // falhar, senão a MainWindow nem abre. Segue com os campos vazios.
        try
        {
            Carregar();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "CarregarConfiguracao");
        }
    }

    public string VersaoTexto => $"Versão {VersaoDoAplicativo.Atual}";

    [ObservableProperty]
    private string nomeEstabelecimento = string.Empty;

    [ObservableProperty]
    private string quantidadeComandasTexto = string.Empty;

    [ObservableProperty]
    private string? pastaBackupExterna;

    [ObservableProperty]
    private string mensagem = string.Empty;

    public void Carregar()
    {
        var configuracao = _obterConfiguracao.Executar();
        NomeEstabelecimento = configuracao.NomeEstabelecimento;
        QuantidadeComandasTexto = configuracao.QuantidadeMaximaComandas?.ToString() ?? string.Empty;
        PastaBackupExterna = configuracao.PastaBackupExterna;
    }

    public void DefinirPastaBackupExterna(string? pasta)
    {
        PastaBackupExterna = pasta;
    }

    [RelayCommand]
    private void Salvar()
    {
        int? quantidade = null;
        if (!string.IsNullOrWhiteSpace(QuantidadeComandasTexto))
        {
            if (!int.TryParse(QuantidadeComandasTexto, out var quantidadeValor))
            {
                Mensagem = "Informe uma quantidade de comandas válida.";
                return;
            }

            quantidade = quantidadeValor;
        }

        try
        {
            var resultado = _salvarConfiguracao.Executar(new ConfiguracaoEstabelecimento
            {
                NomeEstabelecimento = NomeEstabelecimento,
                QuantidadeMaximaComandas = quantidade,
                PastaBackupExterna = PastaBackupExterna
            });

            Mensagem = resultado.Sucesso
                ? "Configurações salvas com sucesso."
                : string.Join(" ", resultado.Erros);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "SalvarConfiguracao");
            Mensagem = "Não foi possível salvar as configurações. Tente novamente.";
        }
    }
}
