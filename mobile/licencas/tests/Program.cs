using VarthexComanda.Application.Licenciamento;
var folder = args[0];
var publicKey = File.ReadAllText(Path.Combine(folder,"public.pem"));
string[] dates = ["2028-01-31", "2027-01-31", "2026-11-30", "2026-09-30"];
for (var months=0; months<=3; months++) {
    var token=File.ReadAllText(Path.Combine(folder,$"license-{months}.txt"));
    var start=DateTimeOffset.Parse(dates[months]+"T00:00:00-03:00");
    var data=LicencaOffline.Validar(token,publicKey,new string('A',64),start);
    if(data.Cliente!="João \"Teste\" \\ Café\n🍔" || data.Inicio!=start || data.Vencimento!=(months==0 ? (DateTimeOffset?)null : start.AddMonths(months))) throw new Exception("Payload incompatível");
    if(months>0) {
        try {LicencaOffline.Validar(token,publicKey,new string('A',64),start.AddMonths(months)); throw new Exception("Licença vencida aceita");}
        catch(InvalidOperationException) {}
    }
}
Console.WriteLine(".NET: 4 licenças Java aceitas, datas e nomes preservados, 3 vencimentos rejeitados.");
