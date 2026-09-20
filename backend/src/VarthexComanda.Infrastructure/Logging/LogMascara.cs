using System.Text.RegularExpressions;

namespace VarthexComanda.Infrastructure.Logging;

// RF26, docs/08: logs nao carregam o caminho do perfil do usuario do Windows.
public static class LogMascara
{
    public const string Substituto = "%USERPROFILE%";

    public static string Aplicar(string texto, string? perfilUsuario = null)
    {
        if (string.IsNullOrEmpty(texto))
            return texto;

        perfilUsuario ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var partes = perfilUsuario.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 0)
            return texto;

        // Aceita \ ou / entre os segmentos, ignora maiusculas e nao casa "maria2" quando o perfil e "maria".
        var padrao = string.Join(@"[\\/]", partes.Select(Regex.Escape)) + @"(?![\p{L}\p{N}_])";
        return Regex.Replace(texto, padrao, Substituto, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
