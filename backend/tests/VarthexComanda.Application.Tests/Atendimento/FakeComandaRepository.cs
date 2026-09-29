using VarthexComanda.Application.Atendimento;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Tests.Atendimento;

public class FakeComandaRepository : IComandaRepository
{
    private readonly List<Comanda> _comandas = new();
    private readonly List<ItemComanda> _itens = new();
    private int _proximoComandaId = 1;
    private int _proximoItemId = 1;

    public Comanda DefinirNomeCliente(int comandaId, string? nome)
    {
        var comanda = _comandas.Single(c => c.Id == comandaId);
        if (comanda.Status != StatusComanda.Aberta) throw new ComandaNaoAbertaException();
        comanda.NomeCliente = nome;
        return comanda;
    }

    public IReadOnlyList<Comanda> ListarAbertas() =>
        _comandas.Where(c => c.Status == StatusComanda.Aberta).OrderBy(c => c.Numero).ToList();

    public ComandaComItens? BuscarComItens(int comandaId)
    {
        var comanda = _comandas.FirstOrDefault(c => c.Id == comandaId);
        if (comanda is null)
        {
            return null;
        }
        var itens = _itens.Where(i => i.ComandaId == comandaId).ToList();
        return new ComandaComItens { Comanda = comanda, Itens = itens };
    }

    public Comanda AbrirComanda(int numero, DateTime agora)
    {
        if (_comandas.Any(c => c.Numero == numero && c.Status == StatusComanda.Aberta))
        {
            throw new NumeroComandaOcupadoException();
        }

        var comanda = new Comanda
        {
            Id = _proximoComandaId++,
            Numero = numero,
            Status = StatusComanda.Aberta,
            AbertaEm = agora,
            FechadaEm = null,
            TotalCentavos = 0
        };
        _comandas.Add(comanda);
        return comanda;
    }

    public ComandaComItens AdicionarItem(int comandaId, Produto produto, int quantidade, DateTime agora)
    {
        var comanda = _comandas.FirstOrDefault(c => c.Id == comandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new ComandaNaoAbertaException();
        }

        var itens = _itens.Where(i => i.ComandaId == comandaId).ToList();
        var itemExistente = itens.FirstOrDefault(i => i.ProdutoId == produto.Id && i.PrecoUnitarioCentavos == produto.PrecoCentavos);

        if (itemExistente is not null)
        {
            itemExistente.Quantidade += quantidade;
            itemExistente.SubtotalCentavos = itemExistente.PrecoUnitarioCentavos * itemExistente.Quantidade;
            itemExistente.AtualizadoEm = agora;
        }
        else
        {
            var novoItem = new ItemComanda
            {
                Id = _proximoItemId++,
                ComandaId = comandaId,
                ProdutoId = produto.Id,
                NomeProduto = produto.Nome,
                PrecoUnitarioCentavos = produto.PrecoCentavos,
                Quantidade = quantidade,
                SubtotalCentavos = produto.PrecoCentavos * quantidade,
                CriadoEm = agora,
                AtualizadoEm = agora
            };
            _itens.Add(novoItem);
            itens.Add(novoItem);
        }

        comanda.TotalCentavos = itens.Sum(i => i.SubtotalCentavos);
        return new ComandaComItens { Comanda = comanda, Itens = itens };
    }

    public ComandaComItens AlterarQuantidade(int itemId, int quantidade, DateTime agora)
    {
        var item = _itens.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Item não encontrado.");
        var comanda = _comandas.FirstOrDefault(c => c.Id == item.ComandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new ComandaNaoAbertaException();
        }

        item.Quantidade = quantidade;
        item.SubtotalCentavos = item.PrecoUnitarioCentavos * quantidade;
        item.AtualizadoEm = agora;

        var itens = _itens.Where(i => i.ComandaId == comanda.Id).ToList();
        comanda.TotalCentavos = itens.Sum(i => i.SubtotalCentavos);
        return new ComandaComItens { Comanda = comanda, Itens = itens };
    }

    public ComandaComItens RemoverItem(int itemId, DateTime agora)
    {
        var item = _itens.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Item não encontrado.");
        var comanda = _comandas.FirstOrDefault(c => c.Id == item.ComandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new ComandaNaoAbertaException();
        }

        _itens.Remove(item);
        var itensRestantes = _itens.Where(i => i.ComandaId == comanda.Id).ToList();
        comanda.TotalCentavos = itensRestantes.Sum(i => i.SubtotalCentavos);
        return new ComandaComItens { Comanda = comanda, Itens = itensRestantes };
    }

    public Comanda CancelarComanda(int comandaId, DateTime agora)
    {
        var comanda = _comandas.FirstOrDefault(c => c.Id == comandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new ComandaNaoAbertaException();
        }

        comanda.Status = StatusComanda.Cancelada;
        comanda.FechadaEm = agora;
        return comanda;
    }

    private readonly List<Venda> _vendas = new();
    private int _proximoVendaId = 1;

    public Venda EncerrarComanda(int comandaId, DateTime agora)
    {
        var comanda = _comandas.FirstOrDefault(c => c.Id == comandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new ComandaNaoAbertaException();
        }

        var proximoNumero = (_vendas.Count == 0 ? 0 : _vendas.Max(v => v.Numero)) + 1;
        var venda = new Venda
        {
            Id = _proximoVendaId++,
            ComandaId = comanda.Id,
            Numero = proximoNumero,
            TotalCentavos = comanda.TotalCentavos,
            FinalizadaEm = agora,
            Status = StatusVenda.Concluida
        };
        _vendas.Add(venda);

        comanda.Status = StatusComanda.Fechada;
        comanda.FechadaEm = agora;
        return venda;
    }
}
