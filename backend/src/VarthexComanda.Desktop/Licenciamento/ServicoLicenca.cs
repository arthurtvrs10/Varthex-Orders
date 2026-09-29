using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
using VarthexComanda.Application.Licenciamento;

namespace VarthexComanda.Desktop.Licenciamento;

public sealed class ServicoLicenca
{
    private sealed record Estado(string Chave, DateTimeOffset UltimoUso);
    private readonly string _arquivo;
    private readonly string _publica;
    public string Computador { get; }

    internal ServicoLicenca(string arquivo, string publica, string computador)
    {
        _arquivo = arquivo;
        _publica = publica;
        Computador = computador;
    }

    public ServicoLicenca()
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        using var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
        var id = key?.GetValue("MachineGuid") as string;
        if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Não foi possível identificar este computador.");
        Computador = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("VarthexComanda:" + id)));
        _arquivo = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VarthexComanda", "licenca", "estado.dat");
        using var stream = typeof(ServicoLicenca).Assembly.GetManifestResourceStream("VarthexComanda.licenca-publica.pem")
            ?? throw new InvalidOperationException("Chave pública de licenciamento ausente.");
        using var reader = new StreamReader(stream);
        _publica = reader.ReadToEnd();
    }

    private Estado? Ler()
    {
        if (!File.Exists(_arquivo)) return null;
        try
        {
            var bytes = ProtectedData.Unprotect(File.ReadAllBytes(_arquivo), null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<Estado>(bytes) ?? throw new InvalidOperationException("Licença local inválida.");
        }
        catch (Exception e) when (e is CryptographicException or JsonException)
        { throw new InvalidOperationException("O registro de licença está danificado. Contate o suporte."); }
    }

    public DadosLicenca Verificar(string? novaChave = null)
    {
        var estado = Ler();
        var chave = novaChave?.Trim() ?? estado?.Chave ?? throw new InvalidOperationException("Informe sua chave para ativar o aplicativo.");
        var agora = DateTimeOffset.UtcNow;
        var dados = LicencaOffline.Validar(chave, _publica, Computador, agora, estado?.UltimoUso);
        var ultimo = estado is not null && estado.UltimoUso > agora ? estado.UltimoUso : agora;
        Directory.CreateDirectory(Path.GetDirectoryName(_arquivo)!);
        var bytes = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(new Estado(chave, ultimo)), null, DataProtectionScope.CurrentUser);
        var temporario = _arquivo + ".tmp";
        File.WriteAllBytes(temporario, bytes);
        File.Move(temporario, _arquivo, true);
        return dados;
    }
}
