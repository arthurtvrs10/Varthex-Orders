using System.Security.Cryptography;
using System.Text.Json;

namespace VarthexComanda.Application.Licenciamento;

public sealed record DadosLicenca(int Versao, string Id, string Computador, string Cliente,
    DateTimeOffset Inicio, DateTimeOffset? Vencimento);

public static class LicencaOffline
{
    public static string Emitir(DadosLicenca dados, string chavePrivada)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(chavePrivada);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(dados);
        return Convert.ToBase64String(bytes) + "." + Convert.ToBase64String(
            rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    }

    public static DadosLicenca Validar(string chave, string chavePublica, string computador,
        DateTimeOffset agora, DateTimeOffset? ultimaUtilizacao = null)
    {
        if (chave.Length > 16384) throw new InvalidOperationException("Chave inválida.");
        DadosLicenca dados;
        try
        {
            var partes = chave.Trim().Split('.');
            if (partes.Length != 2) throw new FormatException();
            var bytes = Convert.FromBase64String(partes[0]);
            using var rsa = RSA.Create();
            rsa.ImportFromPem(chavePublica);
            if (!rsa.VerifyData(bytes, Convert.FromBase64String(partes[1]), HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
                throw new FormatException();
            dados = JsonSerializer.Deserialize<DadosLicenca>(bytes) ?? throw new FormatException();
        }
        catch (Exception e) when (e is FormatException or CryptographicException or JsonException or ArgumentException)
        { throw new InvalidOperationException("Chave inválida ou alterada."); }
        if (dados.Versao != 1 || string.IsNullOrWhiteSpace(dados.Id) || string.IsNullOrWhiteSpace(dados.Cliente))
            throw new InvalidOperationException("Formato de licença não suportado.");
        if (!string.Equals(dados.Computador, computador, StringComparison.Ordinal))
            throw new InvalidOperationException("Esta chave pertence a outro computador.");
        if (ultimaUtilizacao.HasValue && agora < ultimaUtilizacao.Value.AddMinutes(-5))
            throw new InvalidOperationException("O relógio foi atrasado. Corrija a data e a hora do Windows.");
        if (agora < dados.Inicio) throw new InvalidOperationException("A licença ainda não começou. Confira a data do Windows.");
        if (dados.Vencimento.HasValue && (dados.Vencimento <= dados.Inicio || agora >= dados.Vencimento))
            throw new InvalidOperationException("Licença vencida. Solicite uma nova chave após a renovação.");
        return dados;
    }
}
