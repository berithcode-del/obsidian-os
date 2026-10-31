param(
    [string]$Distro = 'ObsisidianBuild-20261029',
    [switch]$Force,
    [string]$OutputDir,
    [string]$Tag = 'r1'
)
# Build the Obsidian OS 1.0 card image from the proven base + staged overlay.
#
# Runs the assembly inside the WSL build distribution (root, loop devices) and
# writes the release folder with image, manifest, sums and build report.
# Nothing here touches the console or a physical card.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$base = Join-Path (Split-Path -Parent $root) '02-imagens-personalizadas\Obsisidian-prova-USB-base-goodluck-20261029.img'
$overlayTar = Join-Path $root 'build\image-overlay.tar'
$overlayJson = Join-Path $root 'build\overlay.json'
if (-not $OutputDir) { $OutputDir = Join-Path $root "artifacts\releases\obsidian-os-1.0-$Tag" }
$outImage = Join-Path $OutputDir "obsidian-os-1.0-lab-$Tag.img"

foreach ($p in @($base, $overlayTar, $overlayJson)) {
    if (-not (Test-Path -LiteralPath $p)) { throw "arquivo de entrada ausente: $p" }
}
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

function ConvertTo-WslPath([string]$winPath) {
    $full = [System.IO.Path]::GetFullPath($winPath)
    $drive = $full.Substring(0, 1).ToLowerInvariant()
    $rest = $full.Substring(2).Replace('\', '/')
    return "/mnt/$drive$rest"
}

$envPairs = @(
    "BASE_IMG=$(ConvertTo-WslPath $base)",
    "OVERLAY_TAR=$(ConvertTo-WslPath $overlayTar)",
    "OVERLAY_JSON=$(ConvertTo-WslPath $overlayJson)",
    "OUT_IMG=$(ConvertTo-WslPath $outImage)",
    "FORCE=$(if ($Force) { '1' } else { '0' })"
)
$script = ConvertTo-WslPath (Join-Path $PSScriptRoot 'apply-image.sh')

Write-Host "Montando a imagem Obsidian OS 1.0 (somente leitura na base)..."
$output = & wsl -d $Distro -- env @envPairs bash $script 2>&1
$exit = $LASTEXITCODE
$text = ($output | Out-String)
Write-Host $text
if ($exit -ne 0) { throw "a montagem da imagem falhou (exit $exit)" }

# Parse the JSON summary printed by the script.
$start = $text.IndexOf('{')
$end = $text.LastIndexOf('}')
if ($start -lt 0 -or $end -le $start) { throw 'resumo JSON da montagem não encontrado' }
$summary = $text.Substring($start, $end - $start + 1) | ConvertFrom-Json

$launcher = $summary.launcher_sha256
$manifest = [ordered]@{
    schema_version = 1
    package_id     = "obsidian-os-1-0-lab-$Tag"
    display_name   = 'Obsidian OS 1.0 - imagem de cartão (laboratório)'
    system_version = "1.0-lab-$Tag"
    channel        = 'lab'
    customer_installation_allowed = $false
    distribution_review = 'pending'
    includes_final_obsidian_launcher = ($launcher -eq 'de6fa0a3ac98c529c429554755cab021a5a7c85197acf8b12d4bef420165c018')
    compatible_profile_ids = @('ga36-a33-rhododendron-panel1-lab')
    payload = [ordered]@{
        kind        = 'bundled_file'
        file        = (Split-Path -Leaf $outImage)
        sha256      = $summary.sha256
        image_bytes = $summary.size_bytes
    }
    persistent_boot_origin = 'base-goodluck-usb-proof-20261029+overlay-console-20261030'
    sector_bytes = 512
    partitions = @(
        [ordered]@{ role = 'root'; filesystem = 'ext4';  start_sector = 262144;  sector_count = 786432 },
        [ordered]@{ role = 'home'; filesystem = 'fat32'; start_sector = 1048576; sector_count = 1048576 }
    )
    storage_policy = [ordered]@{
        tested_mode = 'single_card'
        data_partition = [ordered]@{
            role          = 'obsidian-data'
            filesystem    = 'fat32'
            label         = 'OBSIDIAN'
            start_sector  = 2097152
            created_by    = 'installer (gravação grava a imagem e em seguida cria/formata a p3)'
            mount_point   = '/media/obsidian'
        }
        home_bytes = 536870912
        rom_directories = @('gba', 'nds', 'snes', 'psx', 'psp', 'cps2', 'cps3')
    }
    boot_verification = [ordered]@{
        test_id       = 'OBSIDIAN-OS-1.0-R1-BOOT-01'
        identity_file = '/etc/obsidian-release'
        boot_report   = '/home/player/obsidian-relatorio-boot.txt'
        status        = 'pending_physical_card_test'
    }
    signature = [ordered]@{ status = 'not_signed_for_distribution' }
    launcher_sha256 = $launcher
    build = [ordered]@{
        built_utc      = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
        base_image     = 'Obsisidian-prova-USB-base-goodluck-20261029.img'
        base_sha256    = $summary.base_image_sha256
        overlay_sha256 = (Get-FileHash -LiteralPath $overlayTar -Algorithm SHA256).Hash.ToLowerInvariant()
        free_kib       = $summary.free_kib
        used_kib       = $summary.used_kib
    }
}
$manifestPath = Join-Path $OutputDir "obsidian-os-1.0-lab-$Tag.manifest.json"
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

$sums = @(
    "$($summary.sha256)  $(Split-Path -Leaf $outImage)"
) -join "`n"
Set-Content -LiteralPath (Join-Path $OutputDir 'SHA256SUMS') -Value ($sums + "`n") -Encoding ASCII
$text | Set-Content -LiteralPath (Join-Path $OutputDir 'build-report.txt') -Encoding UTF8

Write-Host ""
Write-Host "Imagem:   $outImage"
Write-Host "Bytes:    $($summary.size_bytes)"
Write-Host "SHA-256:  $($summary.sha256)"
Write-Host "Launcher: $launcher"
Write-Host "Manifesto:$manifestPath"
