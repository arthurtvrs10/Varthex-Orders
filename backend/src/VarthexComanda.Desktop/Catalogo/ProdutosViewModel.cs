using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Desktop.Catalogo;

public partial class ProdutosViewModel : ObservableObject
{
    private static readonly CultureInfo CulturaMoeda = CultureInfo.GetCultureInfo("pt-BR");

    private readonly ListarCategoriasAtivas _listarCategoriasAtivas;
    private readonly CadastrarCategoria _cadastrarCategoria;
    private readonly PesquisarProdutos _pesquisarProdutos;
    private readonly CadastrarProduto _cadastrarProduto;
    private readonly AlterarProduto _alterarProduto;
    private readonly DesativarProduto _desativarProduto;
    private readonly DefinirFotoProduto _definirFotoProduto;
    private readonly RemoverFotoProduto _removerFotoProduto;
    private readonly ILogger? _logger;

    public ProdutosViewModel(
        ListarCategoriasAtivas listarCategoriasAtivas,
        CadastrarCategoria cadastrarCategoria,
        PesquisarProdutos pesquisarProdutos,
        CadastrarProduto cadastrarProduto,
        AlterarProduto alterarProduto,
        DesativarProduto desativarProduto,
        DefinirFotoProduto definirFotoProduto,
        RemoverFotoProduto removerFotoProduto,
        ILogger? logger = null)
    {
        _listarCategoriasAtivas = listarCategoriasAtivas;
        _cadastrarCategoria = cadastrarCategoria;
        _pesquisarProdutos = pesquisarProdutos;
        _cadastrarProduto = cadastrarProduto;
        _alterarProduto = alterarProduto;
        _desativarProduto = desativarProduto;
        _definirFotoProduto = definirFotoProduto;
        _removerFotoProduto = removerFotoProduto;
        _logger = logger;

        Categorias = new ObservableCollection<Categoria>();
        Produtos = new ObservableCollection<Produto>();

        CarregarCategorias();
        Pesquisar();
    }

    public ObservableCollection<Categoria> Categorias { get; }
    public ObservableCollection<Produto> Produtos { get; }

    [ObservableProperty]
    private Categoria? categoriaFiltro;

    [ObservableProperty]
    private string textoBusca = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TemProdutoSelecionado))]
    [NotifyPropertyChangedFor(nameof(TituloFormulario))]
    private Produto? produtoSelecionado;

    public bool TemProdutoSelecionado => ProdutoSelecionado is not null;

    public string TituloFormulario => ProdutoSelecionado is null ? "NOVO PRODUTO" : "EDITAR PRODUTO";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoverFotoCommand))]
    private string? fotoPendenteCaminho;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoverFotoCommand))]
    private string? fotoArquivoAtual;

    [ObservableProperty]
    private string nomeProduto = string.Empty;

    [ObservableProperty]
    private Categoria? categoriaProduto;

    [ObservableProperty]
    private string precoProdutoReais = string.Empty;

    [ObservableProperty]
    private bool produtoAtivo = true;

    [ObservableProperty]
    private string novaCategoriaNome = string.Empty;

    [ObservableProperty]
    private string mensagem = string.Empty;

    partial void OnCategoriaFiltroChanged(Categoria? value) => Pesquisar();

    partial void OnTextoBuscaChanged(string value) => Pesquisar();

    partial void OnProdutoSelecionadoChanged(Produto? value)
    {
        if (value is null)
        {
            NomeProduto = string.Empty;
            CategoriaProduto = null;
            PrecoProdutoReais = string.Empty;
            ProdutoAtivo = true;
            FotoArquivoAtual = null;
            FotoPendenteCaminho = null;
            Mensagem = string.Empty;
            return;
        }

        NomeProduto = value.Nome;
        CategoriaProduto = Categorias.FirstOrDefault(c => c.Id == value.CategoriaId);
        PrecoProdutoReais = (value.PrecoCentavos / 100m).ToString("0.00", CulturaMoeda);
        ProdutoAtivo = value.Ativo;
        FotoArquivoAtual = value?.FotoArquivo;
        FotoPendenteCaminho = null;
        Mensagem = string.Empty;
    }

    [RelayCommand]
    private void Pesquisar()
    {
        var resultado = _pesquisarProdutos.Executar(CategoriaFiltro?.Id, TextoBusca);
        Produtos.Clear();
        foreach (var produto in resultado)
        {
            Produtos.Add(produto);
        }
    }

    [RelayCommand]
    private void LimparFiltro()
    {
        CategoriaFiltro = null;
    }

    [RelayCommand]
    private void Novo()
    {
        ProdutoSelecionado = null;
        NomeProduto = string.Empty;
        CategoriaProduto = null;
        PrecoProdutoReais = string.Empty;
        ProdutoAtivo = true;
        FotoPendenteCaminho = null;
        Mensagem = string.Empty;
    }

    [RelayCommand]
    private void Salvar()
    {
        if (CategoriaProduto is null)
        {
            Mensagem = "Selecione uma categoria.";
            return;
        }
        if (!TentarConverterPreco(PrecoProdutoReais, out var precoCentavos))
        {
            Mensagem = "Informe um preço válido.";
            return;
        }

        var criando = ProdutoSelecionado is null;

        try
        {
            var resultado = ProdutoSelecionado is null
                ? _cadastrarProduto.Executar(NomeProduto, CategoriaProduto.Id, precoCentavos)
                : _alterarProduto.Executar(ProdutoSelecionado.Id, NomeProduto, CategoriaProduto.Id, precoCentavos, ProdutoAtivo);

            if (!resultado.Sucesso)
            {
                Mensagem = string.Join(" ", resultado.Erros);
                return;
            }

            if (criando && FotoPendenteCaminho is not null)
            {
                var criado = resultado.Valor!;
                string? falhaFoto = null;
                try
                {
                    var resultadoFoto = _definirFotoProduto.Executar(criado.Id, FotoPendenteCaminho);
                    if (!resultadoFoto.Sucesso)
                    {
                        falhaFoto = "Produto cadastrado, mas a foto não foi adicionada: " + string.Join(" ", resultadoFoto.Erros);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.Error(ex, "Falha em {Operacao}", "SalvarFotoDoProduto");
                    falhaFoto = "Produto cadastrado, mas não foi possível salvar a foto. Tente novamente.";
                }

                if (falhaFoto is not null)
                {
                    // O produto continua cadastrado: limpa os filtros para ele aparecer na lista e o
                    // seleciona para o usuario tentar a foto de novo (sem risco de cadastrar duplicado).
                    CategoriaFiltro = null;
                    TextoBusca = string.Empty;
                    Pesquisar();
                    var selecionado = Produtos.FirstOrDefault(p => p.Id == criado.Id);
                    if (selecionado is not null)
                    {
                        ProdutoSelecionado = selecionado;
                        Mensagem = falhaFoto;
                    }
                    else
                    {
                        Novo();
                        Mensagem = falhaFoto + " Localize o produto na lista para adicionar a foto.";
                    }
                    return;
                }
            }

            Novo();
            Pesquisar();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "SalvarProduto");
            Mensagem = "Não foi possível salvar o produto. Tente novamente.";
        }
    }

    [RelayCommand]
    private void Desativar()
    {
        if (ProdutoSelecionado is null)
        {
            Mensagem = "Selecione um produto para desativar.";
            return;
        }

        try
        {
            var resultado = _desativarProduto.Executar(ProdutoSelecionado.Id);
            if (!resultado.Sucesso)
            {
                Mensagem = string.Join(" ", resultado.Erros);
                return;
            }

            Novo();
            Pesquisar();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "DesativarProduto");
            Mensagem = "Não foi possível desativar o produto. Tente novamente.";
        }
    }

    public void DefinirFoto(string caminhoOrigem)
    {
        if (ProdutoSelecionado is null)
        {
            FotoPendenteCaminho = caminhoOrigem;
            Mensagem = string.Empty;
            return;
        }

        try
        {
            var resultado = _definirFotoProduto.Executar(ProdutoSelecionado.Id, caminhoOrigem);
            if (!resultado.Sucesso)
            {
                Mensagem = string.Join(" ", resultado.Erros);
                return;
            }

            ProdutoSelecionado.FotoArquivo = resultado.Valor!.FotoArquivo;
            FotoArquivoAtual = resultado.Valor.FotoArquivo;
            Mensagem = string.Empty;
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "DefinirFoto");
            Mensagem = "Não foi possível salvar a foto. Tente novamente.";
        }
    }

    [RelayCommand(CanExecute = nameof(PodeRemoverFoto))]
    private void RemoverFoto()
    {
        if (ProdutoSelecionado is null)
        {
            FotoPendenteCaminho = null;
            return;
        }

        try
        {
            var resultado = _removerFotoProduto.Executar(ProdutoSelecionado.Id);
            if (!resultado.Sucesso)
            {
                Mensagem = string.Join(" ", resultado.Erros);
                return;
            }

            ProdutoSelecionado.FotoArquivo = null;
            FotoArquivoAtual = null;
            Mensagem = string.Empty;
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "RemoverFoto");
            Mensagem = "Não foi possível remover a foto. Tente novamente.";
        }
    }

    private bool PodeRemoverFoto() => FotoArquivoAtual is not null || FotoPendenteCaminho is not null;

    [RelayCommand]
    private void AdicionarCategoria()
    {
        try
        {
            var resultado = _cadastrarCategoria.Executar(NovaCategoriaNome);
            if (!resultado.Sucesso)
            {
                Mensagem = string.Join(" ", resultado.Erros);
                return;
            }

            NovaCategoriaNome = string.Empty;
            Mensagem = string.Empty;
            CarregarCategorias();
        }
        catch (Exception ex)
        {
            _logger?.Error(ex, "Falha em {Operacao}", "AdicionarCategoria");
            Mensagem = "Não foi possível adicionar a categoria. Tente novamente.";
        }
    }

    private void CarregarCategorias()
    {
        var categoriaSelecionadaId = CategoriaProduto?.Id;
        Categorias.Clear();
        foreach (var categoria in _listarCategoriasAtivas.Executar())
        {
            Categorias.Add(categoria);
        }
        if (categoriaSelecionadaId is not null)
        {
            CategoriaProduto = Categorias.FirstOrDefault(c => c.Id == categoriaSelecionadaId);
        }
    }

    private static bool TentarConverterPreco(string texto, out long precoCentavos)
    {
        precoCentavos = 0;
        const NumberStyles estilo = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite;
        if (!decimal.TryParse(texto, estilo, CulturaMoeda, out var valor))
        {
            return false;
        }
        if (valor <= 0)
        {
            return false;
        }
        precoCentavos = (long)Math.Round(valor * 100m, MidpointRounding.AwayFromZero);
        return true;
    }
}
