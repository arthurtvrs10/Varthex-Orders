using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Domain;

namespace VarthexComanda.Desktop.Tests.Suporte;

/// <summary>Excecao tecnica de teste; a mensagem nunca carrega dados de negocio.</summary>
internal sealed class FalhaTecnicaSimuladaException : Exception
{
    public FalhaTecnicaSimuladaException() : base("falha tecnica simulada")
    {
    }
}

internal sealed class ComandaRepositoryQueFalha : IComandaRepository
{
    private readonly IComandaRepository _interno;

    public ComandaRepositoryQueFalha(IComandaRepository interno) => _interno = interno;

    public bool Falhar { get; set; }

    private void Verificar()
    {
        if (Falhar)
        {
            throw new FalhaTecnicaSimuladaException();
        }
    }

    public IReadOnlyList<Comanda> ListarAbertas() => _interno.ListarAbertas();

    public ComandaComItens? BuscarComItens(int comandaId) => _interno.BuscarComItens(comandaId);

    public Comanda AbrirComanda(int numero, DateTime agora)
    {
        Verificar();
        return _interno.AbrirComanda(numero, agora);
    }

    public ComandaComItens AdicionarItem(int comandaId, Produto produto, int quantidade, DateTime agora)
    {
        Verificar();
        return _interno.AdicionarItem(comandaId, produto, quantidade, agora);
    }

    public ComandaComItens AlterarQuantidade(int itemId, int quantidade, DateTime agora)
    {
        Verificar();
        return _interno.AlterarQuantidade(itemId, quantidade, agora);
    }

    public ComandaComItens RemoverItem(int itemId, DateTime agora)
    {
        Verificar();
        return _interno.RemoverItem(itemId, agora);
    }

    public Comanda CancelarComanda(int comandaId, DateTime agora)
    {
        Verificar();
        return _interno.CancelarComanda(comandaId, agora);
    }

    public Venda EncerrarComanda(int comandaId, DateTime agora)
    {
        Verificar();
        return _interno.EncerrarComanda(comandaId, agora);
    }
}

internal sealed class ProdutoRepositoryQueFalha : IProdutoRepository
{
    private readonly IProdutoRepository _interno;

    public ProdutoRepositoryQueFalha(IProdutoRepository interno) => _interno = interno;

    public bool Falhar { get; set; }

    public Produto? BuscarPorId(int id) => _interno.BuscarPorId(id);

    public IReadOnlyList<Produto> Pesquisar(int? categoriaId, string? texto) => _interno.Pesquisar(categoriaId, texto);

    public Produto Salvar(Produto produto) =>
        Falhar ? throw new FalhaTecnicaSimuladaException() : _interno.Salvar(produto);
}

internal sealed class CategoriaRepositoryQueFalha : ICategoriaRepository
{
    private readonly ICategoriaRepository _interno;

    public CategoriaRepositoryQueFalha(ICategoriaRepository interno) => _interno = interno;

    public bool Falhar { get; set; }

    public Categoria? BuscarPorId(int id) => _interno.BuscarPorId(id);

    public IReadOnlyList<Categoria> ListarAtivas() => _interno.ListarAtivas();

    public bool ExisteNome(string nome, int? ignorarId = null) => _interno.ExisteNome(nome, ignorarId);

    public Categoria Salvar(Categoria categoria) =>
        Falhar ? throw new FalhaTecnicaSimuladaException() : _interno.Salvar(categoria);
}

internal sealed class FotoStorageQueFalha : IFotoStorage
{
    public bool Falhar { get; set; }

    public string Importar(string caminhoOrigem) =>
        Falhar ? throw new FalhaTecnicaSimuladaException() : "foto1.jpg";

    public void Excluir(string nomeArquivo)
    {
    }
}

internal sealed class ConfiguracaoRepositoryQueFalha : IConfiguracaoRepository
{
    private readonly IConfiguracaoRepository _interno;

    public ConfiguracaoRepositoryQueFalha(IConfiguracaoRepository interno) => _interno = interno;

    public bool Falhar { get; set; }

    public string? ObterValor(string chave) => _interno.ObterValor(chave);

    public void Definir(string chave, string valor, DateTime atualizadoEm)
    {
        if (Falhar)
        {
            throw new FalhaTecnicaSimuladaException();
        }

        _interno.Definir(chave, valor, atualizadoEm);
    }
}
