[CmdletBinding()]
param([string]$Java = 'C:\Program Files\Java\jdk-21.0.11')
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$out = Join-Path $repo 'artifacts/android-tests'
New-Item -ItemType Directory -Force $out | Out-Null
& "$Java/bin/javac.exe" -encoding UTF-8 -d $out "$PSScriptRoot/src/com/varthex/licencas/LicenseIssuer.java" "$PSScriptRoot/tests/IssuerTest.java"
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar testes Java' }
& "$Java/bin/java.exe" -cp $out IssuerTest "$out/fixtures"
if ($LASTEXITCODE -ne 0) { throw 'Falha nos testes Java' }
& dotnet run --project "$PSScriptRoot/tests/Interop.csproj" -- "$out/fixtures"
if ($LASTEXITCODE -ne 0) { throw 'Falha na interoperabilidade com o Windows' }
