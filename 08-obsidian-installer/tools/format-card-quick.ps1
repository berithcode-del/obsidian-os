param(
    [Parameter(Mandatory = $true)][int]$DiskNumber,
    [Parameter(Mandatory = $true)][string]$ExpectedSerial,
    [Parameter(Mandatory = $true)][long]$ExpectedBytes,
    [string]$Label = 'SDCARD',
    [string]$LogPath = 'D:\dsh-temp\format-quick.log',
    [string]$ReportPath = 'D:\dsh-temp\format-quick.json'
)
# Formatação rápida: cria uma partição ocupando o cartão inteiro, formata FAT32
# e confirma que o volume aparece e aceita leitura/escrita. Exige administrador.
# Travas: recusa disco de sistema/BOOT, disco não removível, série e tamanho diferentes.
$ErrorActionPreference = 'Stop'
function Write-Log([string]$message) {
    $line = "{0}  {1}" -f (Get-Date).ToString('HH:mm:ss'), $message
    Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8
    Write-Host $line
}
function Fail([string]$message) {
    Write-Log "FALHA: $message"
    @{ success = $false; message = $message; disk = $DiskNumber } |
        ConvertTo-Json | Set-Content -LiteralPath $ReportPath -Encoding UTF8
    exit 1
}

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $LogPath) | Out-Null
Set-Content -LiteralPath $LogPath -Value "=== formatacao rapida $(Get-Date -Format o) ===" -Encoding UTF8

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { Fail 'processo não elevado' }

$disk = Get-Disk -Number $DiskNumber -ErrorAction SilentlyContinue
if (-not $disk) { Fail "disco $DiskNumber não encontrado" }
Write-Log ("alvo: disco {0} · {1} · série {2} · {3:N0} bytes" -f $disk.Number, $disk.FriendlyName, $disk.SerialNumber, $disk.Size)
if ($disk.IsBoot -or $disk.IsSystem) { Fail 'disco de sistema/BOOT recusado' }
if ($disk.SerialNumber -ne $ExpectedSerial) { Fail "série $($disk.SerialNumber) difere de $ExpectedSerial" }
if ([Math]::Abs($disk.Size - $ExpectedBytes) -gt 64MB) { Fail "tamanho $($disk.Size) difere de $ExpectedBytes" }

# Recusa discos fixos, a menos que o chamador insista explicitamente.
$fixed = Get-CimInstance Win32_DiskDrive | Where-Object { $_.Index -eq $DiskNumber }
if ($fixed -and $fixed.MediaType -notmatch 'Removable') { Fail "mídia não removível ($($fixed.MediaType)) recusada" }

$script = Join-Path (Split-Path -Parent $LogPath) 'diskpart-quick.txt'
Set-Content -LiteralPath $script -Encoding ASCII -Value @(
    "select disk $DiskNumber",
    'clean',
    'create partition primary',
    "format fs=fat32 quick label=$Label",
    'assign',
    'exit'
)
$out = & "$env:SystemRoot\System32\diskpart.exe" /s $script 2>&1 | Out-String
$out.Trim().Split("`n") | ForEach-Object { if ($_.Trim()) { Write-Log ("diskpart: " + $_.Trim()) } }
$lower = $out.ToLowerInvariant()
if ($lower -match 'erro|error|falha|invalid|não foi possível') { Fail 'diskpart reportou erro' }

# Espera o volume aparecer com o rótulo.
$volume = $null
for ($i = 0; $i -lt 40 -and -not $volume; $i++) {
    Start-Sleep -Milliseconds 750
    Update-HostStorageCache -ErrorAction SilentlyContinue
    $volume = Get-Volume -FileSystemLabel $Label -ErrorAction SilentlyContinue | Select-Object -First 1
}
if (-not $volume) { Fail "o volume $Label não apareceu" }
Write-Log ("volume: {0} rótulo={1} fs={2} {3:N2} GB livres={4:N2} GB" -f $volume.DriveLetter, $volume.FileSystemLabel, $volume.FileSystem, ($volume.Size / 1GB), ($volume.SizeRemaining / 1GB))

# Prova de leitura e escrita.
$root = $volume.DriveLetter + ':\'
$probe = Join-Path $root 'teste-formatacao.txt'
$payload = 'Obsidian Installer: cartao formatado em ' + (Get-Date -Format s)
Set-Content -LiteralPath $probe -Value $payload -Encoding UTF8
$readBack = Get-Content -LiteralPath $probe -Raw -Encoding UTF8
$ok = $readBack.Trim() -eq $payload
Remove-Item -LiteralPath $probe -Force
if (-not $ok) { Fail 'a leitura do arquivo de teste não confere' }
Write-Log 'escrita e leitura de teste conferidas'

$partitions = @(Get-Partition -DiskNumber $DiskNumber)
$result = [ordered]@{
    success         = $true
    disk            = $DiskNumber
    model           = $disk.FriendlyName
    serial          = $disk.SerialNumber
    bytes           = $disk.Size
    gib             = [math]::Round($disk.Size / 1GB, 2)
    partition_style = [string]$disk.PartitionStyle
    partitions      = $partitions.Count
    drive_letter    = $volume.DriveLetter
    label           = $volume.FileSystemLabel
    filesystem      = $volume.FileSystem
    free_gib        = [math]::Round($volume.SizeRemaining / 1GB, 2)
    write_read_ok   = $ok
    finished_utc    = (Get-Date).ToUniversalTime().ToString('o')
    log             = $LogPath
}
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
Write-Log 'CONCLUÍDO: cartão com partição única FAT32, pronto para uso'
exit 0
