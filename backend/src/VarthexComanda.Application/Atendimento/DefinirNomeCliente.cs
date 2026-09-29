using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Atendimento;

public sealed class DefinirNomeCliente(IComandaRepository comandas)
{
    public Resultado<Comanda> Executar(int comandaId, string? nome)
    {
        nome = string.IsNullOrWhiteSpace(nome) ? null : nome.Trim();
        if (nome?.Length > 80 || nome?.Any(char.IsControl) == true)
            return Resultado<Comanda>.Falha("Informe um nome com até 80 caracteres, sem quebras de linha.");
        try { return Resultado<Comanda>.Ok(comandas.DefinirNomeCliente(comandaId, nome)); }
        catch (InvalidOperationException ex) { return Resultado<Comanda>.Falha(ex.Message); }
        catch (ComandaNaoAbertaException ex) { return Resultado<Comanda>.Falha(ex.Message); }
    }
}
