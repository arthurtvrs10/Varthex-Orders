using System.Text.RegularExpressions;

namespace VarthexComanda.Infrastructure.Logging;

// RF26, docs/08: logs nao carregam o caminho do perfil do usuario do Windows.
public static class LogMascara
{
    public const string Substituto = "%USERPROFILE%";

    public static string Aplicar(string texto, string? perfilUsuario = null) =>
        CriarMascara(perfilUsuario)(texto);

    // Resolve o perfil e monta a regex uma unica vez; o resultado e reutilizavel por evento.
    public static Func<string, string> CriarMascara(string? perfilUsuario = null)
    {
        perfilUsuario ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var partes = perfilUsuario.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length == 0)
            return texto => texto;

        // Entre segmentos aceita sequencias de \ ou / (cobre caminho com barra escapada/dobrada), ignora
        // maiusculas e exige que o perfil termine ali: nao casa "maria2" nem "maria.silva" para o perfil "maria".
        var padrao = string.Join(@"[\\/]+", partes.Select(Regex.Escape))
            + @"(?![\p{L}\p{N}_])(?![.\-][\p{L}\p{N}_])";
        var regex = new Regex(padrao, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        return texto => string.IsNullOrEmpty(texto) ? texto : regex.Replace(texto, Substituto);
    }
}
