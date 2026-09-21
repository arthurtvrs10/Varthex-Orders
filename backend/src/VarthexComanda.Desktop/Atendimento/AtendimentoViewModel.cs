using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Domain;

namespace VarthexComanda.Desktop.Atendimento;

public partial class AtendimentoViewModel : ObservableObject
{
    private readonly AbrirComanda _abrirComanda;
    private readonly AdicionarItem _adicionarItem;
    private readonly AlterarQuantidade _alterarQuantidade;
    private readonly RemoverItem _removerItem;
    private readonly CancelarComanda _cancelarComanda;
    private readonly IComandaRepository _comandas;
    private readonly ListarCategoriasAtivas _listarCategoriasAtivas;
    private readonly PesquisarProdutos _pesquisarProdutos;
    private readonly IConfirmador _confirmador;
    private readonly IEncerramentoDialog _encerramentoDialog;
    private readonly ObterConfiguracao _obterConfiguracao;
    private readonly IClock _relogio;
    private readonly ILogger? _logger;

    public AtendimentoViewModel(
        AbrirComanda abrirComanda,
        AdicionarItem adicionarItem,
        AlterarQuantidade alterarQuantidade,
        RemoverItem removerItem,
        CancelarComanda cancelarComanda,
        IComandaRepository comandas,
        ListarCategoriasAtivas listarCategoriasAtivas,
        PesquisarProdutos pesquisarProdutos,
        IConfirmador confirmador,
        IEncerramentoDialog encerramentoDialog,
        ObterConfiguracao obterConfiguracao,
        IClock relogio,
        ILogger? logger = null)
    {
        _abrirComanda = abrirComanda;
        _adicionarItem = adicionarItem;
        _alterarQuantidade = alterarQuantidade;
        _removerItem = removerItem;
        _cancelarComanda = cancelarComanda;
        _comandas = comandas;
        _listarCategoriasAtivas = listarCategoriasAtivas;
        _pesquisarProdutos = pesquisarProdutos;
        _confirmador = confirmador;
        _encerramentoDialog = encerramentoDialog;
        _obterConfiguracao = obterConfiguracao;
        _relogio = relogio;
        _logger = logger;

        ComandasAbertas = new ObservableCollection<Comanda>();
        Categorias = new ObservableCollection<Categoria>();
        Itens = new ObservableCollection<ItemComanda>();
        ProdutosCatalogo = new ObservableCollection<Produto>();
        Slots = new ObservableCollection<ComandaSlotItem>();

        // Com o banco corrompido (modo de restauração) estas leituras lançam; a construção
        // não pode falhar, senão a MainWindow nem abre. Segue com listas vazias.
        try
        {
            AtualizarComandasAbertas();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "CarregarComandasAbertas");
            ComandasAbertas.Clear();
            Slots.Clear();
        }

        try
        {
            CarregarCategorias();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "CarregarCategorias");
            Categorias.Clear();
            ProdutosCatalogo.Clear();
        }
    }

    public ObservableCollection<Comanda> ComandasAbertas { get; }
    public ObservableCollection<Categoria> Categorias { get; }
    public ObservableCollection<ItemComanda> Itens { get; }
    public ObservableCollection<Produto> ProdutosCatalogo { get; }
    public ObservableCollection<ComandaSlotItem> Slots { get; }

    [ObservableProperty]
    private string novoNumero = string.Empty;

    [ObservableProperty]
    private Comanda? comandaAtual;

    [ObservableProperty]
    private Categoria? categoriaCatalogo;

    [ObservableProperty]
    private string textoBuscaCatalogo = string.Empty;

    [ObservableProperty]
    private string mensagem = string.Empty;

    /// <summary>Item da comanda destacado para operar por teclado (Up/Down, +, -, Delete).</summary>
    [ObservableProperty]
    private int? itemSelecionadoId;

    partial void OnCategoriaCatalogoChanged(Categoria? value) => PesquisarCatalogo();

    partial void OnTextoBuscaCatalogoChanged(string value) => PesquisarCatalogo();

    [RelayCommand]
    private void PesquisarCatalogo()
    {
        var resultado = _pesquisarProdutos.Executar(CategoriaCatalogo?.Id, TextoBuscaCatalogo);
        ProdutosCatalogo.Clear();
        foreach (var produto in resultado.Where(p => p.Ativo))
        {
            ProdutosCatalogo.Add(produto);
        }
    }

    [RelayCommand]
    private void LimparFiltro()
    {
        CategoriaCatalogo = null;
    }

    [RelayCommand]
    private void SelecionarCategoria(Categoria categoria) => CategoriaCatalogo = categoria;

    [RelayCommand]
    private void Abrir()
    {
        if (!int.TryParse(NovoNumero, out var numero))
        {
            Mensagem = "Informe um número de comanda válido.";
            return;
        }

        try
        {
            var resultado = _abrirComanda.Executar(numero);
            if (!resultado.Sucesso)
            {
                Mensagem = string.Join(" ", resultado.Erros);
                return;
            }

            NovoNumero = string.Empty;
            Mensagem = string.Empty;
            AtualizarComandasAbertas();
            AbrirParaEdicao(resultado.Valor!.Id);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "AbrirComanda");
            Mensagem = "Não foi possível abrir a comanda. Tente novamente.";
        }
    }

    [RelayCommand]
    private void SelecionarComanda(Comanda comanda) => AbrirParaEdicao(comanda.Id);

    [RelayCommand]
    private void AbrirOuSelecionarSlot(ComandaSlotItem slot)
    {
        if (slot.Aberta)
        {
            if (slot.ComandaId is int comandaId)
            {
                AbrirParaEdicao(comandaId);
            }
            return;
        }

        NovoNumero = slot.Numero.ToString();
        Abrir();
    }

    private void AbrirParaEdicao(int comandaId)
    {
        var detalhe = _comandas.BuscarComItens(comandaId);
        if (detalhe is null)
        {
            Mensagem = "Comanda não encontrada.";
            return;
        }

        ComandaAtual = detalhe.Comanda;
        ItemSelecionadoId = null;
        SubstituirItens(detalhe.Itens, null);
        VerTotalCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void FecharEdicao()
    {
        ComandaAtual = null;
        ItemSelecionadoId = null;
        TextoBuscaCatalogo = string.Empty;
        Itens.Clear();
        VerTotalCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(PodeVerTotal))]
    private void VerTotal()
    {
        if (ComandaAtual is null)
        {
            return;
        }

        var comandaId = ComandaAtual.Id;
        if (_encerramentoDialog.Abrir(comandaId))
        {
            FecharEdicao();
            AtualizarComandasAbertas();
        }
    }

    private bool PodeVerTotal() => ComandaAtual is not null && Itens.Count > 0;

    [RelayCommand]
    private void AdicionarProdutoAoItem(Produto produto)
    {
        if (ComandaAtual is null)
        {
            return;
        }

        try
        {
            AplicarResultado(_adicionarItem.Executar(ComandaAtual.Id, produto.Id, 1), produto.Id);
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "AdicionarItem");
            Mensagem = "Não foi possível adicionar o produto à comanda. Tente novamente.";
        }
    }

    [RelayCommand]
    private void AumentarQuantidade(ItemComanda item)
    {
        try
        {
            AplicarResultado(_alterarQuantidade.Executar(item.Id, item.Quantidade + 1));
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "AumentarQuantidade");
            Mensagem = "Não foi possível atualizar a quantidade do item. Tente novamente.";
        }
    }

    [RelayCommand]
    private void DiminuirQuantidade(ItemComanda item)
    {
        try
        {
            if (item.Quantidade <= 1)
            {
                if (!_confirmador.Confirmar("Remover item", MensagemRemocao(item)))
                {
                    return;
                }

                AplicarResultado(_removerItem.Executar(item.Id));
                return;
            }

            AplicarResultado(_alterarQuantidade.Executar(item.Id, item.Quantidade - 1));
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "DiminuirQuantidade");
            Mensagem = "Não foi possível atualizar a quantidade do item. Tente novamente.";
        }
    }

    [RelayCommand]
    private void Remover(ItemComanda item)
    {
        if (!_confirmador.Confirmar("Remover item", MensagemRemocao(item)))
        {
            return;
        }

        try
        {
            AplicarResultado(_removerItem.Executar(item.Id));
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "RemoverItem");
            Mensagem = "Não foi possível remover o item. Tente novamente.";
        }
    }

    private static string MensagemRemocao(ItemComanda item) =>
        $"Deseja remover o item \"{item.NomeProduto}\" da comanda?";

    [RelayCommand]
    private void CancelarComandaAtual()
    {
        if (ComandaAtual is null)
        {
            return;
        }

        if (Itens.Count > 0)
        {
            var mensagem = $"A comanda {ComandaAtual.Numero} possui {Itens.Count} item(ns) e total de " +
                $"{CentavosParaMoedaConverter.Formatar(ComandaAtual.TotalCentavos)}. Deseja cancelar mesmo assim?";
            if (!_confirmador.Confirmar("Cancelar comanda", mensagem))
            {
                return;
            }
        }

        try
        {
            var resultado = _cancelarComanda.Executar(ComandaAtual.Id);
            if (!resultado.Sucesso)
            {
                Mensagem = string.Join(" ", resultado.Erros);
                return;
            }

            Mensagem = string.Empty;
            FecharEdicao();
            AtualizarComandasAbertas();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "CancelarComanda");
            Mensagem = "Não foi possível cancelar a comanda. Tente novamente.";
        }
    }

    /// <summary>
    /// Enter na busca: adiciona o primeiro produto listado (respeita categoria e filtro atuais).
    /// O texto da busca permanece; a view o seleciona para o proximo item.
    /// </summary>
    [RelayCommand]
    private void AdicionarPrimeiroDaBusca()
    {
        var primeiro = ProdutosCatalogo.FirstOrDefault();
        if (primeiro is null)
        {
            Mensagem = "Nenhum produto encontrado.";
            return;
        }

        AdicionarProdutoAoItem(primeiro);
    }

    [RelayCommand]
    private void SelecionarProximoItem()
    {
        if (Itens.Count == 0)
        {
            return;
        }

        var indice = IndiceDoSelecionado();
        ItemSelecionadoId = Itens[indice < 0 ? 0 : Math.Min(indice + 1, Itens.Count - 1)].Id;
    }

    [RelayCommand]
    private void SelecionarItemAnterior()
    {
        if (Itens.Count == 0)
        {
            return;
        }

        var indice = IndiceDoSelecionado();
        ItemSelecionadoId = Itens[indice < 0 ? Itens.Count - 1 : Math.Max(indice - 1, 0)].Id;
    }

    [RelayCommand]
    private void AumentarSelecionado()
    {
        if (ObterSelecionado() is { } item)
        {
            AumentarQuantidade(item);
        }
    }

    [RelayCommand]
    private void DiminuirSelecionado()
    {
        if (ObterSelecionado() is { } item)
        {
            DiminuirQuantidade(item);
        }
    }

    [RelayCommand]
    private void RemoverSelecionado()
    {
        if (ObterSelecionado() is { } item)
        {
            Remover(item);
        }
    }

    private ItemComanda? ObterSelecionado() =>
        ItemSelecionadoId is int id ? Itens.FirstOrDefault(i => i.Id == id) : null;

    private int IndiceDoSelecionado()
    {
        if (ItemSelecionadoId is not int id)
        {
            return -1;
        }

        for (var i = 0; i < Itens.Count; i++)
        {
            if (Itens[i].Id == id)
            {
                return i;
            }
        }
        return -1;
    }

    // Recarrega a lista mantendo a selecao coerente: o item pedido (se existir), senao o mesmo item,
    // senao o vizinho na posicao em que o selecionado estava (ou nada, se a lista esvaziou).
    private void SubstituirItens(IEnumerable<ItemComanda> novos, int? selecionar)
    {
        var indiceAnterior = IndiceDoSelecionado();
        var idAtual = ItemSelecionadoId;

        Itens.Clear();
        foreach (var item in novos)
        {
            Itens.Add(item);
        }

        if (selecionar is int pedido && Itens.Any(i => i.Id == pedido))
        {
            ItemSelecionadoId = pedido;
        }
        else if (idAtual is int atual && Itens.Any(i => i.Id == atual))
        {
            ItemSelecionadoId = atual;
        }
        else if (indiceAnterior < 0 || Itens.Count == 0)
        {
            ItemSelecionadoId = null;
        }
        else
        {
            ItemSelecionadoId = Itens[Math.Min(indiceAnterior, Itens.Count - 1)].Id;
        }
    }

    private void AplicarResultado(Resultado<ComandaComItens> resultado, int? produtoIdASelecionar = null)
    {
        if (!resultado.Sucesso)
        {
            Mensagem = string.Join(" ", resultado.Erros);
            return;
        }

        Mensagem = string.Empty;
        ComandaAtual = resultado.Valor!.Comanda;
        var selecionar = produtoIdASelecionar is int produtoId
            ? resultado.Valor.Itens.FirstOrDefault(i => i.ProdutoId == produtoId)?.Id
            : null;
        SubstituirItens(resultado.Valor.Itens, selecionar);
        AtualizarComandasAbertas();
        VerTotalCommand.NotifyCanExecuteChanged();
    }

    public void AtualizarComandasAbertas()
    {
        ComandasAbertas.Clear();
        foreach (var comanda in _comandas.ListarAbertas())
        {
            ComandasAbertas.Add(comanda);
        }
        AtualizarSlots();
    }

    private void AtualizarSlots()
    {
        var configuracao = _obterConfiguracao.Executar();
        var tamanho = configuracao.QuantidadeMaximaComandas ?? 20;
        var agora = _relogio.UtcNow;

        Slots.Clear();
        for (var numero = 1; numero <= tamanho; numero++)
        {
            var comanda = ComandasAbertas.FirstOrDefault(c => c.Numero == numero);
            if (comanda is not null)
            {
                Slots.Add(new ComandaSlotItem
                {
                    Numero = numero,
                    Aberta = true,
                    ComandaId = comanda.Id,
                    TotalFormatado = CentavosParaMoedaConverter.Formatar(comanda.TotalCentavos),
                    TempoFormatado = FormatarTempoAberta(comanda.AbertaEm, agora)
                });
            }
            else
            {
                Slots.Add(new ComandaSlotItem
                {
                    Numero = numero,
                    Aberta = false,
                    ComandaId = null,
                    TotalFormatado = string.Empty,
                    TempoFormatado = string.Empty
                });
            }
        }

        foreach (var comanda in ComandasAbertas.Where(c => c.Numero > tamanho))
        {
            Slots.Add(new ComandaSlotItem
            {
                Numero = comanda.Numero,
                Aberta = true,
                ComandaId = comanda.Id,
                TotalFormatado = CentavosParaMoedaConverter.Formatar(comanda.TotalCentavos),
                TempoFormatado = FormatarTempoAberta(comanda.AbertaEm, agora)
            });
        }
    }

    private static string FormatarTempoAberta(DateTime abertaEmUtc, DateTime agoraUtc)
    {
        var decorrido = agoraUtc - abertaEmUtc;
        if (decorrido.TotalMinutes < 1)
        {
            return "agora";
        }
        if (decorrido.TotalMinutes < 60)
        {
            return $"há {(int)decorrido.TotalMinutes} min";
        }
        return $"há {(int)decorrido.TotalHours} h";
    }

    public void AtualizarCategorias() => CarregarCategorias();

    private void CarregarCategorias()
    {
        var categoriaSelecionadaId = CategoriaCatalogo?.Id;
        Categorias.Clear();
        foreach (var categoria in _listarCategoriasAtivas.Executar())
        {
            Categorias.Add(categoria);
        }
        if (categoriaSelecionadaId is not null)
        {
            CategoriaCatalogo = Categorias.FirstOrDefault(c => c.Id == categoriaSelecionadaId);
        }
        PesquisarCatalogo();
    }
}
