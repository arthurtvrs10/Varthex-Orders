using System.Globalization;
using System.Security.Cryptography;
using VarthexComanda.Application.Licenciamento;

try
{
    if (args.Length == 3 && args[0] == "inicializar")
    {
        if (File.Exists(args[1]) || File.Exists(args[2])) throw new InvalidOperationException("Chaves já existem; não serão substituídas.");
        using var rsa = RSA.Create(3072);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))!);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
        File.WriteAllText(args[1], rsa.ExportPkcs8PrivateKeyPem());
        File.WriteAllText(args[2], rsa.ExportSubjectPublicKeyInfoPem());
        Console.WriteLine("Chaves criadas. Guarde a chave privada e seu backup em local seguro; nunca envie ao cliente.");
        return 0;
    }

    Console.WriteLine("Varthex — Emissor de licenças offline (uso exclusivo do responsável)");
    string Perguntar(string texto) { Console.Write(texto + ": "); return Console.ReadLine()?.Trim() ?? ""; }
    var privada = args.Length == 1 ? args[0] : Perguntar("Caminho do arquivo de chave privada .pem");
    var computador = Perguntar("Código do computador do cliente").ToUpperInvariant();
    if (computador.Length != 64 || !computador.All(Uri.IsHexDigit)) throw new InvalidOperationException("Código de computador inválido.");
    var cliente = Perguntar("Nome do cliente/estabelecimento");
    if (string.IsNullOrWhiteSpace(cliente) || cliente.Length > 200) throw new InvalidOperationException("Nome inválido.");
    var plano = Perguntar("Quantidade de meses (1, 2, 3 ou 0 para vitalícia)");
    if (!int.TryParse(plano, out var meses) || meses is < 0 or > 3) throw new InvalidOperationException("Plano inválido.");
    var data = Perguntar("Data inicial dd/MM/aaaa (vazio = hoje; renovação antecipada: informe o vencimento atual)");
    var dia = string.IsNullOrEmpty(data) ? DateTime.UtcNow.AddHours(-3).Date : DateTime.ParseExact(data, "dd/MM/yyyy", CultureInfo.InvariantCulture);
    var inicio = new DateTimeOffset(DateTime.SpecifyKind(dia, DateTimeKind.Unspecified), TimeSpan.FromHours(-3));
    DateTimeOffset? fim = meses == 0 ? null : inicio.AddMonths(meses);
    var dados = new DadosLicenca(1, Guid.NewGuid().ToString("N"), computador, cliente, inicio, fim);
    Console.WriteLine($"Cliente: {cliente}\nInício: {inicio:dd/MM/yyyy}\nVencimento exclusivo: {(fim.HasValue ? fim.Value.ToString("dd/MM/yyyy HH:mm zzz") : "Vitalícia")}");
    if (!string.Equals(Perguntar("Emitir após confirmar o pagamento? Digite SIM"), "SIM", StringComparison.OrdinalIgnoreCase)) return 0;
    var chave = LicencaOffline.Emitir(dados, File.ReadAllText(privada.Trim('"')));
    var arquivo = Path.Combine(AppContext.BaseDirectory, "licenca-" + dados.Id + ".txt");
    File.WriteAllText(arquivo, chave);
    Console.WriteLine($"Chave salva em: {arquivo}\n\n{chave}");
    if (!Console.IsInputRedirected) { Console.WriteLine("Pressione Enter para fechar."); Console.ReadLine(); }
    return 0;
}
catch (Exception e)
{
    Console.Error.WriteLine("Falha: " + e.Message);
    if (!Console.IsInputRedirected) { Console.WriteLine("Pressione Enter para fechar."); Console.ReadLine(); }
    return 1;
}
