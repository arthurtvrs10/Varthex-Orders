using Microsoft.EntityFrameworkCore;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Domain;

namespace VarthexComanda.Infrastructure.Persistence.Atendimento;

public class EfVendaRepository : IVendaRepository
{
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabricaContexto;

    public EfVendaRepository(IDbContextFactory<VarthexComandaDbContext> fabricaContexto)
    {
        _fabricaContexto = fabricaContexto;
    }

    public IReadOnlyList<VendaResumo> ListarPorData(DateTime inicioUtc, DateTime fimUtc)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        return contexto.Vendas
            .Where(v => v.FinalizadaEm >= inicioUtc && v.FinalizadaEm < fimUtc && v.Status == StatusVenda.Concluida)
            .Join(contexto.Comandas, v => v.ComandaId, c => c.Id, (v, c) => new VendaResumo { Venda = v, NumeroComanda = c.Numero, NomeCliente = c.NomeCliente })
            .OrderBy(vr => vr.Venda.FinalizadaEm)
            .ToList();
    }

    public IReadOnlyList<ItemComanda>? BuscarItensDaVenda(int vendaId)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        var venda = contexto.Vendas.SingleOrDefault(v => v.Id == vendaId);
        if (venda is null)
        {
            return null;
        }

        return contexto.ItensComanda
            .Where(i => i.ComandaId == venda.ComandaId)
            .OrderBy(i => i.Id)
            .ToList();
    }
}
