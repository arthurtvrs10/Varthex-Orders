using System.IO.Compression;

namespace VarthexComanda.MassaMinima;

/// <summary>
/// Gera fotos sinteticas (PNG RGB 8 bits) sem depender de System.Drawing: um degrade diagonal com a
/// inicial do produto em fonte bitmap 5x7. Nada de imagens de terceiros; a saida e deterministica.
/// </summary>
public static class PngSintetico
{
    public const int Lado = 160;

    private static readonly Dictionary<char, string[]> Fonte = CriarFonte();

    public static byte[] Gerar(char inicial, (byte R, byte G, byte B) corInicio, (byte R, byte G, byte B) corFim)
    {
        var letra = Fonte.TryGetValue(char.ToUpperInvariant(inicial), out var glifo) ? glifo : Fonte['?'];
        const int escala = 12;                 // 5x7 -> 60x84
        var origemX = (Lado - 5 * escala) / 2;
        var origemY = (Lado - 7 * escala) / 2;

        // cada linha do PNG comeca com o byte de filtro 0
        var bruto = new byte[Lado * (1 + Lado * 3)];
        for (var y = 0; y < Lado; y++)
        {
            var linha = y * (1 + Lado * 3);
            bruto[linha] = 0;
            for (var x = 0; x < Lado; x++)
            {
                var t = (x + y) / (2.0 * (Lado - 1));
                var r = (byte)(corInicio.R + (corFim.R - corInicio.R) * t);
                var g = (byte)(corInicio.G + (corFim.G - corInicio.G) * t);
                var b = (byte)(corInicio.B + (corFim.B - corInicio.B) * t);

                if (EhTraco(letra, x - origemX, y - origemY, escala))
                {
                    (r, g, b) = (255, 255, 255);
                }
                else if (EhTraco(letra, x - origemX - 4, y - origemY - 4, escala))
                {
                    (r, g, b) = ((byte)(r / 2), (byte)(g / 2), (byte)(b / 2)); // sombra
                }

                var p = linha + 1 + x * 3;
                bruto[p] = r;
                bruto[p + 1] = g;
                bruto[p + 2] = b;
            }
        }

        using var saida = new MemoryStream();
        saida.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var cabecalho = new byte[13];
        EscreverInt32(cabecalho, 0, Lado);
        EscreverInt32(cabecalho, 4, Lado);
        cabecalho[8] = 8;   // profundidade
        cabecalho[9] = 2;   // truecolor RGB
        EscreverBloco(saida, "IHDR", cabecalho);

        using (var comprimido = new MemoryStream())
        {
            using (var zlib = new ZLibStream(comprimido, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(bruto);
            }
            EscreverBloco(saida, "IDAT", comprimido.ToArray());
        }

        EscreverBloco(saida, "IEND", []);
        return saida.ToArray();
    }

    private static bool EhTraco(string[] glifo, int x, int y, int escala)
    {
        if (x < 0 || y < 0 || x >= 5 * escala || y >= 7 * escala) return false;
        return glifo[y / escala][x / escala] == '#';
    }

    private static void EscreverBloco(Stream saida, string tipo, byte[] dados)
    {
        var tamanho = new byte[4];
        EscreverInt32(tamanho, 0, dados.Length);
        saida.Write(tamanho);

        var corpo = new byte[4 + dados.Length];
        for (var i = 0; i < 4; i++) corpo[i] = (byte)tipo[i];
        dados.CopyTo(corpo, 4);
        saida.Write(corpo);

        var crc = new byte[4];
        EscreverInt32(crc, 0, unchecked((int)Crc32(corpo)));
        saida.Write(crc);
    }

    private static void EscreverInt32(byte[] destino, int indice, int valor)
    {
        destino[indice] = (byte)(valor >> 24);
        destino[indice + 1] = (byte)(valor >> 16);
        destino[indice + 2] = (byte)(valor >> 8);
        destino[indice + 3] = (byte)valor;
    }

    private static readonly uint[] TabelaCrc = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    private static uint Crc32(ReadOnlySpan<byte> dados)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in dados) c = TabelaCrc[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }

    private static Dictionary<char, string[]> CriarFonte()
    {
        var d = new Dictionary<char, string[]>();
        void Add(char c, params string[] linhas) =>
            d[c] = linhas.Select(l => l.Replace('1', '#').Replace('0', '.')).ToArray();

        Add('A', "01110", "10001", "10001", "11111", "10001", "10001", "10001");
        Add('B', "11110", "10001", "10001", "11110", "10001", "10001", "11110");
        Add('C', "01110", "10001", "10000", "10000", "10000", "10001", "01110");
        Add('D', "11110", "10001", "10001", "10001", "10001", "10001", "11110");
        Add('E', "11111", "10000", "10000", "11110", "10000", "10000", "11111");
        Add('F', "11111", "10000", "10000", "11110", "10000", "10000", "10000");
        Add('G', "01110", "10001", "10000", "10111", "10001", "10001", "01111");
        Add('H', "10001", "10001", "10001", "11111", "10001", "10001", "10001");
        Add('I', "01110", "00100", "00100", "00100", "00100", "00100", "01110");
        Add('J', "00111", "00010", "00010", "00010", "00010", "10010", "01100");
        Add('K', "10001", "10010", "10100", "11000", "10100", "10010", "10001");
        Add('L', "10000", "10000", "10000", "10000", "10000", "10000", "11111");
        Add('M', "10001", "11011", "10101", "10101", "10001", "10001", "10001");
        Add('N', "10001", "11001", "10101", "10011", "10001", "10001", "10001");
        Add('O', "01110", "10001", "10001", "10001", "10001", "10001", "01110");
        Add('P', "11110", "10001", "10001", "11110", "10000", "10000", "10000");
        Add('Q', "01110", "10001", "10001", "10001", "10101", "10010", "01101");
        Add('R', "11110", "10001", "10001", "11110", "10100", "10010", "10001");
        Add('S', "01111", "10000", "10000", "01110", "00001", "00001", "11110");
        Add('T', "11111", "00100", "00100", "00100", "00100", "00100", "00100");
        Add('U', "10001", "10001", "10001", "10001", "10001", "10001", "01110");
        Add('V', "10001", "10001", "10001", "10001", "10001", "01010", "00100");
        Add('W', "10001", "10001", "10001", "10101", "10101", "11011", "10001");
        Add('X', "10001", "10001", "01010", "00100", "01010", "10001", "10001");
        Add('Y', "10001", "10001", "01010", "00100", "00100", "00100", "00100");
        Add('Z', "11111", "00001", "00010", "00100", "01000", "10000", "11111");
        Add('?', "01110", "10001", "00001", "00110", "00100", "00000", "00100");
        return d;
    }
}
