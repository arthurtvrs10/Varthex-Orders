using VarthexComanda.MassaMinima;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Homologacao;

// Seguranca do gerador da massa minima (Etapa 8): marcador de pasta gerada, pastas pessoais, alias 8.3 e
// junctions. Nada aqui gera massa nem toca na pasta real: os caminhos reais aparecem so como TEXTO e as
// recusas acontecem antes de qualquer E/S; o cenario de junction usa uma "pasta protegida" falsa e temporaria.
public sealed class MassaMinimaSegurancaTests : IDisposable
{
    private static readonly DateTime Agora = new(2026, 9, 21, 15, 0, 0, DateTimeKind.Utc);
    private readonly List<string> _pastas = new();

    public void Dispose()
    {
        foreach (var pasta in _pastas)
        {
            try { Directory.Delete(pasta, recursive: true); } catch { /* limpeza best-effort */ }
        }
    }

    private string NovaPasta()
    {
        var pasta = Path.Combine(Path.GetTempPath(), $"varthex-massa-seg-{Guid.NewGuid():N}");
        _pastas.Add(pasta);
        return pasta;
    }

    [Fact]
    public void Forcar_EmPastaNaoVaziaSemMarcador_NaoApagaNada()
    {
        var pasta = NovaPasta();
        Directory.CreateDirectory(Path.Combine(pasta, "data"));
        Directory.CreateDirectory(Path.Combine(pasta, "fotos"));
        File.WriteAllText(Path.Combine(pasta, "data", "importante.db"), "dados de outra coisa");
        File.WriteAllText(Path.Combine(pasta, "fotos", "minha.png"), "x");

        var ex = Assert.Throws<RecusaDeMassaException>(() => MassaMinimaGerador.Gerar(pasta, Agora, forcar: true));
        Assert.Contains("nao foi criada por esta ferramenta", ex.Message);
        Assert.Contains("pasta nova e vazia", ex.Message);
        Assert.DoesNotContain("--forcar", ex.Message);
        Assert.True(File.Exists(Path.Combine(pasta, "data", "importante.db")));
        Assert.True(File.Exists(Path.Combine(pasta, "fotos", "minha.png")));
        Assert.False(File.Exists(Path.Combine(pasta, SegurancaDaSaida.NomeDoMarcador))); // nem marca a pasta alheia

        // a segunda barreira (a propria limpeza) tambem exige o marcador
        Assert.Throws<RecusaDeMassaException>(() => SegurancaDaSaida.ExigirMarcadorParaLimpar(pasta));
    }

    [Fact]
    public void PastasPessoaisDoUsuario_SeusPaisERaizes_SaoRecusados_MasSubpastasNao()
    {
        var perfil = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var esperadas = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Path.Combine(perfil, "Downloads"),
        }.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();

        Assert.True(esperadas.Count >= 4);
        var conhecidas = SegurancaDaSaida.PastasPessoais();
        foreach (var pasta in esperadas)
        {
            Assert.Contains(conhecidas, c => string.Equals(c, pasta, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(SegurancaDaSaida.MotivoDeRecusa(pasta));                        // a propria pasta
            Assert.NotNull(SegurancaDaSaida.MotivoDeRecusa(pasta.ToUpperInvariant()));     // caixa diferente
            Assert.NotNull(SegurancaDaSaida.MotivoDeRecusa(Path.GetDirectoryName(pasta))); // o pai
            Assert.NotNull(SegurancaDaSaida.MotivoDeRecusa(Path.GetPathRoot(pasta)));      // a raiz do disco

            // as recusas acontecem antes de qualquer E/S: nada e criado nem lido nessas pastas
            Assert.Throws<RecusaDeMassaException>(() => MassaMinimaGerador.Gerar(pasta, Agora));
            Assert.Throws<RecusaDeMassaException>(() => MassaMinimaGerador.Gerar(pasta, Agora, forcar: true));

            // subpastas continuam permitidas (decisao textual; nada e criado)
            Assert.Null(SegurancaDaSaida.MotivoDeRecusa(Path.Combine(pasta, "massa-de-teste")));
        }
    }

    [Fact]
    public void AliasCurto8ponto3DaPastaReal_ERecusadoSemAcessoAoDisco()
    {
        // texto puro: se a pasta existir o runtime expande o alias; se nao, a comparacao textual o reconhece
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        foreach (var caminho in new[]
                 {
                     Path.Combine(local, "VARTHE~1"),
                     Path.Combine(local, "varthe~2", "data"),
                     Path.Combine(local, "VARTHE~1", "backups", "novo"),
                 })
        {
            Assert.NotNull(SegurancaDaSaida.MotivoDeRecusa(caminho));
            Assert.Throws<RecusaDeMassaException>(() => MassaMinimaGerador.Gerar(caminho, Agora, forcar: true));
        }
        Assert.Null(SegurancaDaSaida.MotivoDeRecusa(Path.Combine(local, "VARTHE~1x"))); // nao e alias
    }

    [Fact]
    public void JunctionComoComponenteDoCaminho_ApontandoParaPastaProtegida_ERecusada()
    {
        // "pasta protegida" falsa e descartavel injetada no lugar da real: o teste nunca usa %LOCALAPPDATA%\VarthexComanda
        var baseTemp = NovaPasta();
        var falsaReal = Path.Combine(baseTemp, "pretensa-pasta-real");
        Directory.CreateDirectory(falsaReal);
        var atalhoParaAPasta = Path.Combine(baseTemp, "atalho-para-a-pasta");
        var atalhoParaOPai = Path.Combine(baseTemp, "atalho-para-o-pai");
        var atalhoInocente = Path.Combine(baseTemp, "atalho-inocente");
        var outra = Path.Combine(baseTemp, "outra-pasta");
        Directory.CreateDirectory(outra);

        if (!CriarJunction(atalhoParaAPasta, falsaReal) || !CriarJunction(atalhoParaOPai, baseTemp) || !CriarJunction(atalhoInocente, outra))
        {
            Console.WriteLine("junction nao pode ser criada neste ambiente: teste ignorado");
            return; // ambiente sem permissao/suporte a junctions
        }

        try
        {
            // literalmente nada aqui e a "pasta real"...
            Assert.Null(SegurancaDaSaida.MotivoDeRecusa(Path.Combine(atalhoParaAPasta, "sub", "nova"), falsaReal));

            // ...mas resolvendo o atalho em qualquer componente (mesmo com o resto do caminho ainda inexistente), sim
            Assert.Throws<RecusaDeMassaException>(() => SegurancaDaSaida.ValidarOuRecusar(atalhoParaAPasta, false, falsaReal));
            Assert.Throws<RecusaDeMassaException>(() =>
                SegurancaDaSaida.ValidarOuRecusar(Path.Combine(atalhoParaAPasta, "sub", "nova"), true, falsaReal));
            // atalho para um PAI da pasta protegida: atalho\pretensa-pasta-real\x cai dentro dela
            Assert.Throws<RecusaDeMassaException>(() =>
                SegurancaDaSaida.ValidarOuRecusar(Path.Combine(atalhoParaOPai, "pretensa-pasta-real", "x"), false, falsaReal));
            // atalho para o pai como a propria saida = pasta que CONTEM a protegida
            Assert.Throws<RecusaDeMassaException>(() => SegurancaDaSaida.ValidarOuRecusar(atalhoParaOPai, false, falsaReal));

            // controle: atalho para uma pasta qualquer continua aceito
            var destino = Path.Combine(atalhoInocente, "nova");
            Assert.Equal(Path.GetFullPath(destino), SegurancaDaSaida.ValidarOuRecusar(destino, false, falsaReal));
        }
        finally
        {
            foreach (var atalho in new[] { atalhoParaAPasta, atalhoParaOPai, atalhoInocente })
            {
                try { Directory.Delete(atalho); } catch { /* remove so o atalho, nunca o alvo */ }
            }
        }

        Assert.True(Directory.Exists(falsaReal)); // o alvo do atalho nao foi apagado
    }

    private static bool CriarJunction(string atalho, string alvo)
    {
        try
        {
            var info = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink /J \"{atalho}\" \"{alvo}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var processo = System.Diagnostics.Process.Start(info)!;
            processo.StandardOutput.ReadToEnd();
            processo.StandardError.ReadToEnd();
            processo.WaitForExit(15000);
            return processo.ExitCode == 0 && Directory.Exists(atalho);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
