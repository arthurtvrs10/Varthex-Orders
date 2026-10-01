# Varthex Licenças — Android

Emissor offline para uso do responsável pelo Varthex Comanda. Android 8.0 ou superior, sem dependências externas e sem permissão de internet.

## Instalar e usar

1. Transfira [Varthex-Licencas-1.0.0.apk](https://raw.githubusercontent.com/arthurtvrs10/Varthex-Orders/main/downloads/android/Varthex-Licencas-1.0.0.apk) para o celular, abra e permita a instalação por esse aplicativo quando o Android solicitar.
2. Transfira **somente para o celular do responsável** o arquivo `artifacts/segredos/licenca-privada.pem` existente neste projeto. Ele não está incluído no APK nem no Git.
3. Abra **Varthex Licenças** e toque em **Importar chave privada (.pem)**. Escolha esse arquivo no seletor do Android.
4. Informe o cliente e cole o código de 64 caracteres exibido pelo sistema Windows.
5. Escolha 1, 2, 3 meses ou Vitalícia. A data inicial usa o horário de Brasília; para renovação antecipada, informe o vencimento atual.
6. Confirme a autorização, toque em **Gerar chave de ativação** e depois **Copiar chave** ou **Compartilhar chave**.

Envie ao cliente apenas a licença gerada. Quem possui a chave privada pode emitir licenças: ela é exclusiva do responsável. O app confere a correspondência com a chave pública do sistema antes de importar.

O app guarda a chave privada apenas na memória da sessão. **Bloquear emissor** a remove da sessão; reabrir após encerramento do processo exige nova importação. O arquivo PEM que você transferiu continua no armazenamento do celular até ser removido por você. O app não faz backup nem envia a chave. A captura da tela e a prévia em aplicativos recentes são bloqueadas.

## Compilar

JDK 21, Android SDK Platform 35 e Build Tools 35.0.0. Não precisa de Gradle ou Android Studio. Com as ferramentas instaladas:

```powershell
.\mobile\licencas\build.ps1 -Sdk 'C:\caminho\android-sdk' -Java 'C:\caminho\jdk-21'
```

O APK e SHA-256 ficam em `artifacts/android-licencas/`. O script cria uma chave de assinatura do APK em `artifacts/segredos/android-emissor.p12`, com senha no arquivo adjacente. Preserve esses dois arquivos para assinar atualizações; eles são diferentes da chave privada que emite licenças. Nenhum segredo deve entrar no repositório.

O script usa as [ferramentas oficiais do Android](https://developer.android.com/tools) e verifica o APK com [apksigner](https://developer.android.com/tools/apksigner).

## Validação

`tests/IssuerTest.java` gera chaves sintéticas e licenças para os quatro planos. `tests/Interop.csproj` as valida usando o código real `LicencaOffline` do aplicativo Windows, incluindo acentos, caracteres especiais, limites de mês e rejeição no instante de vencimento. Nenhuma chave privada de produção é usada nos testes.

```powershell
.\mobile\licencas\test.ps1
```

APK compilado e assinatura verificada; testes de interoperabilidade aprovados. Ainda não testado em aparelho Android ou emulador: instalação, seletor de arquivos, teclado e compartilhamento precisam de conferência no celular.
