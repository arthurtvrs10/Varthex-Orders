using VarthexComanda.Domain;

namespace VarthexComanda.Application.Atendimento;

public interface IComandaRepository
{
    IReadOnlyList<Comanda> ListarAbertas();
    ComandaComItens? BuscarComItens(int comandaId);
    Comanda AbrirComanda(int numero, DateTime agora);
    Comanda DefinirNomeCliente(int comandaId, string? nome);
    ComandaComItens AdicionarItem(int comandaId, Produto produto, int quantidade, DateTime agora);
    ComandaComItens AlterarQuantidade(int itemId, int quantidade, DateTime agora);
    ComandaComItens RemoverItem(int itemId, DateTime agora);
    Comanda CancelarComanda(int comandaId, DateTime agora);
    Venda EncerrarComanda(int comandaId, DateTime agora);
}
