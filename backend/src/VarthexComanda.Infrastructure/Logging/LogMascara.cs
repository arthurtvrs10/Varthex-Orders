using System.Text.RegularExpressions;

namespace VarthexComanda.Infrastructure.Logging;

// RF26, docs/08: logs nao carregam o caminho do perfil do usuario do Windows
// (nem as pastas de dados/backup externo registradas em PastasMascaradas).
public static class LogMascara
{
    public const string Substituto = "%USERPROFILE%";

    private static readonly char[] Separadores = ['\\', '/'];

    public static string Aplicar(string texto, string? perfilUsuario = null, PastasMascaradas? pastas = null) =>
        CriarMascara(perfilUsuario, pastas)(texto);

    // Resolve o perfil e monta a regex uma unica vez; o resultado e reutilizavel por evento.
    // As pastas registradas (mais especificas) sao mascaradas ANTES do perfil, para que uma pasta
    // dentro do perfil nao deixe o nome da subpasta no log.
    public static Func<string, string> CriarMascara(string? perfilUsuario = null, PastasMascaradas? pastas = null)
    {
        perfilUsuario ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var regexPerfil = CriarRegexDeCaminho(perfilUsuario);

        return texto =>
        {
            if (string.IsNullOrEmpty(texto))
                return texto;
            if (pastas is not null)
                texto = pastas.Aplicar(texto);
            return regexPerfil is null ? texto : regexPerfil.Replace(texto, Substituto);
        };
    }

    // Entre segmentos aceita sequencias de \ ou / (cobre caminho com barra escapada/dobrada), ignora
    // maiusculas e exige que o caminho termine ali: nao casa "maria2" nem "maria.silva" para "maria".
    // Caminho UNC (\\servidor\pasta) tambem casa o \\ inicial. Devolve null se nao houver segmentos.
    internal static Regex? CriarRegexDeCaminho(string caminho)
    {
        var partes = caminho.Split(Separadores, StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 0)
            return null;

        var inicioUnc = caminho.StartsWith(@"\\", StringComparison.Ordinal) || caminho.StartsWith("//", StringComparison.Ordinal);
        var padrao = (inicioUnc ? @"[\\/]{2}" : string.Empty)
            + string.Join(@"[\\/]+", partes.Select(Regex.Escape))
            + @"(?![\p{L}\p{N}_])(?![.\-][\p{L}\p{N}_])";
        return new Regex(padrao, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    internal static int ContarSegmentos(string caminho) =>
        caminho.Split(Separadores, StringSplitOptions.RemoveEmptyEntries).Length;
}
