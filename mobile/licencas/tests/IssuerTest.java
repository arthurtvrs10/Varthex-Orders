import com.varthex.licencas.LicenseIssuer;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import java.security.*;
import java.time.LocalDate;
import java.util.Base64;

public class IssuerTest {
    public static void main(String[] args) throws Exception {
        Path out=Paths.get(args[0]); Files.createDirectories(out);
        KeyPairGenerator generator=KeyPairGenerator.getInstance("RSA"); generator.initialize(2048);
        KeyPair pair=generator.generateKeyPair();
        String privatePem=pem("PRIVATE KEY",pair.getPrivate().getEncoded());
        String publicPem=pem("PUBLIC KEY",pair.getPublic().getEncoded());
        PrivateKey key=LicenseIssuer.importKey(privatePem,publicPem);
        LicenseIssuer.importKey(" \r\n"+privatePem.replace("\n","\r\n")+"\r\n ",publicPem);
        expectFailure(()->LicenseIssuer.importKey(privatePem.replace("-----END PRIVATE KEY-----",""),publicPem));
        expectFailure(()->LicenseIssuer.importKey(publicPem,publicPem));
        expectFailure(()->LicenseIssuer.importKey("",publicPem));
        expectFailure(()->LicenseIssuer.importKey(null,publicPem));
        String pc=new String(new char[64]).replace('\0','A');
        String[] dates={"2028-01-31","2027-01-31","2026-11-30","2026-09-30"};
        for(int months=0;months<=3;months++){
            String token=LicenseIssuer.issue(key,pc,"João \"Teste\" \\ Café\n🍔",months,LocalDate.parse(dates[months]));
            Files.write(out.resolve("license-"+months+".txt"),token.getBytes(StandardCharsets.UTF_8));
        }
        expectFailure(()->LicenseIssuer.issue(key,"ABC","Teste",1,LocalDate.now()));
        expectFailure(()->LicenseIssuer.issue(key,pc," ",1,LocalDate.now()));
        expectFailure(()->LicenseIssuer.issue(key,pc,"Teste",4,LocalDate.now()));
        expectFailure(()->LicenseIssuer.issue(null,pc,"Teste",1,LocalDate.now()));
        expectFailure(()->LicenseIssuer.importKey(privatePem,pem("PUBLIC KEY",generator.generateKeyPair().getPublic().getEncoded())));
        Files.write(out.resolve("public.pem"),publicPem.getBytes(StandardCharsets.UTF_8));
        System.out.println("Java: 4 licenças, PEM com CRLF e 9 casos inválidos verificados; apenas chaves sintéticas.");
    }
    interface Action {void run() throws Exception;}
    static void expectFailure(Action f)throws Exception{try{f.run();}catch(IllegalArgumentException|GeneralSecurityException e){return;}throw new AssertionError("Entrada inválida aceita");}
    static String pem(String kind,byte[] data){return "-----BEGIN "+kind+"-----\n"+Base64.getEncoder().encodeToString(data)+"\n-----END "+kind+"-----";}
}
