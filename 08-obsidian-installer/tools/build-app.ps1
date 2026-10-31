param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$PublishPortable,
    [string[]]$SdkRoots = @('D:\dotnet', "$env:LOCALAPPDATA\ObsidianInstaller\dev-sdk")
)
# Compila a solução. O SDK e o cache do NuGet ficam em D: (o disco C: é somente leitura).
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$projectRoot = Split-Path -Parent $PSScriptRoot

if (-not $env:DOTNET_CLI_HOME) { $env:DOTNET_CLI_HOME = 'D:\dotnet-home' }
if (-not $env:NUGET_PACKAGES) { $env:NUGET_PACKAGES = 'D:\dotnet-home\packages' }
if (-not $env:TMP -or $env:TMP -like 'C:\*') { $env:TMP = 'D:\dsh-temp' }
if (-not $env:TEMP -or $env:TEMP -like 'C:\*') { $env:TEMP = 'D:\dsh-temp' }
New-Item -ItemType Directory -Force -Path $env:DOTNET_CLI_HOME, $env:NUGET_PACKAGES, $env:TMP | Out-Null

$dotnet = $null
foreach ($root in $SdkRoots) {
    $candidate = Join-Path $root 'dotnet.exe'
    if (Test-Path -LiteralPath $candidate) { $dotnet = $candidate; break }
}
if (-not $dotnet) {
    $installed = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($installed) { $dotnet = $installed.Source }
}
if (-not $dotnet) {
    throw 'SDK do .NET 10 não encontrado (procurei D:\dotnet, %LOCALAPPDATA%\ObsidianInstaller\dev-sdk e o PATH). Este script não eleva privilégios nem acessa o console.'
}

Push-Location $projectRoot
try {
    $sdkVersion = (& $dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Não foi possível consultar a versão do SDK .NET.' }
    Write-Host "Compilando Obsidian Installer com .NET SDK $sdkVersion. O aplicativo não será iniciado."

    & $dotnet build 'ObsidianInstaller.sln' --configuration $Configuration -p:Platform=x64
    if ($LASTEXITCODE -ne 0) { throw 'A compilação falhou.' }

    & $dotnet build 'src/ObsidianInstaller.SelfTest/ObsidianInstaller.SelfTest.csproj' --configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'A compilação do self-test falhou.' }

    if ($PublishPortable) {
        & $dotnet publish 'src/ObsidianInstaller.App/ObsidianInstaller.App.csproj' --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=false -o 'packaging/out/instalador-win-x64'
        if ($LASTEXITCODE -ne 0) { throw 'A publicação portátil falhou.' }
    }
}
finally {
    Pop-Location
}
