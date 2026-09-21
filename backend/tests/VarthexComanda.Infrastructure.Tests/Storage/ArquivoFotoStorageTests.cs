using Serilog;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Infrastructure.Storage;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Storage;

public class ArquivoFotoStorageTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), $"varthex-fotos-tests-{Guid.NewGuid()}");
    private readonly AppPaths _paths;
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();

    public ArquivoFotoStorageTests()
    {
        _paths = new AppPaths(_raiz);
        _paths.EnsureCreated();
    }

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
        {
            Directory.Delete(_raiz, true);
        }
    }

    private string CriarArquivoOrigem(string nome, int bytes = 16)
    {
        var caminho = Path.Combine(_raiz, nome);
        var cabecalho = Path.GetExtension(nome).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => new byte[] { 0xFF, 0xD8, 0xFF },
            ".png" => new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A },
            ".bmp" => new byte[] { 0x42, 0x4D },
            _ => Array.Empty<byte>(),
        };
        var conteudo = new byte[Math.Max(bytes, cabecalho.Length)];
        cabecalho.CopyTo(conteudo, 0);
        File.WriteAllBytes(caminho, conteudo);
        return caminho;
    }

    private string[] ArquivosNaPastaDeFotos() =>
        Directory.Exists(_paths.FotosDirectory) ? Directory.GetFiles(_paths.FotosDirectory) : Array.Empty<string>();

    [Fact]
    public void Importar_ArquivoValido_CopiaParaPastaDeFotosERetornaNome()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var origem = CriarArquivoOrigem("foto.png");

        var nome = storage.Importar(origem);

        Assert.EndsWith(".png", nome);
        Assert.True(File.Exists(Path.Combine(_paths.FotosDirectory, nome)));
        Assert.True(File.Exists(origem));
    }

    [Fact]
    public void Importar_ExtensaoMaiuscula_NomeGuardadoEmMinusculas()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var origem = CriarArquivoOrigem("FOTO.JPG");

        var nome = storage.Importar(origem);

        Assert.EndsWith(".jpg", nome);
    }

    [Fact]
    public void Importar_MesmoArquivoDuasVezes_GeraNomesDiferentes()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var origem = CriarArquivoOrigem("foto.png");

        var primeiro = storage.Importar(origem);
        var segundo = storage.Importar(origem);

        Assert.NotEqual(primeiro, segundo);
    }

    [Fact]
    public void Importar_ExtensaoNaoSuportada_LancaFotoInvalida()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var origem = CriarArquivoOrigem("documento.pdf");

        var excecao = Assert.Throws<FotoInvalidaException>(() => storage.Importar(origem));

        Assert.Equal("Formato não suportado. Use JPG, PNG ou BMP.", excecao.Message);
    }

    [Fact]
    public void Importar_ArquivoInexistente_LancaFotoInvalida()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);

        var excecao = Assert.Throws<FotoInvalidaException>(() => storage.Importar(Path.Combine(_raiz, "nao-existe.png")));

        Assert.Equal("Arquivo de imagem não encontrado.", excecao.Message);
    }

    [Fact]
    public void Importar_ArquivoMaiorQueOLimite_LancaFotoInvalida()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger, tamanhoMaximoBytes: 8);
        var origem = CriarArquivoOrigem("grande.png", bytes: 16);

        var excecao = Assert.Throws<FotoInvalidaException>(() => storage.Importar(origem));

        Assert.Contains("excede o tamanho máximo", excecao.Message);
    }

    [Fact]
    public void Importar_ArquivoComConteudoQueNaoEImagem_LancaFotoInvalidaSemCopiar()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var origem = Path.Combine(_raiz, "falsa.png");
        File.WriteAllBytes(origem, new byte[] { 1, 2, 3, 4 });
        var antes = ArquivosNaPastaDeFotos();

        var excecao = Assert.Throws<FotoInvalidaException>(() => storage.Importar(origem));

        Assert.Equal("Não foi possível ler a imagem. Escolha outro arquivo.", excecao.Message);
        Assert.Equal(antes, ArquivosNaPastaDeFotos());
    }

    [Fact]
    public void Importar_ArquivoVazio_LancaFotoInvalida()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var origem = Path.Combine(_raiz, "vazia.jpg");
        File.WriteAllBytes(origem, Array.Empty<byte>());

        var excecao = Assert.Throws<FotoInvalidaException>(() => storage.Importar(origem));

        Assert.Equal("Não foi possível ler a imagem. Escolha outro arquivo.", excecao.Message);
        Assert.Empty(ArquivosNaPastaDeFotos());
    }

    [Theory]
    [InlineData("foto.jpeg", ".jpeg")]
    [InlineData("foto.bmp", ".bmp")]
    public void Importar_ArquivoJpegEBmpValidos_Aceita(string nomeOrigem, string extensaoEsperada)
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var origem = CriarArquivoOrigem(nomeOrigem);

        var nome = storage.Importar(origem);

        Assert.EndsWith(extensaoEsperada, nome);
        Assert.True(File.Exists(Path.Combine(_paths.FotosDirectory, nome)));
    }

    [Fact]
    public void Excluir_ArquivoExistente_Remove()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var nome = storage.Importar(CriarArquivoOrigem("foto.png"));

        storage.Excluir(nome);

        Assert.False(File.Exists(Path.Combine(_paths.FotosDirectory, nome)));
    }

    [Fact]
    public void Excluir_ArquivoInexistente_NaoLanca()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);

        var excecao = Record.Exception(() => storage.Excluir("nao-existe.png"));

        Assert.Null(excecao);
    }

    [Fact]
    public void Excluir_NomeComTravessiaDeDiretorio_UsaSoONomeDoArquivo()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var foraDaPasta = CriarArquivoOrigem("fora.jpg");

        storage.Excluir("..\\fora.jpg");

        Assert.True(File.Exists(foraDaPasta));
    }

    [Theory]
    [InlineData("a.jpg", true)]
    [InlineData("A.JPEG", true)]
    [InlineData("b.png", true)]
    [InlineData("c.BMP", true)]
    [InlineData("d.txt", false)]
    [InlineData("e.png.exe", false)]
    [InlineData("semextensao", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ExtensaoPermitida_SegueAWhitelistDeImagens(string? nome, bool esperado)
    {
        Assert.Equal(esperado, ArquivoFotoStorage.ExtensaoPermitida(nome));
    }
}
