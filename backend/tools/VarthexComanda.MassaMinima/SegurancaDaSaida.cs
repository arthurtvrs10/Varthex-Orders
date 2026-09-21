namespace VarthexComanda.MassaMinima;

/// <summary>A pasta de saida foi recusada (dados reais, raiz de disco, pasta pessoal, pasta nao vazia...).</summary>
public sealed class RecusaDeMassaException : Exception
{
    public RecusaDeMassaException(string mensagem) : base(mensagem) { }
}

/// <summary>
/// Protecao da pasta de saida. A parte de <see cref="MotivoDeRecusa"/> e feita SOMENTE com comparacao
/// de caminhos canonicos (sem tocar no disco), para poder recusar a pasta real dos dados sem le-la.
/// </summary>
public static class SegurancaDaSaida
{
    /// <summary>Pasta real dos dados do aplicativo (%LOCALAPPDATA%\VarthexComanda) — usada apenas como texto.</summary>
    public static string PastaRealDosDados()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(local) ? string.Empty : Path.Combine(local, "VarthexComanda");
    }

    /// <summary>Devolve o motivo da recusa (texto para o operador) ou null se o caminho pode ser usado.</summary>
    public static string? MotivoDeRecusa(string? pastaRaiz)
    {
        if (string.IsNullOrWhiteSpace(pastaRaiz))
        {
            return "Informe a pasta de saida (--saida <pasta>).";
        }

        string completo;
        try
        {
            completo = Canonico(pastaRaiz);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return "A pasta de saida informada nao e um caminho valido.";
        }

        var real = PastaRealDosDados();
        if (real.Length > 0)
        {
            var protegida = Canonico(real);
            if (Igual(completo, protegida) || DentroDe(completo, protegida))
            {
                return "Recusado: a pasta de saida e (ou esta dentro de) a pasta real dos dados do aplicativo " +
                       "(%LOCALAPPDATA%\\VarthexComanda). A massa minima so pode ser gerada numa pasta descartavel.";
            }
            if (DentroDe(protegida, completo))
            {
                return "Recusado: a pasta de saida contem a pasta real dos dados do aplicativo " +
                       "(%LOCALAPPDATA%\\VarthexComanda). Escolha uma pasta descartavel propria.";
            }
        }

        var raiz = Path.GetPathRoot(completo);
        if (!string.IsNullOrEmpty(raiz) && Igual(completo, Canonico(raiz)))
        {
            return "Recusado: a pasta de saida e a raiz de um disco. Escolha uma subpasta descartavel.";
        }

        var perfil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(perfil) && Igual(completo, Canonico(perfil)))
        {
            return "Recusado: a pasta de saida e a pasta pessoal do usuario. Escolha uma subpasta descartavel.";
        }

        return null;
    }

    /// <summary>
    /// Valida a saida e devolve o caminho canonico a usar. Lanca <see cref="RecusaDeMassaException"/> ANTES de
    /// qualquer acesso ao disco quando o caminho e proibido; so entao consulta a pasta (vazia? atalho?).
    /// </summary>
    public static string ValidarOuRecusar(string? pastaRaiz, bool forcar)
    {
        var motivo = MotivoDeRecusa(pastaRaiz);
        if (motivo is not null)
        {
            throw new RecusaDeMassaException(motivo);
        }

        var completo = Canonico(pastaRaiz!);

        if (File.Exists(completo))
        {
            throw new RecusaDeMassaException("Recusado: a saida indicada e um arquivo, nao uma pasta.");
        }

        if (Directory.Exists(completo))
        {
            // junction/link simbolico apontando para uma pasta proibida
            var alvo = new DirectoryInfo(completo).ResolveLinkTarget(returnFinalTarget: true);
            if (alvo is not null)
            {
                var motivoDoAlvo = MotivoDeRecusa(alvo.FullName);
                if (motivoDoAlvo is not null)
                {
                    throw new RecusaDeMassaException(motivoDoAlvo);
                }
            }

            if (Directory.EnumerateFileSystemEntries(completo).Any() && !forcar)
            {
                throw new RecusaDeMassaException(
                    "Recusado: a pasta de saida nao esta vazia. Use uma pasta nova ou vazia, " +
                    "ou --forcar para apagar os subdiretorios data, logs, backups e fotos dela e gerar de novo.");
            }
        }

        return completo;
    }

    private static string Canonico(string caminho) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(caminho));

    private static bool Igual(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool DentroDe(string caminho, string pai) =>
        caminho.StartsWith(Path.TrimEndingDirectorySeparator(pai) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
