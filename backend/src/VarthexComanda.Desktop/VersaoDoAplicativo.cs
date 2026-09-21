using System.Reflection;

namespace VarthexComanda.Desktop;

/// <summary>Versão do aplicativo (a mesma do executável), exibida em Configurações e registrada no log.</summary>
public static class VersaoDoAplicativo
{
    public static string Atual { get; } = Ler();

    private static string Ler()
    {
        var assembly = typeof(VersaoDoAplicativo).Assembly;
        var informacional = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informacional))
        {
            return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        // o SDK anexa "+hash" da revisao; a versao exibida e so a parte antes do "+"
        var indiceMais = informacional.IndexOf('+');
        return indiceMais >= 0 ? informacional[..indiceMais] : informacional;
    }
}
