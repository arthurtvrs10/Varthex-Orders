using System.Text.RegularExpressions;

namespace VarthexComanda.MassaMinima;

/// <summary>A pasta de saida foi recusada (dados reais, raiz de disco, pasta pessoal, pasta nao vazia...).</summary>
public sealed class RecusaDeMassaException : Exception
{
    public RecusaDeMassaException(string mensagem) : base(mensagem) { }
}

/// <summary>
/// Protecao da pasta de saida. A parte de <see cref="MotivoDeRecusa"/> e feita SOMENTE com comparacao
/// de caminhos canonicos (sem ler o conteudo do disco), para poder recusar a pasta real dos dados sem le-la.
/// <see cref="ValidarOuRecusar"/> so consulta o disco depois de o caminho literal ter passado (marcador,
/// pasta vazia, junctions/links em qualquer componente do caminho).
/// </summary>
public static class SegurancaDaSaida
{
    /// <summary>
    /// Marcador gravado na raiz de toda pasta gerada por esta ferramenta. Sem ele, nada e apagado (--forcar
    /// so reaproveita pastas que a propria ferramenta criou).
    /// </summary>
    public const string NomeDoMarcador = ".varthex-massa-minima";

    private const string NomeDaPastaDeDados = "VarthexComanda";

    // alias 8.3 de "VarthexComanda": 6 primeiras letras + ~N (ex.: VARTHE~1)
    private static readonly Regex AliasCurtoDaPastaDeDados = new("^VARTHE~[0-9]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Pasta real dos dados do aplicativo (%LOCALAPPDATA%\VarthexComanda) — usada apenas como texto.</summary>
    public static string PastaRealDosDados()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(local) ? string.Empty : Path.Combine(local, NomeDaPastaDeDados);
    }

    /// <summary>
    /// Pastas pessoais do usuario que nunca podem ser a raiz da saida (nem conte-las): Documentos, Area de Trabalho,
    /// Imagens, Musicas, Videos e Downloads. Subpastas delas continuam permitidas. So texto, sem tocar no disco.
    /// </summary>
    public static IReadOnlyList<string> PastasPessoais()
    {
        var perfil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var lista = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
        };
        if (!string.IsNullOrWhiteSpace(perfil))
        {
            lista.Add(perfil);
            lista.Add(Path.Combine(perfil, "Downloads"));
        }
        return lista.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Devolve o motivo da recusa (texto para o operador) ou null se o caminho pode ser usado.
    /// Os parametros opcionais existem para os testes injetarem pastas "protegidas" falsas; em uso normal
    /// valem a pasta real dos dados e as pastas pessoais do usuario.
    /// </summary>
    public static string? MotivoDeRecusa(string? pastaRaiz, string? pastaRealDosDados = null, IEnumerable<string>? pastasPessoais = null)
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

        var real = pastaRealDosDados ?? PastaRealDosDados();
        if (real.Length > 0)
        {
            var protegida = Canonico(real);
            var comparavel = ExpandirAliasCurto(completo, protegida);
            if (Igual(comparavel, protegida) || DentroDe(comparavel, protegida))
            {
                return "Recusado: a pasta de saida e (ou esta dentro de) a pasta real dos dados do aplicativo " +
                       "(%LOCALAPPDATA%\\VarthexComanda). A massa minima so pode ser gerada numa pasta descartavel.";
            }
            if (DentroDe(protegida, comparavel))
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

        // pastas pessoais (Documentos, Area de Trabalho, Imagens, Musicas, Videos, Downloads, perfil): a propria
        // pasta e as que a contem (pais/raiz) sao recusadas; subpastas delas sao permitidas
        foreach (var pessoal in pastasPessoais ?? PastasPessoais())
        {
            if (string.IsNullOrWhiteSpace(pessoal)) continue;
            var canonica = Canonico(pessoal);
            if (Igual(completo, canonica) || DentroDe(canonica, completo))
            {
                return "Recusado: a pasta de saida e uma pasta pessoal do usuario (Documentos, Area de Trabalho, Imagens, " +
                       "Musicas, Videos, Downloads ou o perfil) ou contem uma delas. Escolha uma subpasta descartavel nova.";
            }
        }

        return null;
    }

    /// <summary>
    /// Valida a saida e devolve o caminho canonico a usar. Lanca <see cref="RecusaDeMassaException"/> ANTES de
    /// qualquer acesso ao disco quando o caminho literal e proibido; so entao consulta o disco (junctions/links em
    /// qualquer componente do caminho, pasta vazia, marcador).
    /// </summary>
    public static string ValidarOuRecusar(string? pastaRaiz, bool forcar, string? pastaRealDosDados = null, IEnumerable<string>? pastasPessoais = null)
    {
        var motivo = MotivoDeRecusa(pastaRaiz, pastaRealDosDados, pastasPessoais);
        if (motivo is not null)
        {
            throw new RecusaDeMassaException(motivo);
        }

        var completo = Canonico(pastaRaiz!);

        if (File.Exists(completo))
        {
            throw new RecusaDeMassaException("Recusado: a saida indicada e um arquivo, nao uma pasta.");
        }

        var motivoPorLink = MotivoPorLinks(completo, pastaRealDosDados, pastasPessoais);
        if (motivoPorLink is not null)
        {
            throw new RecusaDeMassaException(motivoPorLink);
        }

        if (Directory.Exists(completo) && Directory.EnumerateFileSystemEntries(completo).Any())
        {
            var geradaPorEstaFerramenta = File.Exists(Path.Combine(completo, NomeDoMarcador));
            if (!geradaPorEstaFerramenta)
            {
                throw new RecusaDeMassaException(MensagemDePastaNaoGerada);
            }
            if (!forcar)
            {
                throw new RecusaDeMassaException(
                    "Recusado: a pasta de saida nao esta vazia (ja tem uma massa gerada). Use uma pasta nova ou vazia, " +
                    "ou --forcar para apagar os subdiretorios data, logs, backups e fotos dela e gerar de novo.");
            }
        }

        return completo;
    }

    /// <summary>
    /// Confirma que a pasta pode ser limpa (so quando o marcador existe); a limpeza em si tambem chama isto,
    /// como segunda barreira.
    /// </summary>
    public static void ExigirMarcadorParaLimpar(string raiz)
    {
        if (!File.Exists(Path.Combine(raiz, NomeDoMarcador)))
        {
            throw new RecusaDeMassaException(MensagemDePastaNaoGerada);
        }
    }

    public const string MensagemDePastaNaoGerada =
        "Recusado: a pasta de saida nao esta vazia e nao foi criada por esta ferramenta (falta o marcador " +
        NomeDoMarcador + "), entao nada sera apagado nela. Use uma pasta nova e vazia.";

    // Junction/link simbolico em QUALQUER componente do caminho: cada prefixo existente e resolvido e o caminho
    // resultante passa pelas mesmas recusas do literal.
    private static string? MotivoPorLinks(string completo, string? pastaRealDosDados, IEnumerable<string>? pastasPessoais)
    {
        var resto = new List<string>(); // componentes ainda nao resolvidos, do mais proximo da raiz ao mais fundo
        var atual = completo;
        while (atual is not null)
        {
            try
            {
                if (Directory.Exists(atual))
                {
                    var alvo = new DirectoryInfo(atual).ResolveLinkTarget(returnFinalTarget: true);
                    if (alvo is not null)
                    {
                        var resolvido = alvo.FullName;
                        foreach (var componente in resto) resolvido = Path.Combine(resolvido, componente);
                        var motivo = MotivoDeRecusa(resolvido, pastaRealDosDados, pastasPessoais);
                        if (motivo is not null)
                        {
                            return motivo + " (o caminho informado passa por um atalho/junction que leva a essa pasta.)";
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // prefixo que nao da para inspecionar: segue para o pai
            }

            var nome = Path.GetFileName(atual);
            if (nome.Length > 0) resto.Insert(0, nome);
            atual = Path.GetDirectoryName(atual);
        }
        return null;
    }

    // "%LOCALAPPDATA%\VARTHE~1\..." e o mesmo lugar que "%LOCALAPPDATA%\VarthexComanda\...": troca so o componente
    // do alias curto (comparacao textual; o runtime ja expande nomes 8.3 de pastas existentes em GetFullPath)
    private static string ExpandirAliasCurto(string completo, string protegida)
    {
        var pai = Path.GetDirectoryName(protegida);
        if (pai is null || !DentroDe(completo, pai)) return completo;

        var relativo = completo[(pai.Length + 1)..];
        var primeiro = relativo.Split(Path.DirectorySeparatorChar, 2)[0];
        if (!AliasCurtoDaPastaDeDados.IsMatch(primeiro)) return completo;

        return Path.Combine(pai, NomeDaPastaDeDados) + relativo[primeiro.Length..];
    }

    private static string Canonico(string caminho) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(caminho));

    private static bool Igual(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool DentroDe(string caminho, string pai) =>
        caminho.StartsWith(Path.TrimEndingDirectorySeparator(pai) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
