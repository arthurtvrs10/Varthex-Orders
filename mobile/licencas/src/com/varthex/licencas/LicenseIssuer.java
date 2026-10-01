package com.varthex.licencas;

import java.nio.charset.StandardCharsets;
import java.security.*;
import java.security.interfaces.RSAPrivateCrtKey;
import java.security.spec.*;
import java.time.LocalDate;
import java.util.Base64;
import java.util.Locale;
import java.util.UUID;

/** Same signed UTF-8 payload and RSA-PSS/SHA-256 protocol as LicencaOffline (.NET). */
public final class LicenseIssuer {
    private LicenseIssuer() {}

    public static PrivateKey importKey(String pem, String publicPem) throws GeneralSecurityException {
        if (!pem.contains("-----BEGIN PRIVATE KEY-----"))
            throw new IllegalArgumentException("Selecione a chave privada .pem do emissor original (PKCS#8).");
        KeyFactory factory = KeyFactory.getInstance("RSA");
        PrivateKey privateKey = factory.generatePrivate(new PKCS8EncodedKeySpec(decode(pem, "PRIVATE KEY")));
        PublicKey expected = factory.generatePublic(new X509EncodedKeySpec(decode(publicPem, "PUBLIC KEY")));
        if (!(privateKey instanceof RSAPrivateCrtKey)) throw new InvalidKeyException("Chave RSA inválida.");
        RSAPrivateCrtKey rsa = (RSAPrivateCrtKey) privateKey;
        PublicKey actual = factory.generatePublic(new RSAPublicKeySpec(rsa.getModulus(), rsa.getPublicExponent()));
        if (!MessageDigest.isEqual(actual.getEncoded(), expected.getEncoded()))
            throw new InvalidKeyException("Esta chave privada não corresponde ao Varthex Comanda.");
        return privateKey;
    }

    private static byte[] decode(String pem, String type) {
        return Base64.getDecoder().decode(pem.replace("-----BEGIN " + type + "-----", "")
                .replace("-----END " + type + "-----", "").replaceAll("\\s", ""));
    }

    public static String issue(PrivateKey key, String computer, String client, int months, LocalDate start)
            throws GeneralSecurityException {
        if (key == null) throw new InvalidKeyException("Importe a chave privada primeiro.");
        computer = computer.trim().toUpperCase(Locale.ROOT);
        client = client.trim();
        if (!computer.matches("[0-9A-F]{64}")) throw new IllegalArgumentException("O código deve ter 64 caracteres, de 0 a 9 e A a F.");
        if (client.isEmpty() || client.length() > 200) throw new IllegalArgumentException("Informe o cliente (até 200 caracteres).");
        if (months < 0 || months > 3) throw new IllegalArgumentException("Plano inválido.");
        if (start == null || start.getYear() < 2000 || start.getYear() > 9998)
            throw new IllegalArgumentException("Data inicial inválida.");
        String end = months == 0 ? "null" : quote(start.plusMonths(months) + "T00:00:00-03:00");
        String json = "{\"Versao\":1,\"Id\":" + quote(UUID.randomUUID().toString().replace("-", ""))
                + ",\"Computador\":" + quote(computer) + ",\"Cliente\":" + quote(client)
                + ",\"Inicio\":" + quote(start + "T00:00:00-03:00") + ",\"Vencimento\":" + end + "}";
        byte[] payload = json.getBytes(StandardCharsets.UTF_8);
        Signature signer;
        try { signer = Signature.getInstance("RSASSA-PSS"); }
        catch (NoSuchAlgorithmException e) { signer = Signature.getInstance("SHA256withRSA/PSS"); }
        signer.setParameter(new PSSParameterSpec("SHA-256", "MGF1", MGF1ParameterSpec.SHA256, 32, 1));
        signer.initSign(key);
        signer.update(payload);
        return Base64.getEncoder().encodeToString(payload) + "." + Base64.getEncoder().encodeToString(signer.sign());
    }

    private static String quote(String text) {
        StringBuilder result = new StringBuilder("\"");
        for (int i = 0; i < text.length(); i++) {
            char c = text.charAt(i);
            if (c == '"' || c == '\\') result.append('\\').append(c);
            else if (c < 32 || Character.isSurrogate(c)) result.append(String.format(Locale.ROOT, "\\u%04x", (int)c));
            else result.append(c);
        }
        return result.append('"').toString();
    }
}
