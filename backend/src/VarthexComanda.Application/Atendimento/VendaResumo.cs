using VarthexComanda.Domain;

namespace VarthexComanda.Application.Atendimento;

public class VendaResumo
{
    public required Venda Venda { get; init; }
    public required int NumeroComanda { get; init; }
    public string? NomeCliente { get; init; }
}
