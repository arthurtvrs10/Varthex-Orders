[CmdletBinding()]
param([string]$Sdk, [string]$Java = 'C:\Program Files\Java\jdk-21.0.11')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $Sdk) { $Sdk = Join-Path $repo 'artifacts/android-sdk' }
$tools = Join-Path $Sdk 'build-tools/35.0.0'
$platform = Join-Path $Sdk 'platforms/android-35/android.jar'
$out = Join-Path $repo 'artifacts/android-licencas'
$classes = Join-Path $out 'classes'
$dex = Join-Path $out 'dex'
New-Item -ItemType Directory -Force $out,$classes,$dex | Out-Null
function Check { if ($LASTEXITCODE -ne 0) { throw "Falha no empacotamento: $LASTEXITCODE" } }
& "$tools/aapt.exe" package -f -M "$PSScriptRoot/AndroidManifest.xml" -S "$PSScriptRoot/res" -A "$PSScriptRoot/assets" -I $platform -F "$out/unsigned.apk"
Check
$sources = @(Get-ChildItem "$PSScriptRoot/src" -Filter '*.java' -Recurse | Select-Object -ExpandProperty FullName)
& "$Java/bin/javac.exe" --release 8 -encoding UTF-8 -classpath $platform -d $classes @sources
Check
$compiled = @(Get-ChildItem $classes -Filter '*.class' -Recurse | Select-Object -ExpandProperty FullName)
& "$Java/bin/java.exe" -cp "$tools/lib/d8.jar" com.android.tools.r8.D8 --release --min-api 26 --lib $platform --output $dex @compiled
Check
Push-Location $dex
try { & "$tools/aapt.exe" add "$out/unsigned.apk" classes.dex; Check } finally { Pop-Location }
& "$tools/zipalign.exe" -f -p 4 "$out/unsigned.apk" "$out/aligned.apk"
Check
# Independent APK signing key; never use the license-issuing key here.
$secrets = Join-Path $repo 'artifacts/segredos'
New-Item -ItemType Directory -Force $secrets | Out-Null
$keystore = Join-Path $secrets 'android-emissor.p12'
$password = Join-Path $secrets 'android-emissor-password.txt'
if (-not (Test-Path $keystore)) {
    if (-not (Test-Path $password)) {
        $bytes = New-Object byte[] 32
        [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
        [IO.File]::WriteAllText($password,[Convert]::ToBase64String($bytes))
    }
    & "$Java/bin/keytool.exe" -genkeypair -keystore $keystore -storetype PKCS12 -storepass:file $password -alias emissor -keyalg RSA -keysize 3072 -validity 10000 -dname 'CN=Varthex Licencas, O=Varthex, C=BR'
    Check
}
$apk = Join-Path $out 'Varthex-Licencas-1.0.1.apk'
& "$Java/bin/java.exe" -jar "$tools/lib/apksigner.jar" sign --ks $keystore --ks-pass "file:$password" --out $apk "$out/aligned.apk"
Check
& "$Java/bin/java.exe" -jar "$tools/lib/apksigner.jar" verify --verbose $apk
Check
$hash = (Get-FileHash $apk -Algorithm SHA256).Hash
"$hash  Varthex-Licencas-1.0.1.apk" | Set-Content "$apk.sha256" -Encoding ascii
Write-Host "APK gerado: $apk"
