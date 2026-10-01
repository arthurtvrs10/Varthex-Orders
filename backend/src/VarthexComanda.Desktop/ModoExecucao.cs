using System.IO;

namespace VarthexComanda.Desktop;

public static class ModoExecucao
{
    public static bool Demonstracao(IEnumerable<string> argumentos, string pastaAplicativo) =>
        argumentos.Contains("--demonstracao", StringComparer.OrdinalIgnoreCase)
        || File.Exists(Path.Combine(pastaAplicativo, "edicao-demonstracao.txt"));

    // A demonstração nunca usa a variável de ambiente que possa apontar para dados reais.
    public static string? RaizDados(bool demonstracao, string? ambiente, string localAppData) =>
        demonstracao ? Path.Combine(localAppData, "VarthexComandaDemo")
            : string.IsNullOrWhiteSpace(ambiente) ? null : ambiente;
}
