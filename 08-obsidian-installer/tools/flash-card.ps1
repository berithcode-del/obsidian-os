param(
    [Parameter(Mandatory = $true)][int]$DiskNumber,
    [Parameter(Mandatory = $true)][string]$ExpectedSerial,
    [Parameter(Mandatory = $true)][string]$Image,
    [Parameter(Mandatory = $true)][string]$ImageSha256,
    [string]$Label = 'OBSIDIAN',
    [long]$DataStartSector = 2097152,
    [switch]$SkipDataPartition,
    [string]$LogPath = 'D:\dsh-temp\flash-card.log',
    [string]$ReportPath = 'D:\dsh-temp\flash-card.json'
)
# Limpa o cartão, grava a imagem, confere o início e o fim, declara a partição de
# dados e a formata FAT32. Exige administrador. Recusa disco de sistema, não USB,
# série diferente e imagem com hash divergente.
$ErrorActionPreference = 'Stop'
function Log([string]$m) {
    $line = "{0}  {1}" -f (Get-Date).ToString('HH:mm:ss'), $m
    Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8
    Write-Host $line
}
function Fail([string]$m) {
    Log "FALHA: $m"
    @{ success = $false; stage = 'abortado'; message = $m } | ConvertTo-Json | Set-Content -LiteralPath $ReportPath -Encoding UTF8
    exit 1
}
function Sha256Bytes([byte[]]$b) {
    $h = [Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($h.ComputeHash($b))).Replace('-', '').ToLowerInvariant() } finally { $h.Dispose() }
}
function Diskpart([string[]]$lines) {
    $f = Join-Path (Split-Path -Parent $LogPath) ("dp-{0}.txt" -f ([guid]::NewGuid().ToString('N').Substring(0, 8)))
    Set-Content -LiteralPath $f -Value ($lines + 'exit') -Encoding ASCII
    $out = & "$env:SystemRoot\System32\diskpart.exe" /s $f 2>&1 | Out-String
    $out.Trim().Split("`n") | ForEach-Object { if ($_.Trim()) { Log ("  diskpart: " + $_.Trim()) } }
    return $out
}

Set-Content -LiteralPath $LogPath -Value "=== gravacao $(Get-Date -Format o) ===" -Encoding UTF8

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { Fail 'processo não elevado' }
if (-not (Test-Path $Image)) { Fail "imagem ausente: $Image" }

$disk = Get-Disk -Number $DiskNumber -ErrorAction SilentlyContinue
if (-not $disk) { Fail "disco $DiskNumber não encontrado" }
Log ("alvo: disco {0} · {1} · série {2} · {3:N0} bytes · {4}" -f $disk.Number, $disk.FriendlyName, $disk.SerialNumber, $disk.Size, $disk.BusType)
if ($disk.IsBoot -or $disk.IsSystem) { Fail 'disco de sistema recusado' }
if ($disk.BusType -ne 'USB') { Fail "barramento $($disk.BusType) recusado" }
if ($disk.SerialNumber -ne $ExpectedSerial) { Fail "série $($disk.SerialNumber) difere de $ExpectedSerial" }

$imgInfo = Get-Item $Image
Log ("conferindo hash da imagem local ({0:N0} bytes)" -f $imgInfo.Length)
$imgHash = (Get-FileHash $Image -Algorithm SHA256).Hash.ToLowerInvariant()
if ($imgHash -ne $ImageSha256) { Fail "hash da imagem difere: $imgHash" }
Log 'hash da imagem confere'

Log 'limpando o cartão (remove partições e volumes)'
$out = Diskpart @("select disk $DiskNumber", 'clean')
Start-Sleep -Seconds 2
Update-HostStorageCache -ErrorAction SilentlyContinue
$parts = @(Get-Partition -DiskNumber $DiskNumber -ErrorAction SilentlyContinue)
Log ("partições após clean: {0}" -f $parts.Count)
if ($parts.Count -gt 0) { Fail 'o disco ainda tem partições depois do clean' }

Log 'gravando a imagem'
$handle = "\\.\PhysicalDrive$DiskNumber"
$stream = [IO.File]::Open($handle, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::ReadWrite)
$buffer = New-Object byte[] (4MB)
$written = [int64]0
$sw = [Diagnostics.Stopwatch]::StartNew()
$sha = [Security.Cryptography.SHA256]::Create()
$src = [IO.File]::OpenRead($Image)
try {
    while (($count = $src.Read($buffer, 0, $buffer.Length)) -gt 0) {
        $stream.Write($buffer, 0, $count)
        $null = $sha.TransformBlock($buffer, 0, $count, $null, 0)
        $written += $count
        if ($written % (256MB) -lt $buffer.Length) {
            Log ("  {0:N0} MiB · {1:N1} MiB/s" -f ($written / 1MB), ($written / 1MB / [Math]::Max($sw.Elapsed.TotalSeconds, 0.001)))
        }
    }
    $null = $sha.TransformFinalBlock((New-Object byte[] 0), 0, 0)
    $streamHash = ([BitConverter]::ToString($sha.Hash)).Replace('-', '').ToLowerInvariant()
} finally { $src.Dispose(); $sha.Dispose() }
$stream.Flush($true)
$sw.Stop()
Log ("gravados {0:N0} bytes em {1:N1}s ({2:N2} MiB/s)" -f $written, $sw.Elapsed.TotalSeconds, ($written / 1MB / [Math]::Max($sw.Elapsed.TotalSeconds, 0.001)))
if ($streamHash -ne $ImageSha256) { $stream.Dispose(); Fail "hash do fluxo gravado difere: $streamHash" }

# confere o início e o fim do que está no cartão
Log 'conferindo o cartão (primeiros e últimos 8 MiB)'
$probe = 8MB
$okStart = $false; $okEnd = $false
$stream.Position = 0
$head = New-Object byte[] $probe
$read = 0
while ($read -lt $probe) { $n = $stream.Read($head, $read, $probe - $read); if ($n -le 0) { break }; $read += $n }
$srcHead = New-Object byte[] $probe
$fs = [IO.File]::OpenRead($Image); try { $null = $fs.Read($srcHead, 0, $probe) } finally { $fs.Dispose() }
$okStart = (Sha256Bytes $head) -eq (Sha256Bytes $srcHead)
$stream.Position = $written - $probe
$tail = New-Object byte[] $probe
$read = 0
while ($read -lt $probe) { $n = $stream.Read($tail, $read, $probe - $read); if ($n -le 0) { break }; $read += $n }
$srcTail = New-Object byte[] $probe
$fs = [IO.File]::OpenRead($Image); try { $fs.Position = $imgInfo.Length - $probe; $null = $fs.Read($srcTail, 0, $probe) } finally { $fs.Dispose() }
$okEnd = (Sha256Bytes $tail) -eq (Sha256Bytes $srcTail)
Log ("início confere: {0} · fim confere: {1}" -f $okStart, $okEnd)
if (-not ($okStart -and $okEnd)) { $stream.Dispose(); Fail 'a releitura de conferência divergiu' }

if ($SkipDataPartition) {
    Log 'layout de dados ignorado: o cartão mantém a tabela de partições da imagem'
    $stream.Dispose()
} else {
    $totalSectors = [uint32]($disk.Size / 512)
    $dataSectors = $totalSectors - [uint32]$DataStartSector
    Log ("declarando partição 3: início {0}, {1:N0} setores ({2:N2} GiB)" -f $DataStartSector, $dataSectors, ($dataSectors * 512 / 1GB))
    $mbr = New-Object byte[] 512
    $stream.Position = 0
    $null = $stream.Read($mbr, 0, 512)
    $entry = 446 + 2 * 16
    $mbr[$entry] = 0x00; $mbr[$entry + 1] = 0xFE; $mbr[$entry + 2] = 0xFF; $mbr[$entry + 3] = 0xFF
    $mbr[$entry + 4] = 0x0C; $mbr[$entry + 5] = 0xFE; $mbr[$entry + 6] = 0xFF; $mbr[$entry + 7] = 0xFF
    [BitConverter]::GetBytes([uint32]$DataStartSector).CopyTo($mbr, $entry + 8)
    [BitConverter]::GetBytes([uint32]$dataSectors).CopyTo($mbr, $entry + 12)
    $mbr[510] = 0x55; $mbr[511] = 0xAA
    $stream.Position = 0
    $stream.Write($mbr, 0, 512)
    $stream.Flush($true)
    $stream.Dispose()

    Update-HostStorageCache -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3

    Log 'formatando a partição de dados'
    Diskpart @("rescan", "select disk $DiskNumber", 'select partition 3', "format fs=fat32 quick label=$Label", 'assign') | Out-Null
    Start-Sleep -Seconds 3
    Update-HostStorageCache -ErrorAction SilentlyContinue

    $partitions = @(Get-Partition -DiskNumber $DiskNumber -ErrorAction SilentlyContinue)
    $dataVolume = $null
    foreach ($p in $partitions) {
        $v = Get-Volume -Partition $p -ErrorAction SilentlyContinue
        if ($v) { Log ("  p{0} {1:N2} GB {2} -> {3}: {4} {5}" -f $p.PartitionNumber, ($p.Size / 1GB), $p.Type, $v.DriveLetter, $v.FileSystemLabel, $v.FileSystem) }
        if ($v -and $v.FileSystemLabel -eq $Label) { $dataVolume = $v }
    }
    if ($dataVolume) {
        foreach ($f in @('leitura', 'videos', 'saves')) { New-Item -ItemType Directory -Force (Join-Path ($dataVolume.DriveLetter + ':\') $f) | Out-Null }
        Log ("pasta de dados pronta em {0}:" -f $dataVolume.DriveLetter)
    } else {
        Log 'AVISO: a partição de dados não apareceu com o rótulo esperado'
    }
}

@{ success = $true; disk = $DiskNumber; image_sha256 = $imgHash; bytes_written = $written;
   start_verified = $okStart; end_verified = $okEnd; partitions = $partitions.Count;
   data_volume = $dataVolume.DriveLetter; completed_utc = (Get-Date).ToUniversalTime().ToString('o');
   log = $LogPath } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ReportPath -Encoding UTF8
Log 'CONCLUÍDO'
