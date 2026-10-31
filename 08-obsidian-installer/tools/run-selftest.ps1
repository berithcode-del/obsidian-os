param(
    [string]$WorkDirectory = 'D:\dsh-temp\selftest'
)
# Executa o self-test do núcleo sem tocar em cartão físico.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$dll = Join-Path $projectRoot 'src/ObsidianInstaller.SelfTest/bin/Release/net10.0/ObsidianInstaller.SelfTest.dll'
if (-not (Test-Path -LiteralPath $dll)) {
    throw "Compile primeiro com tools/build-app.ps1: $dll não existe."
}
if (-not $env:TMP -or $env:TMP -like 'C:\*') { $env:TMP = 'D:\dsh-temp' }
if (-not $env:TEMP -or $env:TEMP -like 'C:\*') { $env:TEMP = 'D:\dsh-temp' }
$dotnet = if (Test-Path 'D:\dotnet\dotnet.exe') { 'D:\dotnet\dotnet.exe' } else { (Get-Command dotnet -ErrorAction Stop).Source }
& $dotnet $dll $WorkDirectory
exit $LASTEXITCODE
