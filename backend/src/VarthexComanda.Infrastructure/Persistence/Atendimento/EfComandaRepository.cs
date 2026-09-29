using Microsoft.EntityFrameworkCore;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Atendimento;

public class EfComandaRepository : IComandaRepository
{
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabricaContexto;

    public EfComandaRepository(IDbContextFactory<VarthexComandaDbContext> fabricaContexto)
    {
        _fabricaContexto = fabricaContexto;
    }

    public IReadOnlyList<Comanda> ListarAbertas()
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        return contexto.Comandas
            .Where(c => c.Status == StatusComanda.Aberta)
            .OrderBy(c => c.Numero)
            .ToList();
    }

    public ComandaComItens? BuscarComItens(int comandaId)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var comanda = contexto.Comandas.SingleOrDefault(c => c.Id == comandaId);
        if (comanda is null)
        {
            return null;
        }

        var itens = contexto.ItensComanda
            .Where(i => i.ComandaId == comandaId)
            .OrderBy(i => i.Id)
            .ToList();
        return new ComandaComItens { Comanda = comanda, Itens = itens };
    }

    public Comanda AbrirComanda(int numero, DateTime agora)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var comanda = new Comanda
        {
            Id = 0,
            Numero = numero,
            Status = StatusComanda.Aberta,
            AbertaEm = agora,
            FechadaEm = null,
            TotalCentavos = 0
        };
        contexto.Comandas.Add(comanda);
        try
        {
            contexto.SaveChanges();
        }
        catch (DbUpdateException)
        {
            throw new NumeroComandaOcupadoException();
        }
        return comanda;
    }

    public Comanda DefinirNomeCliente(int comandaId, string? nome)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var comanda = contexto.Comandas.SingleOrDefault(c => c.Id == comandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta) throw new ComandaNaoAbertaException();
        comanda.NomeCliente = nome;
        contexto.SaveChanges();
        return comanda;
    }

    public ComandaComItens AdicionarItem(int comandaId, Produto produto, int quantidade, DateTime agora)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var comanda = contexto.Comandas.SingleOrDefault(c => c.Id == comandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new ComandaNaoAbertaException();
        }

        var itens = contexto.ItensComanda.Where(i => i.ComandaId == comandaId).OrderBy(i => i.Id).ToList();
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
                Id = 0,
                ComandaId = comandaId,
                ProdutoId = produto.Id,
                NomeProduto = produto.Nome,
                PrecoUnitarioCentavos = produto.PrecoCentavos,
                Quantidade = quantidade,
                SubtotalCentavos = produto.PrecoCentavos * quantidade,
                CriadoEm = agora,
                AtualizadoEm = agora
            };
            contexto.ItensComanda.Add(novoItem);
            itens.Add(novoItem);
        }

        comanda.TotalCentavos = itens.Sum(i => i.SubtotalCentavos);
        contexto.SaveChanges();

        return new ComandaComItens { Comanda = comanda, Itens = itens };
    }

    public ComandaComItens AlterarQuantidade(int itemId, int quantidade, DateTime agora)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var item = contexto.ItensComanda.SingleOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Item não encontrado.");
        var comanda = contexto.Comandas.SingleOrDefault(c => c.Id == item.ComandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new ComandaNaoAbertaException();
        }

        item.Quantidade = quantidade;
        item.SubtotalCentavos = item.PrecoUnitarioCentavos * quantidade;
        item.AtualizadoEm = agora;

        var itens = contexto.ItensComanda.Where(i => i.ComandaId == comanda.Id).OrderBy(i => i.Id).ToList();
        comanda.TotalCentavos = itens.Sum(i => i.SubtotalCentavos);
        contexto.SaveChanges();

        return new ComandaComItens { Comanda = comanda, Itens = itens };
    }

    public ComandaComItens RemoverItem(int itemId, DateTime agora)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var item = contexto.ItensComanda.SingleOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("Item não encontrado.");
        var comanda = contexto.Comandas.SingleOrDefault(c => c.Id == item.ComandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new ComandaNaoAbertaException();
        }

        contexto.ItensComanda.Remove(item);

        var itensRestantes = contexto.ItensComanda
            .Where(i => i.ComandaId == comanda.Id && i.Id != itemId)
            .OrderBy(i => i.Id)
            .ToList();
        comanda.TotalCentavos = itensRestantes.Sum(i => i.SubtotalCentavos);
        contexto.SaveChanges();

        return new ComandaComItens { Comanda = comanda, Itens = itensRestantes };
    }

    public Comanda CancelarComanda(int comandaId, DateTime agora)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var comanda = contexto.Comandas.SingleOrDefault(c => c.Id == comandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new ComandaNaoAbertaException();
        }

        comanda.Status = StatusComanda.Cancelada;
        comanda.FechadaEm = agora;
        contexto.SaveChanges();
        return comanda;
    }

    public Venda EncerrarComanda(int comandaId, DateTime agora)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var comanda = contexto.Comandas.SingleOrDefault(c => c.Id == comandaId)
            ?? throw new InvalidOperationException("Comanda não encontrada.");
        if (comanda.Status != StatusComanda.Aberta)
        {
            throw new ComandaNaoAbertaException();
        }

        var proximoNumero = (contexto.Vendas.Max(v => (int?)v.Numero) ?? 0) + 1;
        var venda = new Venda
        {
            Id = 0,
            ComandaId = comanda.Id,
            Numero = proximoNumero,
            TotalCentavos = comanda.TotalCentavos,
            FinalizadaEm = agora,
            Status = StatusVenda.Concluida
        };
        contexto.Vendas.Add(venda);

        comanda.Status = StatusComanda.Fechada;
        comanda.FechadaEm = agora;

        contexto.SaveChanges();
        return venda;
    }
}
