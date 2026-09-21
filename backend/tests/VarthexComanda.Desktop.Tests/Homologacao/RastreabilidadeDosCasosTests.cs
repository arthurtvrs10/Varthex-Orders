using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Homologacao;

// Rastreabilidade executavel dos casos de aceitacao CT01-CT22 (docs/docs/09-testes-aceitacao.md).
//
// Percorre por reflexao os QUATRO assemblies de teste, coleta [Trait("Caso", "CTnn")] de cada
// [Fact]/[Theory] (no metodo ou na classe) e monta o mapa CT -> testes. O teste FALHA quando:
//   - um caso automatizavel do catalogo abaixo fica sem nenhum teste;
//   - um Trait "Caso" tem valor fora de CT01-CT22 (erro de digitacao);
//   - um caso catalogado como "manual" ganha teste (o catalogo precisa ser atualizado).
//
// Onde este teste mora: em Desktop.Tests, o unico projeto de teste que compila com referencia aos
// outros tres SEM artificios. Application.Tests e Domain.Tests sao net10.0 e Infrastructure.Tests
// tambem, e Desktop.Tests e net10.0-windows: um projeto net10.0 nao pode referenciar um
// net10.0-windows, entao o inverso (hospedar em Infrastructure.Tests e enxergar Desktop.Tests)
// exigiria carregar DLLs por caminho ou mudar o TargetFramework. Em Desktop.Tests basta uma
// ProjectReference normal para cada um dos outros projetos de teste.
//
// Tabela Markdown: se a variavel de ambiente VARTHEX_GERAR_RASTREABILIDADE tiver um caminho, o
// teste grava la o arquivo (UTF-8 sem BOM, fim de linha CRLF). Por padrao NAO escreve em lugar
// nenhum.
public class RastreabilidadeDosCasosTests
{
    public const string VariavelDeGeracao = "VARTHEX_GERAR_RASTREABILIDADE";

    public enum Tipo { Automatizado, Estrutural, Manual }

    public sealed record Caso(string Codigo, string Cenario, string Criterio, Tipo Tipo, string Nota);

    // Catalogo dos 22 casos. "Tipo" e "Nota" dizem com honestidade o que a cobertura automatizada
    // prova de fato; o restante (ensaio no app real, PC da loja) fica descrito na nota.
    public static readonly IReadOnlyList<Caso> Catalogo = new[]
    {
        new Caso("CT01", "Abrir comanda livre", "Número fica ocupado e comanda fica `ABERTA`", Tipo.Automatizado,
            "Caso de uso (repositório em memória), repositório EF sobre SQLite real e ViewModel."),
        new Caso("CT02", "Impedir duplicidade", "Segunda abertura do mesmo número é recusada", Tipo.Automatizado,
            "A recusa está no caso de uso e no índice único parcial do SQLite; a tela, em vez de exibir erro, seleciona a comanda já aberta."),
        new Caso("CT03", "Adicionar produto", "Duas inclusões de R$ 18,00 resultam em quantidade 2 e subtotal R$ 36,00", Tipo.Automatizado,
            "Inclui teste com os valores literais (R$ 18,00) sobre SQLite real."),
        new Caso("CT04", "Preservar preço", "Alterar produto para R$ 22,00 não muda item lançado por R$ 18,00", Tipo.Automatizado,
            "Ponta a ponta com SQLite real: item, total e histórico da venda mantêm R$ 18,00."),
        new Caso("CT05", "Corrigir quantidade", "Reduzir 3 para 2 recalcula e persiste subtotal e total", Tipo.Automatizado,
            "A persistência é conferida relendo por contexto novo."),
        new Caso("CT06", "Recusar quantidade inválida", "Valor negativo ou fracionário não é gravado", Tipo.Automatizado,
            "Negativo/zero: caso de uso e restrição CHECK do banco. Fracionário: apenas por construção (a quantidade é `int` em toda a cadeia e a tela só tem + e −); não há campo de digitação para testar."),
        new Caso("CT07", "Recusar comanda vazia", "Nenhuma venda é criada", Tipo.Automatizado,
            "Caso de uso, ViewModel e restrição do banco (venda de total zero é recusada)."),
        new Caso("CT08", "Exibir total", "Itens de R$ 18,00, R$ 18,00 e R$ 6,00 mostram total R$ 42,00", Tipo.Automatizado,
            "Total calculado e persistido com os valores literais; a exibição em tela é conferida no ViewModel (sem teste visual de pixel)."),
        new Caso("CT09", "Falha na cobrança externa", "Voltar mantém comanda aberta e não cria venda", Tipo.Automatizado,
            "ViewModel de encerramento (em memória e sobre SQLite real). A maquininha de verdade é ensaio manual."),
        new Caso("CT10", "Encerrar após cobrança manual", "Cria venda e fecha comanda sem dados de pagamento", Tipo.Automatizado,
            "\"Sem dados de pagamento\" é conferido no esquema real do banco (nenhuma coluna de pagamento)."),
        new Caso("CT11", "Rollback", "Falha entre venda e fechamento desfaz tudo", Tipo.Automatizado,
            "Injeção real de falha (interceptor de comandos do EF) no meio do `SaveChanges` do encerramento; nada é persistido."),
        new Caso("CT12", "Histórico", "Itens, total e horário correspondem ao encerramento", Tipo.Automatizado,
            "Horário conferido ao tick; itens e total relidos pelo repositório do histórico."),
        new Caso("CT13", "Operar offline", "Cadastro, comanda, total, encerramento e histórico funcionam sem rede", Tipo.Estrutural,
            "Só verificação estrutural: os quatro assemblies do produto não referenciam APIs de rede. Não substitui operar de fato sem rede (ensaio manual)."),
        new Caso("CT14", "Criar backup", "Cópia abre, passa na integridade e possui checksum", Tipo.Automatizado,
            "Serviço real de backup sobre SQLite real."),
        new Caso("CT15", "Rejeitar backup corrompido", "Base ativa permanece inalterada", Tipo.Automatizado,
            "Backup real com bytes corrompidos: caso de uso recusa sem nenhuma escrita na base ativa (hash idêntico)."),
        new Caso("CT16", "Atualizar aplicação", "Migrações completam e totais anteriores permanecem", Tipo.Automatizado,
            "Banco criado só até a `InitialCreate`, com dados por SQL, migrado até a última com backup preventivo; totais, vendas e comanda aberta preservados."),
        new Caso("CT17", "Navegar por teclado", "É possível localizar, adicionar e iniciar encerramento", Tipo.Automatizado,
            "Parcial: ViewModel e XAML (atalhos, foco, Enter/Esc). A prova com teclado físico real é ensaio manual."),
        new Caso("CT18", "Resumo diário", "Quatro vendas totalizando R$ 120,00 geram ticket médio R$ 30,00", Tipo.Automatizado,
            "ViewModel do histórico sobre SQLite real, com os valores literais."),
        new Caso("CT19", "Impedir segunda instância", "Com o aplicativo aberto, nova execução exibe aviso e não abre outra conexão com a base", Tipo.Automatizado,
            "Parcial: teste unitário da trava (mutex). A prova com o segundo executável (aviso na tela e nenhuma segunda conexão) é ensaio."),
        new Caso("CT20", "Recuperar comandas abertas", "Após término forçado, a reabertura mostra os mesmos itens e totais sem criar venda", Tipo.Automatizado,
            "Nível de repositório: reabre o SQLite real. O término forçado do processo do app é ensaio."),
        new Caso("CT21", "Instalar no Windows", "Pacote autocontido inicia em instalação limpa sem exigir SDK ou runtime separado", Tipo.Manual,
            "Sem teste automatizado: exige um Windows limpo (procedimento em docs/homologacao)."),
        new Caso("CT22", "Controlar logs", "Rotação remove arquivos além da retenção e respeita o limite de armazenamento configurado", Tipo.Automatizado,
            "Rotação por tamanho e retenção de N arquivos com o configurador real de log."),
    };

    private static readonly Regex FormatoDoCodigo = new("^CT(0[1-9]|1[0-9]|2[0-2])$", RegexOptions.Compiled);

    public sealed record TesteDoCaso(string Projeto, string Classe, string Metodo)
    {
        public string Nome => $"{Classe}.{Metodo}";
    }

    // Os quatro assemblies de teste, por nome: se algum nao estiver ao alcance, Assembly.Load lanca
    // e o teste falha (em vez de a rastreabilidade ficar silenciosamente incompleta).
    private static readonly string[] AssembliesDeTeste =
    {
        "VarthexComanda.Domain.Tests",
        "VarthexComanda.Application.Tests",
        "VarthexComanda.Infrastructure.Tests",
        "VarthexComanda.Desktop.Tests",
    };

    public static IReadOnlyDictionary<string, List<TesteDoCaso>> ColetarMapa(IEnumerable<Assembly> assemblies, List<string>? valoresInvalidos = null, Func<Type, bool>? incluirTipo = null)
    {
        var mapa = new SortedDictionary<string, List<TesteDoCaso>>(StringComparer.Ordinal);
        foreach (var assembly in assemblies)
        {
            var projeto = assembly.GetName().Name!;
            foreach (var tipo in assembly.GetTypes().Where(t => incluirTipo?.Invoke(t) ?? true))
            {
                var casosDaClasse = CasosDe(HierarquiaDe(tipo).SelectMany(t => CustomAttributeData.GetCustomAttributes(t)));
                foreach (var metodo in tipo.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    var atributos = CustomAttributeData.GetCustomAttributes(metodo);
                    if (!atributos.Any(EhFactOuTheoryAtivo)) continue;

                    foreach (var caso in casosDaClasse.Concat(CasosDe(atributos)).Distinct())
                    {
                        if (!FormatoDoCodigo.IsMatch(caso))
                        {
                            valoresInvalidos?.Add($"{projeto}: {tipo.Name}.{metodo.Name} -> \"{caso}\"");
                            continue;
                        }
                        if (!mapa.TryGetValue(caso, out var lista)) mapa[caso] = lista = new List<TesteDoCaso>();
                        lista.Add(new TesteDoCaso(projeto, tipo.Name, metodo.Name));
                    }
                }
            }
        }
        foreach (var lista in mapa.Values) lista.Sort((a, b) => string.CompareOrdinal(a.Nome, b.Nome));
        return mapa;
    }

    private static IEnumerable<Type> HierarquiaDe(Type tipo)
    {
        for (var t = tipo; t is not null && t != typeof(object); t = t.BaseType) yield return t;
    }

    private static bool EhFactOuTheoryAtivo(CustomAttributeData atributo)
    {
        if (!typeof(FactAttribute).IsAssignableFrom(atributo.AttributeType)) return false;
        // [Fact(Skip = "...")] nao conta como cobertura
        return !atributo.NamedArguments.Any(a => a.MemberName == nameof(FactAttribute.Skip) && !string.IsNullOrEmpty(a.TypedValue.Value as string));
    }

    private static IEnumerable<string> CasosDe(IEnumerable<CustomAttributeData> atributos) =>
        atributos
            .Where(a => a.AttributeType == typeof(TraitAttribute)
                        && a.ConstructorArguments.Count == 2
                        && (a.ConstructorArguments[0].Value as string) == "Caso")
            .Select(a => (a.ConstructorArguments[1].Value as string) ?? string.Empty);

    /// <summary>Problemas de cobertura: casos automatizaveis sem teste, e casos manuais que ganharam teste.</summary>
    public static List<string> Verificar(IReadOnlyDictionary<string, List<TesteDoCaso>> mapa, IEnumerable<Caso> catalogo)
    {
        var problemas = new List<string>();
        foreach (var caso in catalogo)
        {
            var quantidade = mapa.TryGetValue(caso.Codigo, out var lista) ? lista.Count : 0;
            if (caso.Tipo != Tipo.Manual && quantidade == 0)
            {
                problemas.Add($"{caso.Codigo} ({caso.Cenario}) ficou sem nenhum teste com [Trait(\"Caso\", \"{caso.Codigo}\")].");
            }
            if (caso.Tipo == Tipo.Manual && quantidade > 0)
            {
                problemas.Add($"{caso.Codigo} esta catalogado como manual mas ja tem {quantidade} teste(s): atualize o catalogo (Tipo/Nota).");
            }
        }
        return problemas;
    }

    private static IReadOnlyDictionary<string, List<TesteDoCaso>> MapaDaSuite(List<string>? valoresInvalidos = null) =>
        ColetarMapa(
            AssembliesDeTeste.Select(nome => Assembly.Load(new AssemblyName(nome))),
            valoresInvalidos,
            tipo => tipo != typeof(AmostraDeTestes)); // a amostra do teste do scanner nao e teste real

    [Fact]
    public void Catalogo_TemExatamenteCT01AteCT22EmOrdem()
    {
        Assert.Equal(
            Enumerable.Range(1, 22).Select(n => $"CT{n:00}"),
            Catalogo.Select(c => c.Codigo));
    }

    [Fact]
    public void TodoCasoAutomatizavel_TemAoMenosUmTeste_ETraitsSaoValidos()
    {
        var invalidos = new List<string>();
        var mapa = MapaDaSuite(invalidos);

        Assert.True(invalidos.Count == 0, "Trait \"Caso\" com valor invalido:\n" + string.Join("\n", invalidos));
        var problemas = Verificar(mapa, Catalogo);
        Assert.True(problemas.Count == 0, "Rastreabilidade quebrada:\n" + string.Join("\n", problemas));

        var destino = Environment.GetEnvironmentVariable(VariavelDeGeracao);
        if (!string.IsNullOrWhiteSpace(destino))
        {
            var pasta = Path.GetDirectoryName(Path.GetFullPath(destino));
            if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);
            File.WriteAllText(destino, GerarMarkdown(mapa, Catalogo), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }

    // O guarda tem de FALHAR de verdade quando um caso perde todos os testes: mapa sintetico sem CT05.
    [Fact]
    public void Verificar_FalhaQuandoUmCasoObrigatorioPerdeTodosOsTestes()
    {
        var mapa = MapaDaSuite().ToDictionary(p => p.Key, p => p.Value);
        Assert.True(mapa.Remove("CT05"));

        var problemas = Verificar(mapa, Catalogo);

        var problema = Assert.Single(problemas);
        Assert.StartsWith("CT05 ", problema);
    }

    [Fact]
    public void Verificar_CasoManualComTeste_PedeAtualizacaoDoCatalogo()
    {
        var mapa = new Dictionary<string, List<TesteDoCaso>> { ["CT21"] = new() { new TesteDoCaso("P", "C", "M") } };
        var catalogo = new[] { new Caso("CT21", "x", "y", Tipo.Manual, "z") };

        Assert.Single(Verificar(mapa, catalogo));
    }

    [Fact]
    public void ColetarMapa_IgnoraFactSkipEDetectaTraitInvalido()
    {
        var invalidos = new List<string>();

        var mapa = ColetarMapa(new[] { typeof(AmostraDeTestes).Assembly }, invalidos, tipo => tipo == typeof(AmostraDeTestes));

        // Trait em Fact ativo, em Theory e herdado da classe contam; Fact com Skip nao conta
        Assert.Contains(mapa["CT22"], t => t.Metodo == nameof(AmostraDeTestes.Ativo));
        Assert.Contains(mapa["CT22"], t => t.Metodo == nameof(AmostraDeTestes.ComTeoria));
        Assert.DoesNotContain(mapa["CT22"], t => t.Metodo == nameof(AmostraDeTestes.Ignorado));
        Assert.Contains(invalidos, i => i.Contains("CT99x"));
    }

    private static string EscaparCelula(string texto) => texto.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

    public static string GerarMarkdown(IReadOnlyDictionary<string, List<TesteDoCaso>> mapa, IEnumerable<Caso> catalogo)
    {
        var casos = catalogo.ToList();
        var linhas = new List<string>
        {
            "# Rastreabilidade dos casos de teste (CT01–CT22)",
            "",
            "> Gerado por `RastreabilidadeDosCasosTests`; não editar à mão.",
            $"> Regenerar: defina `{VariavelDeGeracao}` com o caminho deste arquivo e rode `dotnet test backend/tests/VarthexComanda.Desktop.Tests --filter RastreabilidadeDosCasosTests`.",
            "",
            "Cada caso de `docs/docs/09-testes-aceitacao.md` aparece com os testes automatizados marcados com",
            "`[Trait(\"Caso\", \"CTnn\")]`. A coluna **Tipo** diz o que a cobertura automatizada prova:",
            "`automatizado`, `estrutural` (prova por construção do código, não do comportamento em execução)",
            "ou `manual` (sem teste automatizado). A **Nota** registra os limites; o que sobra (ensaio no app real,",
            "no computador da loja) está descrito em `docs/homologacao/`.",
            "",
            "| Caso | Cenário | Critério de aceitação | Tipo | Testes | Nota |",
            "| --- | --- | --- | --- | ---: | --- |",
        };

        foreach (var caso in casos)
        {
            var quantidade = mapa.TryGetValue(caso.Codigo, out var lista) ? lista.Count : 0;
            linhas.Add(
                $"| {caso.Codigo} | {EscaparCelula(caso.Cenario)} | {EscaparCelula(caso.Criterio)} | {TipoComoTexto(caso.Tipo)} | {quantidade} | {EscaparCelula(caso.Nota)} |");
        }

        linhas.Add("");
        linhas.Add("## Testes por caso");
        foreach (var caso in casos)
        {
            linhas.Add("");
            linhas.Add($"### {caso.Codigo} — {caso.Cenario} ({TipoComoTexto(caso.Tipo)})");
            linhas.Add("");
            if (!mapa.TryGetValue(caso.Codigo, out var lista) || lista.Count == 0)
            {
                linhas.Add(caso.Tipo == Tipo.Manual ? "_Sem teste automatizado (manual)._" : "_Nenhum teste._");
                continue;
            }
            foreach (var teste in lista)
            {
                linhas.Add($"- `{teste.Nome}` ({teste.Projeto.Replace("VarthexComanda.", string.Empty)})");
            }
        }

        return string.Join("\r\n", linhas) + "\r\n";
    }

    private static string TipoComoTexto(Tipo tipo) => tipo switch
    {
        Tipo.Automatizado => "automatizado",
        Tipo.Estrutural => "estrutural",
        _ => "manual",
    };

    // Amostra usada so por ColetarMapa_IgnoraFactSkipEDetectaTraitInvalido. E privada (o xUnit nao a
    // executa) e MapaDaSuite a exclui, entao ela nao interfere na rastreabilidade real.
#pragma warning disable xUnit1000 // classe privada de proposito: so o scanner a le, o xUnit nunca a executa
    [Trait("Caso", "CT22")]
    private sealed class AmostraDeTestes
    {
        [Fact] public void Ativo() { }

        [Theory, InlineData(1)] public void ComTeoria(int _) { }

        [Fact(Skip = "amostra")] public void Ignorado() { }

        [Fact, Trait("Caso", "CT99x")] public void TraitInvalido() { }
    }
}
