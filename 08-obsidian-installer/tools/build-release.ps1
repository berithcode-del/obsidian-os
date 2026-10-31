param(
    [string]$Version = '1.0',
    [string]$Tag = 'v1.0-r3',
    [string]$InstallerDir = 'D:\R36S-sistema-novo\08-obsidian-installer\packaging\out\instalador-win-x64',
    [string]$ImageDir = 'D:\R36S-sistema-novo\08-obsidian-installer\artifacts\releases\obsidian-os-1.0-r3',
    [string]$OutputRoot = 'D:\R36S-sistema-novo\release',
    [string]$Compression = 'Optimal'
)
# Monta a pasta de distribuicao: aplicativo (com a imagem dentro), imagem avulsa,
# instrucoes e checksums. Nada e enviado para a internet aqui.
$ErrorActionPreference = 'Stop'
function Log([string]$m) { Write-Host ("[{0}] {1}" -f (Get-Date -Format 'HH:mm:ss'), $m) }

$out = Join-Path $OutputRoot "obsidian-os-$Tag"
$app = Join-Path $out 'instalador'
$img = Join-Path $out 'imagem'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force $app, $img | Out-Null

Log "copiando o instalador ($InstallerDir)"
Copy-Item (Join-Path $InstallerDir '*') $app -Recurse -Force
Remove-Item (Join-Path $app 'work') -Recurse -Force -ErrorAction SilentlyContinue

Log "copiando a imagem e o manifesto"
Copy-Item (Join-Path $ImageDir '*.img') $img -Force
Copy-Item (Join-Path $ImageDir '*.manifest.json') $img -Force
Copy-Item (Join-Path $ImageDir 'SHA256SUMS') $img -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $ImageDir 'build-report.txt') $img -Force -ErrorAction SilentlyContinue

$leia = @'
OBSIDIAN OS 1.0 - INSTALADOR (Windows 10/11, 64 bits)
=====================================================

O QUE TEM AQUI
  instalador\ObsidianInstaller.exe   Instalador com a imagem do sistema ja embutida.
  imagem\obsidian-os-1.0-lab-r3.img  Imagem crua do cartao, para quem preferir gravar
                                     com outra ferramenta (balenaEtcher, Win32DiskImager).
  imagem\*.manifest.json             Manifesto com o SHA-256 conferido pelo instalador.
  SHA256SUMS.txt                     Hashes de todos os arquivos desta pasta.

COMO GRAVAR O CARTAO
  1. Ligue o computador com o console DESLIGADO e o cabo USB conectado, SEM o cartao.
     Assim o console entra em modo FEL (o instalador mostra "Console em modo FEL").
  2. Abra instalador\ObsidianInstaller.exe e clique em Continuar.
  3. Insira o cartao no console quando o instalador pedir (ou use um leitor de cartoes).
  4. Confirme o cartao, aceite o aviso de administrador do Windows e aguarde.
  5. Ao terminar: DESCONECTE O CABO USB do computador e ligue o console.
     O console so liga quando nao esta conectado ao computador.

O QUE O INSTALADOR FAZ SOZINHO
  - Confere o SHA-256 da imagem antes de tocar no cartao.
  - Envia o carregador ao console (modo FEL), sem precisar de leitor.
  - Apaga a tabela de particoes, testa o enlace, grava, rele e confere.
  - Cria a area OBSIDIAN (FAT32) com as pastas leitura\, videos\ e saves\.

AVISO
  Todo o conteudo do cartao e apagado. Nao grave no cartao do seu dia a dia.
  O cartao precisa ter no minimo 2 GB; recomendado 16 GB ou mais.

VERIFICAR OS ARQUIVOS (opcional, no PowerShell)
  Get-FileHash .\instalador\ObsidianInstaller.exe -Algorithm SHA256
  Compare com a linha correspondente em SHA256SUMS.txt.
'@
Set-Content -LiteralPath (Join-Path $out 'LEIA-ME.txt') -Value $leia -Encoding UTF8
Set-Content -LiteralPath (Join-Path $app 'LEIA-ME.txt') -Value $leia -Encoding UTF8

Log "comprimindo o aplicativo (pode levar alguns minutos)"
$zip = Join-Path $out "ObsidianOS-$Version-Instalador-win64.zip"
Compress-Archive -Path (Join-Path $app '*') -DestinationPath $zip -CompressionLevel $Compression -Force
Log ("zip: {0:N0} MB" -f ((Get-Item $zip).Length / 1MB))
Remove-Item $app -Recurse -Force

Log "gerando checksums"
$files = @()
$files += Get-Item $zip
$files += Get-ChildItem $img -File
$lines = foreach ($f in $files) {
    $hash = (Get-FileHash $f.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    $rel = $f.FullName.Substring($out.Length + 1).Replace('\', '/')
    "{0}  {1}" -f $hash, $rel
}
Set-Content -LiteralPath (Join-Path $out 'SHA256SUMS.txt') -Value ($lines -join "`n") -Encoding UTF8

$imgFile = Get-ChildItem $img -Filter '*.img' | Select-Object -First 1
$summary = [ordered]@{
    tag = $Tag
    version = $Version
    package_zip = $zip
    package_mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
    image = $imgFile.FullName
    image_mb = [math]::Round($imgFile.Length / 1MB, 1)
    image_sha256 = (Get-FileHash $imgFile.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    files = @($files | ForEach-Object { $_.Name })
    folder = $out
    completed_utc = (Get-Date).ToUniversalTime().ToString('o')
}
$summary | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $out 'release.json') -Encoding UTF8

Log "conteudo final:"
Get-ChildItem $out -Recurse -File | Sort-Object Length -Descending |
    ForEach-Object { "  {0,8:N1} MB  {1}" -f ($_.Length / 1MB), $_.FullName.Substring($out.Length + 1) }
Log "RELEASE_OK -> $out"
