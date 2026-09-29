namespace VarthexComanda.Desktop.Atendimento;

public class ComandaSlotItem
{
    public required int Numero { get; init; }
    public required bool Aberta { get; init; }
    public int? ComandaId { get; init; }
    public string? NomeCliente { get; init; }
    public required string TotalFormatado { get; init; }
    public required string TempoFormatado { get; init; }
}
