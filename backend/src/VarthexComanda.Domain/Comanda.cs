namespace VarthexComanda.Domain;

public class Comanda
{
    public required int Id { get; set; }
    public required int Numero { get; set; }
    public required StatusComanda Status { get; set; }
    public required DateTime AbertaEm { get; set; }
    public DateTime? FechadaEm { get; set; }
    public required long TotalCentavos { get; set; }
    public string? Observacao { get; set; }
    public string? NomeCliente { get; set; }
}
