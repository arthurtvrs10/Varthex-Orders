using System.Globalization;
using Serilog;
using VarthexComanda.Application.Catalogo;

namespace VarthexComanda.Infrastructure.Storage;

public class ArquivoFotoStorage : IFotoStorage
{
    public const long TamanhoMaximoPadraoBytes = 10L * 1024 * 1024;

    private static readonly HashSet<string> ExtensoesPermitidas =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp" };

    /// <summary>Whitelist de imagens (.jpg .jpeg .png .bmp) usada por importacao, backup e contagem de orfas.</summary>
    public static bool ExtensaoPermitida(string? nomeOuCaminho) =>
        !string.IsNullOrEmpty(nomeOuCaminho) && ExtensoesPermitidas.Contains(Path.GetExtension(nomeOuCaminho));

    private readonly AppPaths _paths;
    private readonly ILogger _logger;
    private readonly long _tamanhoMaximoBytes;

    public ArquivoFotoStorage(AppPaths paths, ILogger logger, long tamanhoMaximoBytes = TamanhoMaximoPadraoBytes)
    {
        _paths = paths;
        _logger = logger;
        _tamanhoMaximoBytes = tamanhoMaximoBytes;
    }

    public string Importar(string caminhoOrigem)
    {
        if (string.IsNullOrWhiteSpace(caminhoOrigem) || !File.Exists(caminhoOrigem))
        {
            throw new FotoInvalidaException("Arquivo de imagem não encontrado.");
        }

        var extensao = Path.GetExtension(caminhoOrigem);
        if (!ExtensoesPermitidas.Contains(extensao))
        {
            throw new FotoInvalidaException("Formato não suportado. Use JPG, PNG ou BMP.");
        }

        if (new FileInfo(caminhoOrigem).Length > _tamanhoMaximoBytes)
        {
            var megabytes = (_tamanhoMaximoBytes / 1024d / 1024d).ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));
            throw new FotoInvalidaException($"A imagem excede o tamanho máximo de {megabytes} MB.");
        }

        if (!TemCabecalhoDeImagem(caminhoOrigem))
        {
            throw new FotoInvalidaException("Não foi possível ler a imagem. Escolha outro arquivo.");
        }

        var nome = $"{Guid.NewGuid():N}{extensao.ToLowerInvariant()}";
        Directory.CreateDirectory(_paths.FotosDirectory);
        File.Copy(caminhoOrigem, Path.Combine(_paths.FotosDirectory, nome));
        return nome;
    }

    private static bool TemCabecalhoDeImagem(string caminho)
    {
        Span<byte> cabecalho = stackalloc byte[8];
        using var fluxo = File.OpenRead(caminho);
        var lidos = fluxo.Read(cabecalho);
        ReadOnlySpan<byte> lido = cabecalho[..lidos];
        return lido.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF })
            || lido.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })
            || lido.StartsWith(new byte[] { 0x42, 0x4D });
    }

    public void Excluir(string nomeArquivo)
    {
        if (string.IsNullOrWhiteSpace(nomeArquivo))
        {
            return;
        }

        var caminho = Path.Combine(_paths.FotosDirectory, Path.GetFileName(nomeArquivo));
        try
        {
            if (File.Exists(caminho))
            {
                File.Delete(caminho);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(ex, "Não foi possível excluir a foto {Arquivo}", nomeArquivo);
        }
    }
}
