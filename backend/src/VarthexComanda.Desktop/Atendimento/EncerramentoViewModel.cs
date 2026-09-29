using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Domain;

namespace VarthexComanda.Desktop.Atendimento;

public partial class EncerramentoViewModel : ObservableObject
{
    private readonly EncerrarComanda _encerrarComanda;
    private readonly IComandaRepository _comandas;
    private int _comandaId;
    private readonly ILogger? _logger;

    public EncerramentoViewModel(EncerrarComanda encerrarComanda, IComandaRepository comandas, ILogger? logger = null)
    {
        _encerrarComanda = encerrarComanda;
        _comandas = comandas;
        _logger = logger;
        Itens = new ObservableCollection<ItemComanda>();
    }

    public event EventHandler<bool>? Concluido;

    public ObservableCollection<ItemComanda> Itens { get; }

    [ObservableProperty]
    private int numeroComanda;
    [ObservableProperty]
    private string? nomeCliente;

    [ObservableProperty]
    private long totalCentavos;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmarEncerrarCommand))]
    private bool cobrancaAprovada;

    [ObservableProperty]
    private string mensagem = string.Empty;

    public void Carregar(int comandaId)
    {
        CobrancaAprovada = false;
        Mensagem = string.Empty;

        _comandaId = comandaId;
        var detalhe = _comandas.BuscarComItens(comandaId);
        if (detalhe is null)
        {
            Mensagem = "Comanda não encontrada.";
            return;
        }

        NumeroComanda = detalhe.Comanda.Numero;
        NomeCliente = detalhe.Comanda.NomeCliente;
        TotalCentavos = detalhe.Comanda.TotalCentavos;
        Itens.Clear();
        foreach (var item in detalhe.Itens)
        {
            Itens.Add(item);
        }
    }

    [RelayCommand(CanExecute = nameof(PodeConfirmarEncerrar))]
    private void ConfirmarEncerrar()
    {
        if (!CobrancaAprovada)
        {
            return;
        }

        try
        {
            var resultado = _encerrarComanda.Executar(_comandaId);
            if (!resultado.Sucesso)
            {
                Mensagem = string.Join(" ", resultado.Erros);
                return;
            }

            Mensagem = string.Empty;
            Concluido?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "EncerrarComanda");
            Mensagem = "A venda não foi registrada e a comanda continua aberta.";
        }
    }

    private bool PodeConfirmarEncerrar() => CobrancaAprovada;

    [RelayCommand]
    private void Voltar() => Concluido?.Invoke(this, false);
}
