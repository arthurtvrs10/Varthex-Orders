using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Backup;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Application.Configuracao;
using VarthexComanda.Domain;

namespace VarthexComanda.Desktop.Tests.Suporte;

// Repositorios cujas leituras e escritas sempre lancam, como um banco SQLite corrompido faria.
internal sealed class ComandaRepositoryDeBancoCorrompido : IComandaRepository
{
    private static Exception Falha() => new FalhaTecnicaSimuladaException();
    public Comanda DefinirNomeCliente(int comandaId, string? nome) => throw Falha();

    public IReadOnlyList<Comanda> ListarAbertas() => throw Falha();
    public ComandaComItens? BuscarComItens(int comandaId) => throw Falha();
    public Comanda AbrirComanda(int numero, DateTime agora) => throw Falha();
    public ComandaComItens AdicionarItem(int comandaId, Produto produto, int quantidade, DateTime agora) => throw Falha();
    public ComandaComItens AlterarQuantidade(int itemId, int quantidade, DateTime agora) => throw Falha();
    public ComandaComItens RemoverItem(int itemId, DateTime agora) => throw Falha();
    public Comanda CancelarComanda(int comandaId, DateTime agora) => throw Falha();
    public Venda EncerrarComanda(int comandaId, DateTime agora) => throw Falha();
}

internal sealed class CategoriaRepositoryDeBancoCorrompido : ICategoriaRepository
{
    public Categoria? BuscarPorId(int id) => throw new FalhaTecnicaSimuladaException();
    public IReadOnlyList<Categoria> ListarAtivas() => throw new FalhaTecnicaSimuladaException();
    public bool ExisteNome(string nome, int? ignorarId = null) => throw new FalhaTecnicaSimuladaException();
    public Categoria Salvar(Categoria categoria) => throw new FalhaTecnicaSimuladaException();
}

internal sealed class ProdutoRepositoryDeBancoCorrompido : IProdutoRepository
{
    public Produto? BuscarPorId(int id) => throw new FalhaTecnicaSimuladaException();
    public IReadOnlyList<Produto> Pesquisar(int? categoriaId, string? texto) => throw new FalhaTecnicaSimuladaException();
    public Produto Salvar(Produto produto) => throw new FalhaTecnicaSimuladaException();
    public IReadOnlyList<string> ListarNomesDeFotos() => throw new FalhaTecnicaSimuladaException();
}

internal sealed class ConfiguracaoRepositoryDeBancoCorrompido : IConfiguracaoRepository
{
    public string? ObterValor(string chave) => throw new FalhaTecnicaSimuladaException();
    public void Definir(string chave, string valor, DateTime atualizadoEm) => throw new FalhaTecnicaSimuladaException();
}

internal sealed class BackupRegistroRepositoryDeBancoCorrompido : IBackupRegistroRepository
{
    public void Registrar(BackupRegistro registro) => throw new FalhaTecnicaSimuladaException();
    public IReadOnlyList<BackupRegistro> ListarRecentes(int quantidade) => throw new FalhaTecnicaSimuladaException();
    public bool ExisteBackupHoje(DateTime inicioUtc, DateTime fimUtc) => throw new FalhaTecnicaSimuladaException();
}
