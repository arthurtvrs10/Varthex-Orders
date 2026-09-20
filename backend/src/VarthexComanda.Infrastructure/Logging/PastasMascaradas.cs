using System.Text.RegularExpressions;

namespace VarthexComanda.Infrastructure.Logging;

/// <summary>
/// Pastas (alem do perfil do usuario) cujo caminho nao pode aparecer no log: a pasta de dados fora do
/// perfil (%DADOS%) e as pastas de backup externo (%PASTA_BACKUP_EXTERNA%).
/// A pasta externa so e conhecida em tempo de execucao, entao o registro e mutavel e thread-safe:
/// Adicionar (raro: uma vez por pasta nova) reconstroi um snapshot imutavel de regexes que Aplicar
/// (por evento de log) apenas le, sem lock. Quem usa uma pasta a registra antes de usa-la
/// (EfBackupService.CriarBackupExterno), entao nenhuma mensagem sobre ela chega ao log sem mascara.
/// </summary>
public sealed class PastasMascaradas
{
    public const string TokenDados = "%DADOS%";
    public const string TokenPastaBackupExterna = "%PASTA_BACKUP_EXTERNA%";

    private sealed record Entrada(string Caminho, string Token, Regex Regex, int Segmentos);

    private readonly object _trava = new();
    private volatile Entrada[] _entradas = [];

    /// <summary>Registra um caminho a ser trocado por <paramref name="token"/>. Idempotente. Ignora vazio e raiz de unidade ("D:\").</summary>
    public void Adicionar(string token, string? caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho))
            return;

        var segmentos = LogMascara.ContarSegmentos(caminho);
        var regex = LogMascara.CriarRegexDeCaminho(caminho);
        // "D:\" sozinho viraria mascarar todo "D:" do log; nao vale
        if (regex is null || segmentos < 2)
            return;

        lock (_trava)
        {
            if (_entradas.Any(e => string.Equals(e.Caminho, caminho, StringComparison.OrdinalIgnoreCase) && e.Token == token))
                return;

            // mais especifico (mais segmentos) primeiro: D:\dados\ext antes de D:\dados
            _entradas = _entradas
                .Append(new Entrada(caminho, token, regex, segmentos))
                .OrderByDescending(e => e.Segmentos)
                .ToArray();
        }
    }

    /// <summary>Registra a raiz de dados como %DADOS% somente quando ela esta FORA do perfil do usuario (dentro dele o %USERPROFILE% ja a cobre).</summary>
    public void AdicionarDados(string raiz, string? perfilUsuario = null)
    {
        perfilUsuario ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var regexPerfil = LogMascara.CriarRegexDeCaminho(perfilUsuario);
        // dentro do perfil: o casamento do perfil comeca no inicio da raiz
        if (regexPerfil?.Match(raiz) is { Success: true, Index: 0 })
            return;

        Adicionar(TokenDados, raiz);
    }

    public string Aplicar(string texto)
    {
        foreach (var entrada in _entradas)
            texto = entrada.Regex.Replace(texto, entrada.Token);
        return texto;
    }
}
