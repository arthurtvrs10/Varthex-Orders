using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog;
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Domain;

namespace VarthexComanda.Desktop.Atendimento;

public partial class HistoricoViewModel : ObservableObject
{
    private readonly ListarVendasPorData _listarVendasPorData;
    private readonly BuscarItensDaVenda _buscarItensDaVenda;
    private readonly ILogger? _logger;

    private IReadOnlyList<VendaResumo> _vendasCarregadas = Array.Empty<VendaResumo>();

    public HistoricoViewModel(ListarVendasPorData listarVendasPorData, BuscarItensDaVenda buscarItensDaVenda, IClock relogio, ILogger? logger = null)
    {
        _listarVendasPorData = listarVendasPorData;
        _buscarItensDaVenda = buscarItensDaVenda;
        _logger = logger;

        Vendas = new ObservableCollection<VendaResumo>();
        ItensDaVendaSelecionada = new ObservableCollection<ItemComanda>();

        DataSelecionada = FusoBrasilia.ParaLocal(relogio.UtcNow).Date;
    }

    public ObservableCollection<VendaResumo> Vendas { get; }
    public ObservableCollection<ItemComanda> ItensDaVendaSelecionada { get; }

    [ObservableProperty]
    private DateTime dataSelecionada;

    [ObservableProperty]
    private string textoBuscaNumero = string.Empty;

    [ObservableProperty]
    private VendaResumo? vendaSelecionada;

    [ObservableProperty]
    private int quantidadeVendas;

    [ObservableProperty]
    private int quantidadeExibida;

    [ObservableProperty]
    private long totalDiaCentavos;

    [ObservableProperty]
    private long ticketMedioCentavos;

    [ObservableProperty]
    private string mensagem = string.Empty;

    [ObservableProperty]
    private string mensagemListaVazia = string.Empty;

    partial void OnDataSelecionadaChanged(DateTime value) => AtualizarVendas();

    partial void OnTextoBuscaNumeroChanged(string value) => AplicarFiltro();

    partial void OnVendaSelecionadaChanged(VendaResumo? value)
    {
        ItensDaVendaSelecionada.Clear();
        if (value is null)
        {
            return;
        }

        try
        {
            var itens = _buscarItensDaVenda.Executar(value.Venda.Id);
            if (itens is null)
            {
                return;
            }

            Mensagem = string.Empty;
            foreach (var item in itens)
            {
                ItensDaVendaSelecionada.Add(item);
            }
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "CarregarItensDaVenda");
            Mensagem = "Não foi possível carregar os itens da venda. Tente novamente.";
        }
    }

    public void AtualizarVendas()
    {
        try
        {
            _vendasCarregadas = _listarVendasPorData.Executar(DataSelecionada);
            Mensagem = string.Empty;
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "CarregarVendas");
            _vendasCarregadas = Array.Empty<VendaResumo>();
            Mensagem = "Não foi possível carregar as vendas. Tente novamente.";
        }
        AplicarFiltro();
    }

    private void AplicarFiltro()
    {
        var filtro = TextoBuscaNumero.Trim();
        var filtradas = string.IsNullOrEmpty(filtro)
            ? _vendasCarregadas
            : _vendasCarregadas.Where(vr => vr.NumeroComanda.ToString().Contains(filtro)).ToList();

        Vendas.Clear();
        foreach (var venda in filtradas)
        {
            Vendas.Add(venda);
        }

        QuantidadeExibida = filtradas.Count;

        QuantidadeVendas = _vendasCarregadas.Count;
        TotalDiaCentavos = _vendasCarregadas.Sum(vr => vr.Venda.TotalCentavos);
        TicketMedioCentavos = QuantidadeVendas == 0 ? 0 : TotalDiaCentavos / QuantidadeVendas;

        MensagemListaVazia = QuantidadeExibida > 0
            ? string.Empty
            : QuantidadeVendas == 0
                ? "Ainda não há vendas concluídas nesta data."
                : "Nenhuma venda encontrada para esse número.";
    }
}
