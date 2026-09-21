using System.Reflection;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence.Atendimento;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Homologacao;

// CT13 — criterio de docs/docs/09-testes-aceitacao.md: cadastro, comanda, total, encerramento e
// historico funcionam sem rede.
//
// ATENCAO, cobertura ESTRUTURAL (por construcao), nao um teste sem rede fisica: falha se qualquer
// dos quatro assemblies do produto passar a referenciar uma API capaz de falar pela rede. Isso
// prova que o codigo do produto nao TEM como depender de rede; a prova de operacao com a rede
// desligada de verdade continua sendo ensaio (docs/homologacao). Bibliotecas de terceiros
// (EF Core, Serilog, SQLite) nao sao analisadas: so os assemblies do proprio produto.
public class OperacaoOfflinePorConstrucaoTests
{
    // Assemblies de framework que dao acesso a rede (HTTP, sockets, WebSocket, WebClient/WebRequest,
    // e-mail, ping, servidor HTTP, DNS, TLS). System.Net.Primitives/NetworkInformation nao entram:
    // so tipos de dado (IPAddress etc.) ou consulta de interfaces, sem trafego.
    private static readonly string[] AssembliesDeRedeProibidos =
    {
        "System.Net.Http",
        "System.Net.Http.Json",
        "System.Net.Sockets",
        "System.Net.WebClient",
        "System.Net.WebSockets",
        "System.Net.WebSockets.Client",
        "System.Net.Requests",
        "System.Net.HttpListener",
        "System.Net.Mail",
        "System.Net.Ping",
        "System.Net.Security",
        "System.Net.NameResolution",
        "System.Net.ServicePoint",
        "System.Net.WebProxy",
        "System.Net.WebHeaderCollection",
    };

    // O assembly do projeto Desktop chama-se "VarthexComanda" (VarthexComanda.dll).
    private static IReadOnlyDictionary<string, Assembly> AssembliesDoProduto() => new Dictionary<string, Assembly>
    {
        ["VarthexComanda.Domain"] = typeof(Comanda).Assembly,
        ["VarthexComanda.Application"] = typeof(AbrirComanda).Assembly,
        ["VarthexComanda.Infrastructure"] = typeof(EfComandaRepository).Assembly,
        ["VarthexComanda"] = typeof(AtendimentoViewModel).Assembly,
    };

    private static List<string> ReferenciasDeRede(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(r => r.Name!)
            .Where(r => AssembliesDeRedeProibidos.Contains(r, StringComparer.OrdinalIgnoreCase))
            .ToList();

    [Fact]
    [Trait("Caso", "CT13")]
    public void CT13_AssembliesDoProduto_NaoReferenciamApisDeRede()
    {
        foreach (var (nome, assembly) in AssembliesDoProduto())
        {
            Assert.Equal(nome, assembly.GetName().Name);
            Assert.NotEmpty(assembly.GetReferencedAssemblies()); // controle: a leitura das referencias funcionou

            var proibidos = ReferenciasDeRede(assembly);
            Assert.True(proibidos.Count == 0, $"{nome} referencia API de rede: {string.Join(", ", proibidos)}");
        }
    }

    // Controle do proprio detector (nao e caso de aceitacao): este assembly de teste USA HttpClient,
    // entao tem de ser flagrado; senao o teste acima poderia passar sem enxergar nada.
    [Fact]
    public void Detector_FlagraAssemblyQueUsaHttpClient()
    {
        Assert.Equal("HttpClient", typeof(HttpClient).Name); // uso real do tipo: o compilador emite a referencia

        Assert.Contains("System.Net.Http", ReferenciasDeRede(typeof(OperacaoOfflinePorConstrucaoTests).Assembly));
    }
}
