using System.Globalization;
using VarthexComanda.Application.Atendimento;

namespace VarthexComanda.MassaMinima;

/// <summary>
/// Linha de comando do gerador: dotnet run --project backend/tools/VarthexComanda.MassaMinima -- --saida &lt;pasta&gt; [--forcar] [--agora &lt;UTC ISO-8601&gt;].
/// Codigos de saida: 0 = ok; 1 = uso incorreto/erro; 2 = pasta de saida recusada por seguranca.
/// </summary>
public static class LinhaDeComando
{
    public static int Executar(string[] args, TextWriter saida, TextWriter erro)
    {
        string? pasta = null;
        var forcar = false;
        DateTime? agora = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--saida" when i + 1 < args.Length:
                    pasta = args[++i];
                    break;
                case "--forcar":
                    forcar = true;
                    break;
                case "--agora" when i + 1 < args.Length:
                    if (!DateTime.TryParse(args[++i], CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var valor))
                    {
                        return Falhar(erro, 1, "Valor invalido para --agora (use data/hora UTC, ex.: 2026-09-21T15:00:00Z).");
                    }
                    agora = valor;
                    break;
                case "--ajuda" or "-h" or "--help":
                    saida.WriteLine("Uso: VarthexComanda.MassaMinima --saida <pasta> [--forcar] [--agora <UTC ISO-8601>]");
                    saida.WriteLine("  Gera a massa minima de homologacao numa pasta descartavel (nunca na pasta real dos dados).");
                    return 0;
                default:
                    return Falhar(erro, 1, $"Argumento desconhecido ou incompleto: {args[i]}. Use --ajuda.");
            }
        }

        if (pasta is null)
        {
            return Falhar(erro, 1, "Informe a pasta de saida: --saida <pasta>. Use --ajuda.");
        }

        try
        {
            var resumo = MassaMinimaGerador.Gerar(pasta, agora ?? DateTime.UtcNow, forcar);
            Imprimir(resumo, saida);
            return 0;
        }
        catch (RecusaDeMassaException ex)
        {
            return Falhar(erro, 2, ex.Message);
        }
        catch (Exception ex)
        {
            return Falhar(erro, 1, "Falha ao gerar a massa minima: " + ex.Message);
        }
    }

    static int Falhar(TextWriter erro, int codigo, string mensagem)
    {
        erro.WriteLine(mensagem);
        return codigo;
    }

    static void Imprimir(ResumoMassa r, TextWriter saida)
    {
        var pt = CultureInfo.GetCultureInfo("pt-BR");
        string Reais(long centavos) => (centavos / 100m).ToString("C", pt);

        saida.WriteLine("Massa minima gerada.");
        saida.WriteLine($"  Pasta:               {r.PastaRaiz}");
        saida.WriteLine($"  Referencia (agora):  {FusoBrasilia.ParaLocal(r.AgoraUtc):dd/MM/yyyy HH:mm} (Brasilia) = {r.AgoraUtc:yyyy-MM-dd HH:mm:ss} UTC");
        saida.WriteLine($"  Categorias:          {r.Categorias} ({r.CategoriasInativas} inativa)");
        saida.WriteLine($"  Produtos:            {r.Produtos} ({r.ProdutosInativos} inativo; {r.ProdutosComFoto} com foto sintetica)");
        saida.WriteLine($"  Numeros de comanda:  {r.NumerosDeComanda} (configuracao)");
        saida.WriteLine($"  Comandas abertas:    {r.ComandasAbertas}");
        foreach (var p in r.PerfisDasAbertas)
        {
            saida.WriteLine($"    - comanda {p.Numero,2}: {p.Descricao} ({p.Linhas} linha(s), {p.Unidades} unidade(s), {Reais(p.TotalCentavos)})");
        }
        saida.WriteLine($"  Comandas abandonadas (canceladas): {r.ComandasCanceladas}");
        saida.WriteLine($"  Vendas:              {r.Vendas} ({r.ItensEmVendas} linhas de item), total {Reais(r.TotalVendidoCentavos)}");
        saida.WriteLine($"  Historico:           {r.DiasDeHistorico} dias ({r.DiasComVendas} dias com vendas)");
        saida.WriteLine("  Backups (pasta backups\\):");
        saida.WriteLine($"    - valido:     {r.BackupValido} (+ .sha256 e .fotos.zip)");
        saida.WriteLine($"    - antigo:     {r.BackupAntigo} (esquema da InitialCreate)");
        saida.WriteLine($"    - corrompido: {r.BackupCorrompido} (bytes invalidos; checksum nao confere)");
        saida.WriteLine();
        saida.WriteLine("Para usar com o app (ensaios): defina VARTHEX_COMANDA_DADOS com a pasta acima.");
    }
}
